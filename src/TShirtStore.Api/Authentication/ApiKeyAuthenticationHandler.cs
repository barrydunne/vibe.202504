using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using System.Text;
using TShirtStore.Api.Options;

namespace TShirtStore.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public List<ApiClientOptions> ApiClients { get; set; } = new();
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string ApiKeyHeaderName = "X-Api-Key";
    public const string SecretHeaderName = "X-Api-Secret"; // Or use HMAC signature

    private readonly ILogger<ApiKeyAuthenticationHandler> _logger;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
        _logger = logger.CreateLogger<ApiKeyAuthenticationHandler>();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult()); // No API key header
        }
         if (!Request.Headers.TryGetValue(SecretHeaderName, out var secretValues))
        {
            _logger.LogWarning("Missing API Secret header for ApiKey {ApiKey}", apiKeyValues.FirstOrDefault());
            return Task.FromResult(AuthenticateResult.Fail("Missing API Secret header."));
        }

        var providedApiKey = apiKeyValues.FirstOrDefault();
        var providedSecret = secretValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedApiKey) || string.IsNullOrWhiteSpace(providedSecret))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API Key or Secret format."));
        }

        // Find the client by API Key
        var client = Options.ApiClients.FirstOrDefault(c => c.ApiKey == providedApiKey);

        if (client == null)
        {
             _logger.LogWarning("Invalid API Key provided: {ApiKey}", providedApiKey);
             return Task.FromResult(AuthenticateResult.Fail("Invalid API Key."));
        }

        // IMPORTANT: Compare the provided secret with the STORED HASHED SECRET
        // In a real app, hash the providedSecret using the same algorithm and salt as when storing it.
        // For this example, we'll do a simple comparison assuming the config has the plain secret (NOT SECURE FOR PROD)
        // Replace this with proper HASH comparison (e.g., using BCrypt.Net or ASP.NET Core Identity Hasher)
        bool isSecretValid = VerifySecret(providedSecret, client.SecretHash); // Use the HASHED value

        if (!isSecretValid)
        {
             _logger.LogWarning("Invalid API Secret provided for ClientId: {ClientId}", client.ClientId);
             return Task.FromResult(AuthenticateResult.Fail("Invalid API Secret."));
        }


        // If valid, create claims principal
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, client.ClientId), // Identify the client
            new Claim(ClaimTypes.Name, $"ApiClient-{client.ClientId}"),
            // Add other claims representing client permissions if needed
            // new Claim("permission", "read:products"),
         };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        _logger.LogInformation("API Client {ClientId} authenticated successfully.", client.ClientId);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

     // Example Hashing (use a robust library like BCrypt.Net in production)
    private bool VerifySecret(string providedSecret, string storedHash)
    {
        // THIS IS A PLACEHOLDER - DO NOT USE IN PRODUCTION
        // In reality, you'd re-hash providedSecret and compare hashes.
        // Example using simple SHA256 for demonstration (still not ideal without salt)
         using var sha256 = SHA256.Create();
         var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(providedSecret));
         var hashString = BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();
         return hashString == storedHash; // Compare against the hash stored in config/db
    }

     protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append("WWW-Authenticate", SchemeName); // Indicate API Key auth is expected
        return Task.CompletedTask;
    }

     protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
         Response.StatusCode = StatusCodes.Status403Forbidden;
         return Task.CompletedTask;
    }
}