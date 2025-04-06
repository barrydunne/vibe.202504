using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Shouldly; // <-- Add Shouldly using
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using TShirtStore.Api.Authentication;
using TShirtStore.Api.Options;
using TShirtStore.Api.Tests.TestUtils;
using Xunit;

namespace TShirtStore.Api.Tests.Authentication;

[Trait("Category", "Unit")]
public class ApiKeyAuthenticationHandlerTests
{
    private readonly Fixture _fixture;
    private readonly ApiKeyAuthenticationOptions _options;
    private readonly TestOptionsMonitor<ApiKeyAuthenticationOptions> _optionsMonitor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly UrlEncoder _urlEncoder;
    private readonly ApiKeyAuthenticationHandler _sut;
    private readonly DefaultHttpContext _httpContext;

    private readonly string _testClientId = "TestClient";
    private readonly string _testApiKey = "TestKey123";
    private readonly string _testSecret = "SecretPassword1";
    private readonly string _testSecretHash; // SHA256 hash of _testSecret

    public ApiKeyAuthenticationHandlerTests()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoNSubstituteCustomization());

        // Calculate hash for the test secret
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_testSecret));
        _testSecretHash = BitConverter.ToString(hashedBytes).Replace("-", "").ToLowerInvariant();

        _options = new ApiKeyAuthenticationOptions
        {
            ApiClients = new List<ApiClientOptions>
            {
                new ApiClientOptions { ClientId = _testClientId, ApiKey = _testApiKey, SecretHash = _testSecretHash } // Use the calculated hash
            }
        };
        _optionsMonitor = new TestOptionsMonitor<ApiKeyAuthenticationOptions>(_options);
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _urlEncoder = UrlEncoder.Default;

        _httpContext = new DefaultHttpContext();

        _sut = new ApiKeyAuthenticationHandler(_optionsMonitor, _loggerFactory, _urlEncoder);
        var scheme = new AuthenticationScheme(ApiKeyAuthenticationHandler.SchemeName, null, typeof(ApiKeyAuthenticationHandler));
        _sut.InitializeAsync(scheme, _httpContext).Wait();
    }

    private void SetHeaders(string? apiKey, string? secret)
    {
        _httpContext.Request.Headers.Remove(ApiKeyAuthenticationHandler.ApiKeyHeaderName);
        _httpContext.Request.Headers.Remove(ApiKeyAuthenticationHandler.SecretHeaderName);
        if (apiKey != null)
            _httpContext.Request.Headers.Append(ApiKeyAuthenticationHandler.ApiKeyHeaderName, new StringValues(apiKey));
        if (secret != null)
            _httpContext.Request.Headers.Append(ApiKeyAuthenticationHandler.SecretHeaderName, new StringValues(secret));
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKeyAndSecret_ShouldReturnSuccess()
    {
        // Arrange
        SetHeaders(_testApiKey, _testSecret);

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Principal.ShouldNotBeNull();
        result.Principal!.Identity!.IsAuthenticated.ShouldBeTrue();
        result.Principal.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(_testClientId);
        result.Principal.FindFirstValue(ClaimTypes.Name).ShouldBe($"ApiClient-{_testClientId}");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingApiKeyHeader_ShouldReturnNoResult()
    {
        // Arrange
        SetHeaders(null, _testSecret);

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingSecretHeader_ShouldReturnFail()
    {
        // Arrange
        SetHeaders(_testApiKey, null);

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure!.Message.ShouldBe("Missing API Secret header.");
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("key", "")]
    [InlineData(null, null)]
    public async Task HandleAuthenticateAsync_EmptyKeyOrSecret_ShouldFailOrNoResult(string? key, string? secret)
    {
        // Arrange
        SetHeaders(key, secret);

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        if(key == null && secret != null) {
             result.None.ShouldBeTrue(); // Missing key header
         } else if (key != null && secret == null) {
              result.Failure.ShouldNotBeNull();
              result.Failure!.Message.ShouldBe("Missing API Secret header.");
         } else if (key == null && secret == null){
              result.None.ShouldBeTrue(); // Missing both headers
         }
         else { // Both headers present but potentially empty
            result.Succeeded.ShouldBeFalse();
            result.Failure.ShouldNotBeNull();
            result.Failure!.Message.ShouldBe("Invalid API Key or Secret format.");
         }
    }

    [Fact]
    public async Task HandleAuthenticateAsync_InvalidApiKey_ShouldReturnFail()
    {
        // Arrange
        SetHeaders("InvalidKey", _testSecret);

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure!.Message.ShouldBe("Invalid API Key.");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidKeyInvalidSecret_ShouldReturnFail()
    {
        // Arrange
        SetHeaders(_testApiKey, "WrongSecret");

        // Act
        var result = await _sut.AuthenticateAsync();

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.Failure.ShouldNotBeNull();
        result.Failure!.Message.ShouldBe("Invalid API Secret.");
    }
}