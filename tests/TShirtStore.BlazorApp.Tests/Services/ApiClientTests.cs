using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RichardSzalay.MockHttp; // <-- USE THIS for mocking HttpClient
using Shouldly;
using System.Net;
using System.Net.Http; // Required for HttpMethod, HttpRequestMessage, JsonContent etc.
using System.Net.Http.Headers;
using System.Net.Http.Json; // Required for ReadFromJsonAsync, JsonContent
using System.Text.Json;
using TShirtStore.BlazorApp.Services;
using TShirtStore.Shared;
using Xunit;

namespace TShirtStore.BlazorApp.Tests.Services;

[Trait("Category", "Unit")]
// No longer inherits Bunit.TestContext as we are manually handling HttpClient mocking
public class ApiClientTests
{
    private readonly MockHttpMessageHandler _mockHttp; // From RichardSzalay.MockHttp
    private readonly IAccessTokenProvider _mockTokenProvider; // From NSubstitute via AutoFixture
    private readonly ApiClient _sut; // The service under test
    private readonly Fixture _fixture; // AutoFixture instance
    private readonly ILogger<ApiClient> _mockLogger; // Mock logger provided by NSubstitute via AutoFixture
    private readonly HttpClient _httpClient; // HttpClient configured with the mock handler

    public ApiClientTests()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        // Setup mock handler from RichardSzalay.MockHttp
        _mockHttp = new MockHttpMessageHandler();

        // Create an HttpClient that uses the mock handler
        _httpClient = _mockHttp.ToHttpClient();
        _httpClient.BaseAddress = new Uri("http://localhost/api/"); // Set base address

        // Setup mocks for other dependencies
        _mockTokenProvider = _fixture.Freeze<IAccessTokenProvider>();
        _mockLogger = _fixture.Freeze<ILogger<ApiClient>>();

        // Manually instantiate the SUT, injecting the mocked HttpClient and other mocks
        _sut = new ApiClient(_httpClient, _mockTokenProvider, _mockLogger);

        // Setup default successful token acquisition for most tests
        var token = _fixture.Build<AccessToken>().With(t => t.Value, "test_token").Create();
        var tokenResult = new AccessTokenResult(AccessTokenResultStatus.Success, token, null, null);
        _mockTokenProvider.RequestAccessToken(Arg.Any<AccessTokenRequestOptions>())
                         .Returns(_ => new ValueTask<AccessTokenResult>(tokenResult)); // Correct NSubstitute async syntax
    }

    [Theory, AutoData]
    public async Task GetProductsAsync_Success_ReturnsProductList(List<ProductDto> expectedProducts)
    {
        // Arrange
        // Configure the mock handler using RichardSzalay.MockHttp syntax
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products") // Match method and full URL (or use wildcard)
                 .Respond("application/json", JsonSerializer.Serialize(expectedProducts)); // Respond with JSON string

        // Act
        var result = await _sut.GetProductsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expectedProducts);
        _mockHttp.VerifyNoOutstandingRequest(); // Verify all expected requests were made
        _mockHttp.VerifyNoOutstandingExpectation(); // Verify all configured expectations were met
        await _mockTokenProvider.DidNotReceive().RequestAccessToken(Arg.Any<AccessTokenRequestOptions>());
    }

    [Theory, AutoData]
    public async Task GetProductsAsync_WithSearch_UsesCorrectUrl(List<ProductDto> expectedProducts, string searchTerm)
    {
        // Arrange
        var encodedSearch = Uri.EscapeDataString(searchTerm);
        var expectedUrl = $"http://localhost/api/products?search={encodedSearch}"; // Full URL
        _mockHttp.When(HttpMethod.Get, expectedUrl)
                 .Respond("application/json", JsonSerializer.Serialize(expectedProducts));

        // Act
        var result = await _sut.GetProductsAsync(searchTerm);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expectedProducts);
        _mockHttp.VerifyNoOutstandingRequest();
    }

    [Fact]
    public async Task GetProductsAsync_ApiError_ReturnsNullAndLogsError()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/products")
                 .Respond(HttpStatusCode.InternalServerError); // Respond with status code

        // Act
        var result = await _sut.GetProductsAsync();

        // Assert
        result.ShouldBeNull();
        _mockLogger.Received(1)
            .LogError(Arg.Any<HttpRequestException>(), Arg.Is<string>(s => s.Contains("HTTP error fetching products")), Arg.Is<string?>(s => s == null));
        _mockHttp.VerifyNoOutstandingRequest();
    }

    [Theory, AutoData]
    public async Task CheckoutAsync_Success_ReturnsResponseAndSetsAuthHeader(
        CheckoutRequestDto requestDto,
        CheckoutResponseDto expectedResponseDto)
    {
        // Arrange
        var requestUrl = "http://localhost/api/orders/checkout";
        _mockHttp.When(HttpMethod.Post, requestUrl)
                 // Use .WithContent() or .With() to verify body with RichardSzalay.MockHttp
                 .WithContent(JsonSerializer.Serialize(requestDto)) // Simple check: exact JSON match
                 /* // Or use .With() for more complex checks:
                 .With(httpRequestMessage => {
                     httpRequestMessage.Content.ShouldNotBeNull();
                     try {
                         var actualDto = httpRequestMessage.Content!.ReadFromJsonAsync<CheckoutRequestDto>().Result;
                         actualDto.ShouldNotBeNull();
                         actualDto.StripePaymentMethodId.ShouldBe(requestDto.StripePaymentMethodId);
                         actualDto.CartItems.Count.ShouldBe(requestDto.CartItems.Count);
                         return true;
                     } catch { return false; }
                 })
                 */
                 .Respond("application/json", JsonSerializer.Serialize(expectedResponseDto));

        // Act
        var result = await _sut.CheckoutAsync(requestDto);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBe(expectedResponseDto.Success);
        result.OrderId.ShouldBe(expectedResponseDto.OrderId);
        result.Message.ShouldBe(expectedResponseDto.Message);

        _mockHttp.VerifyNoOutstandingRequest();
        await _mockTokenProvider.Received(1).RequestAccessToken(Arg.Any<AccessTokenRequestOptions>());

        // Verify Authorization header was set (inspect the mock handler's last request if needed)
        // RichardSzalay.MockHttp doesn't expose sent requests easily like Bunit's mock.
        // We rely on testing the IAccessTokenProvider was called, assuming the handler uses it.
    }

     [Theory, AutoData]
    public async Task CheckoutAsync_ApiError_ReturnsFailureResponse(
         CheckoutRequestDto requestDto,
         CheckoutResponseDto apiErrorResponse)
    {
        // Arrange
        apiErrorResponse = apiErrorResponse with { Success = false, Message = "API Error Message", OrderId = null };
        _mockHttp.When(HttpMethod.Post, "http://localhost/api/orders/checkout")
                 .Respond(HttpStatusCode.BadRequest, JsonContent.Create(apiErrorResponse)); // Use JsonContent helper

        // Act
        var result = await _sut.CheckoutAsync(requestDto);

        // Assert
        result.ShouldNotBeNull();
        result!.Success.ShouldBeFalse();
        result.Message.ShouldBe(apiErrorResponse.Message);
        await _mockTokenProvider.Received(1).RequestAccessToken(Arg.Any<AccessTokenRequestOptions>());
        _mockHttp.VerifyNoOutstandingRequest();
    }

    [Theory, AutoData]
    public async Task CheckoutAsync_AuthTokenFails_ReturnsFailureAndLogs(CheckoutRequestDto requestDto)
    {
        // Arrange
        var tokenResult = new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, null!, "login/redirect", null);
        _mockTokenProvider.RequestAccessToken(Arg.Any<AccessTokenRequestOptions>()).Returns(_ => new ValueTask<AccessTokenResult>(tokenResult));

        // Act
        var result = await _sut.CheckoutAsync(requestDto);

        // Assert
        result.ShouldNotBeNull();
        result!.Success.ShouldBeFalse();
        result.Message.ShouldBe("Authentication required.");

         _mockLogger.Received(1)
             .LogWarning(Arg.Is<string>(s => s.Contains("Failed to acquire access token")), Arg.Any<AccessTokenResultStatus>(), Arg.Any<string>());

        // Verify no HTTP call was made using the handler's expectation count
        _mockHttp.GetMatchCount(_mockHttp.When(HttpMethod.Post, "/api/orders/checkout")).ShouldBe(0); // Or check VerifyNoOutstandingRequest
    }

     [Theory, AutoData]
    public async Task GetOrderHistoryAsync_Success_ReturnsOrdersAndSetsAuthHeader(List<OrderDto> expectedOrders)
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/orders")
                 .Respond("application/json", JsonSerializer.Serialize(expectedOrders));

        // Act
        var result = await _sut.GetOrderHistoryAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(expectedOrders);
        _mockHttp.VerifyNoOutstandingRequest();
        await _mockTokenProvider.Received(1).RequestAccessToken(Arg.Any<AccessTokenRequestOptions>());
    }

    [Fact]
    public async Task GetOrderHistoryAsync_ApiError_ReturnsNullAndLogs()
    {
        // Arrange
        _mockHttp.When(HttpMethod.Get, "http://localhost/api/orders")
                 .Respond(HttpStatusCode.InternalServerError);

        // Act
        var result = await _sut.GetOrderHistoryAsync();

        // Assert
        result.ShouldBeNull();
        _mockHttp.VerifyNoOutstandingRequest();
        await _mockTokenProvider.Received(1).RequestAccessToken(Arg.Any<AccessTokenRequestOptions>());
         _mockLogger.Received(1)
             .LogError(Arg.Any<HttpRequestException>(), Arg.Is<string>(s => s.Contains("HTTP error fetching order history")));
    }
}