using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Net.Http.Json;
using TShirtStore.Api.Data;
using TShirtStore.Api.Models;
using TShirtStore.Api.Options;
using TShirtStore.Api.Services;
using TShirtStore.Shared; // Need this for DTOs
using Xunit;

namespace TShirtStore.IntegrationTests;

// --- Custom WebApplicationFactory ---
public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // Use Testing environment, loads appsettings.Testing.json
        builder.ConfigureServices(services =>
        {
            // Remove production DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            // Use InMemory database for integration tests
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"InMemoryDbForTesting_{Guid.NewGuid()}");
            });

            // Mock PaymentService if necessary (or rely on stripe-mock via config)
            var paymentServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PaymentService));
            if (paymentServiceDescriptor != null) services.Remove(paymentServiceDescriptor);
            services.AddSingleton<PaymentService>(sp => {
                 var mock = Substitute.For<PaymentService>(
                     sp.GetRequiredService<ILogger<PaymentService>>(),
                     sp.GetRequiredService<IOptions<StripeOptions>>(), // Need IOptions mock/config
                     sp.GetRequiredService<IHttpClientFactory>()
                 );
                 mock.CreatePaymentIntentAsync(Arg.Any<List<CartItemDto>>(), Arg.Any<string>(), Arg.Any<string>())
                     .Returns(Task.FromResult<(bool, string, string, string)>((true, $"pi_mock_{Guid.NewGuid()}", "cs_mock", string.Empty)));
                 return mock;
            });


            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<AppDbContext>();
            var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory<TProgram>>>();
            try {
                db.Database.EnsureCreated(); // Ensure InMemory DB is created
                 logger.LogInformation("Integration test database created/ensured.");
            } catch (Exception ex) {
                 logger.LogError(ex, "An error occurred creating the test database. Error: {ErrorMessage}", ex.Message);
            }
        });
    }
}
