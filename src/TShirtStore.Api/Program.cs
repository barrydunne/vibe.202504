using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options; // Needed for IOptions DI
using Serilog;
// using Stripe; // Stripe objects are used within PaymentService now
using TShirtStore.Api.Authentication;
using TShirtStore.Api.Data;
using TShirtStore.Api.Endpoints;
using TShirtStore.Api.Middleware;
using TShirtStore.Api.Options;
using TShirtStore.Api.Services;
using System.Text.Json.Serialization; // For Enum conversion

var builder = WebApplication.CreateBuilder(args);

// --- Logging ---
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// --- Configuration ---
// Bind configuration sections directly to IOptions<T>
builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));
// If ApiClient configuration is needed for options pattern elsewhere:
// builder.Services.Configure<List<ApiClientOptions>>(builder.Configuration.GetSection("ApiClients"));

// Get options for immediate use if needed (e.g., HttpClient setup)
var stripeOptionsConfig = builder.Configuration.GetSection("Stripe").Get<StripeOptions>();
var apiClientsConfig = builder.Configuration.GetSection("ApiClients").Get<List<ApiClientOptions>>() ?? new List<ApiClientOptions>(); // Get clients for API key auth handler options


// --- Services ---
// Configure JSON options (e.g., handle enums as strings)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Add JWT Auth Definition
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    // Add API Key Auth Definition
     options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = ApiKeyAuthenticationHandler.ApiKeyHeaderName, // Re-expose constant if needed or use string
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Description = "API Key authentication (Header: X-Api-Key)"
    });
     options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName + "Secret", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = ApiKeyAuthenticationHandler.SecretHeaderName, // Re-expose constant if needed or use string
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, // Treated as API Key for Swagger UI input
        Description = "API Secret authentication (Header: X-Api-Secret)"
    });


    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
             new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationHandler.SchemeName // Apply requirement for Key
                }
            },
            Array.Empty<string>()
        },
        {
             new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationHandler.SchemeName + "Secret" // Apply requirement for Secret
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure Stripe - NO static configuration needed here anymore
// StripeConfiguration.ApiKey = stripeOptions.SecretKey; // REMOVED
// StripeConfiguration.ApiBase = stripeOptions.ApiBase; // REMOVED

// -- HttpClient Configuration --
builder.Services.AddHttpClient("StripeMockClient", client =>
{
    // Configure HttpClient specifically for Stripe interactions
    // Set BaseAddress for stripe-mock if configured
    if (!string.IsNullOrEmpty(stripeOptionsConfig?.ApiBase))
    {
        client.BaseAddress = new Uri(stripeOptionsConfig.ApiBase);
        Console.WriteLine($"--> HttpClient 'StripeMockClient' BaseAddress configured: {stripeOptionsConfig.ApiBase}");
    } else {
        Console.WriteLine("--> Warning: Stripe ApiBase not found in configuration. StripeMockClient may not point to mock.");
    }
    // Add other client configurations like default headers or timeout if needed
});
builder.Services.AddHttpClient(); // Add default factory support if needed elsewhere

// Register PaymentService (now depends on IHttpClientFactory and IOptions<StripeOptions>)
builder.Services.AddScoped<PaymentService>(); // Use Scoped as it uses HttpClientFactory

// Configure DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("OrderDbConnection"),
     npgsqlOptionsAction: sqlOptions => {
         // Optional: Configure Npgsql specific options if needed
         // sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
     }));

builder.Services.AddScoped<OrderService>(); // Register OrderService

// --- Authentication & Authorization ---
builder.Services.AddAuthentication(options => {
        // You might want both schemes to be eligible for authentication,
        // but default to JWT for challenges that require a user login via browser.
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; // Check incoming request for JWT first
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; // Redirect user to login if JWT fails/missing
        // ApiKey scheme will be invoked explicitly by [Authorize(AuthenticationSchemes = ApiKeyAuthenticationHandler.SchemeName)]
        // Or by adding it to the policy requirements.
    })
    .AddJwtBearer(options => // Configure JWT Bearer Token Validation (Keycloak)
    {
        // Authority and Audience now come from IOptions<KeycloakOptions> automatically
        // if registered using builder.Services.Configure<KeycloakOptions>
        // You may need to inject IOptions<KeycloakOptions> here if Configure isn't used
        var keycloakOpts = builder.Configuration.GetSection("Keycloak").Get<KeycloakOptions>()!; // Or use IOptions injection pattern later
        options.Authority = keycloakOpts.Authority;
        options.Audience = keycloakOpts.Audience;
        options.RequireHttpsMetadata = false; // Development only!
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakOpts.Issuer, // Ensure issuer matches Keycloak authority URL
            ValidateAudience = true,
            ValidAudience = keycloakOpts.Audience, // Ensure audience matches 'tshirtstore-api' client ID
            ValidateLifetime = true,
            // ClockSkew = TimeSpan.Zero // Optional: reduce tolerance for token expiration
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, options =>
    {
       // Configure the API Key handler with the clients from configuration
       options.ApiClients = apiClientsConfig;
    });


builder.Services.AddAuthorization(options =>
{
    // Policy for user authenticated via Keycloak JWT
    options.AddPolicy("UserPolicy", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AuthenticationSchemes = new[] { JwtBearerDefaults.AuthenticationScheme }; // This policy only accepts JWT Bearer
        // Example: Add role requirement if Keycloak sends roles
        // policy.RequireRole("user"); // Requires a 'role' claim with value 'user'
    });

    // Policy for client authenticated via API Key
     options.AddPolicy("ApiKeyPolicy", policy =>
    {
        policy.RequireAuthenticatedUser(); // Base requirement: successful auth
        policy.AuthenticationSchemes = new[] { ApiKeyAuthenticationHandler.SchemeName }; // This policy only accepts ApiKey
        // Add specific claims required for API key clients if needed
        // policy.RequireClaim("client_permission", "read:products");
    });
});

// --- CORS for Blazor App ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5001") // Blazor app address (from docker-compose)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
            // Add .AllowCredentials() if needed, but requires more specific origin usually
        });
});

// --- Health Checks ---
builder.Services.AddHealthChecks();

// --- Middleware Pipeline ---
var app = builder.Build();

// Use Serilog request logging - placed early
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
}
else
{
     // Use custom error handling middleware or built-in for prod
     app.UseMiddleware<ErrorHandlingMiddleware>(); // Use custom handler
     // app.UseExceptionHandler("/error"); // Or built-in handler
     // app.MapGet("/error", () => Results.Problem("An unexpected error occurred.", statusCode: 500));
     app.UseHsts(); // Use HSTS in production (requires HTTPS)
}

// app.UseHttpsRedirection(); // Don't use for localhost HTTP development

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");

        logger.LogInformation("Seeding product data...");
        await DataSeeder.SeedProductsAsync(dbContext);
        logger.LogInformation("Product data seeding completed.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        // Depending on policy, you might want to stop the application
        // throw; // Uncomment to stop app startup on DB migration/seed failure
    }
}

// Use CORS policy defined above
app.UseCors("AllowBlazorApp");

// Authentication and Authorization middleware
// IMPORTANT: Place UseAuthentication BEFORE UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// Custom Middleware for robust error handling (if not using UseDeveloperExceptionPage in Dev)
if (!app.Environment.IsDevelopment()) {
     // Already added UseMiddleware<ErrorHandlingMiddleware>(); above for non-dev
}


// --- Map Endpoints ---
app.MapProductEndpoints();
app.MapOrderEndpoints();

// Basic health check endpoint
app.MapHealthChecks("/healthz"); // Changed path slightly to 'healthz' (common practice)

app.Run();

// Make Program class public for WebApplicationFactory in integration tests
public partial class Program { }