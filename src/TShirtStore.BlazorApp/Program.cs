using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using TShirtStore.BlazorApp;
using TShirtStore.BlazorApp.Services;
using Serilog;
using Serilog.Core;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// --- Logging ---
var levelSwitch = new LoggingLevelSwitch();
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.ControlledBy(levelSwitch)
    .Enrich.WithProperty("InstanceId", Guid.NewGuid().ToString("n"))
    .Enrich.FromLogContext()
    .WriteTo.BrowserHttp(
        controlLevelSwitch: levelSwitch,
        endpointUrl: "http://localhost:5341/api/events/raw"
     )
    .WriteTo.BrowserConsole()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(dispose: true);


// --- Configuration ---
var apiBaseAddress = builder.Configuration.GetValue<string>("ApiBaseAddress") ?? "http://localhost:5002";
var keycloakAuthority = builder.Configuration.GetValue<string>("Keycloak:Authority") ?? "http://localhost:8080/realms/tshirtstore";
var keycloakClientId = builder.Configuration.GetValue<string>("Keycloak:ClientId") ?? "tshirtstore-blazor-client";
var stripePublishableKey = builder.Configuration.GetValue<string>("Stripe:PublishableKey") ?? "pk_test_123";


// --- Services ---

// Register the message handler TYPE directly.
// Configuration should be picked up implicitly or by OIDC options.
builder.Services.AddScoped<BaseAddressAuthorizationMessageHandler>();

// Typed HttpClient for API interaction - Use the registered handler type
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseAddress);
})
// Add the TYPE, DI will resolve the scoped instance.
.AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

// Add default HttpClient factory support
builder.Services.AddHttpClient();

// Client-side Cart Service
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<CartService>();

// Stripe Service
builder.Services.AddScoped(sp => new StripeService(
    sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>(),
    stripePublishableKey));

// --- Authentication ---
// Registers AuthenticationStateProvider, IAccessTokenProvider, etc.
// BaseAddressAuthorizationMessageHandler relies on this setup.
builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = keycloakAuthority;
    options.ProviderOptions.ClientId = keycloakClientId;
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Clear();
    options.ProviderOptions.DefaultScopes.Add("openid");
    options.ProviderOptions.DefaultScopes.Add("profile");
    options.ProviderOptions.DefaultScopes.Add("email");
    options.ProviderOptions.DefaultScopes.Add("offline_access");
    options.ProviderOptions.DefaultScopes.Add("tshirtstore_api_scope");
})
.AddAccountClaimsPrincipalFactory<AccountClaimsPrincipalFactory<RemoteUserAccount>>();

// Add base authorization services
builder.Services.AddAuthorizationCore();


// --- Build App ---
var host = builder.Build();

Log.Information("TShirtStore Blazor App Starting...");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Blazor WASM Host terminated unexpectedly during RunAsync.");
}
finally
{
    Log.CloseAndFlush();
}