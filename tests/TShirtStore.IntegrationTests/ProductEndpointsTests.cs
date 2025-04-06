using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly; // <-- Add Shouldly using
using System.Net;
using System.Net.Http.Json;
using TShirtStore.Api.Data;
using TShirtStore.Api.Models; // Use fully qualified if needed
using TShirtStore.Api.Services;
using TShirtStore.Shared;
using Xunit;
using TShirtStore.Api.Options; // Add if options classes are here
using TShirtStore.Api; // Add for Program class access

namespace TShirtStore.IntegrationTests;

[Trait("Category", "Integration")]
public class ProductEndpointsTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;
    private readonly string _apiKey = "TestApiKey123";
    private readonly string _apiSecret = "TestSecret123";

    public ProductEndpointsTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        EnsureDatabaseSeeded();
    }

    private void EnsureDatabaseSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
        if (!dbContext.Products.Any(p => p.Id >= 101)) // Avoid reseeding if possible
        {
            dbContext.Products.AddRange(
               new Product { Id = 101, Name = "Integration Tee 1", Description = "Desc 1", Price = 10.99m, ImageUrl = "img101.jpg" },
               new Product { Id = 102, Name = "Integration Tee 2", Description = "Desc 2", Price = 15.99m, ImageUrl = "img102.jpg" },
               new Product { Id = 103, Name = "Searchable Tee", Description = "Unique Search Term", Price = 20.00m, ImageUrl = "img103.jpg" }
            );
             // If using identity columns, don't set ID and let DB assign
             /*
               dbContext.Products.AddRange(
                 new Product { Name = "Integration Tee 1", Description = "Desc 1", Price = 10.99m, ImageUrl = "img101.jpg" },
                 new Product { Name = "Integration Tee 2", Description = "Desc 2", Price = 15.99m, ImageUrl = "img102.jpg" },
                 new Product { Name = "Searchable Tee", Description = "Unique Search Term", Price = 20.00m, ImageUrl = "img103.jpg" }
             );
             */
            dbContext.SaveChanges();
        }
    }

    [Fact]
    public async Task GetProducts_ReturnsSuccessAndListOfProducts()
    {
        // Act
        var response = await _client.GetAsync("/products");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.ShouldNotBeNull();
        products.Count.ShouldBeGreaterThanOrEqualTo(3);
        products.ShouldContain(p => p.Name == "Integration Tee 1");
    }

    [Fact]
    public async Task GetProducts_WithSearchTerm_ReturnsFilteredProducts()
    {
        // Arrange
        var searchTerm = "Unique Search Term";

        // Act
        var response = await _client.GetAsync($"/products?search={Uri.EscapeDataString(searchTerm)}");

        // Assert
        response.EnsureSuccessStatusCode(); // Keep this for quick failure
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>();
        products.ShouldNotBeNull();
        products.ShouldHaveSingleItem();
        products[0].Name.ShouldBe("Searchable Tee");
    }

    [Fact]
    public async Task GetProductById_WithValidApiKey_ReturnsProduct()
    {
         // Arrange
         int productId;
         // Get a valid ID from the DB context managed by the factory
         using (var scope = _factory.Services.CreateScope()) {
             var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
             productId = db.Products.First(p => p.Name == "Integration Tee 1").Id;
         }

         _client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
         _client.DefaultRequestHeaders.Add("X-Api-Secret", _apiSecret);

         // Act
         var response = await _client.GetAsync($"/products/{productId}");

         _client.DefaultRequestHeaders.Remove("X-Api-Key");
         _client.DefaultRequestHeaders.Remove("X-Api-Secret");

         // Assert
         response.StatusCode.ShouldBe(HttpStatusCode.OK);
         var product = await response.Content.ReadFromJsonAsync<ProductDto>();
         product.ShouldNotBeNull();
         product.Id.ShouldBe(productId);
         product.Name.ShouldBe("Integration Tee 1");
    }

    [Fact]
    public async Task GetProductById_WithoutApiKey_ReturnsUnauthorized()
    {
        // Arrange
        int productId = 101; // Assume ID or get dynamically
        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Remove("X-Api-Secret");

        // Act
        var response = await _client.GetAsync($"/products/{productId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProductById_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        int productId = 101;
        _client.DefaultRequestHeaders.Add("X-Api-Key", "InvalidKey");
        _client.DefaultRequestHeaders.Add("X-Api-Secret", "InvalidSecret");

        // Act
        var response = await _client.GetAsync($"/products/{productId}");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Remove("X-Api-Secret");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProductById_WithInvalidSecret_ReturnsUnauthorized()
    {
        // Arrange
        int productId = 101;
        _client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        _client.DefaultRequestHeaders.Add("X-Api-Secret", "WrongSecret123");

        // Act
        var response = await _client.GetAsync($"/products/{productId}");

        _client.DefaultRequestHeaders.Remove("X-Api-Key");
        _client.DefaultRequestHeaders.Remove("X-Api-Secret");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProductById_WithNonExistentId_ReturnsNotFound()
    {
         // Arrange
         var productId = 9999;
         _client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
         _client.DefaultRequestHeaders.Add("X-Api-Secret", _apiSecret);

         // Act
         var response = await _client.GetAsync($"/products/{productId}");

         _client.DefaultRequestHeaders.Remove("X-Api-Key");
         _client.DefaultRequestHeaders.Remove("X-Api-Secret");

         // Assert
         response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}

