using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly; // <-- Add Shouldly using
using System.Security.Claims;
using TShirtStore.BlazorApp.Pages;
using TShirtStore.BlazorApp.Services;
using TShirtStore.Shared;
using Xunit;

namespace TShirtStore.BlazorApp.Tests.Components;

[Trait("Category", "Unit")]
public class CheckoutPageTests : TestContext // Inherit from bUnit's TestContext
{
    private readonly Fixture _fixture;

    public CheckoutPageTests()
    {
        _fixture = new Fixture();
        _fixture.Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        Services.AddSingleton(_fixture.Freeze<FakeNavigationManager>());
        Services.AddSingleton(_fixture.Freeze<CartService>());
        Services.AddSingleton(_fixture.Freeze<ApiClient>());
        Services.AddSingleton(_fixture.Freeze<StripeService>());
        Services.AddSingleton(_fixture.Freeze<ILogger<Checkout>>());

        var authContext = this.AddTestAuthorization();
        authContext.SetAuthorized("Test User", AuthorizationState.Authorized);
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, "user-123"));
    }

    [Fact]
    public async Task CheckoutPage_LoadsCartAndInitializesStripe_WhenCartNotEmpty()
    {
        // Arrange
        var cartItems = _fixture.CreateMany<CartItemDto>(2).ToList();
        var cartTotal = cartItems.Sum(i => i.Price * i.Quantity);

        var cartService = Services.GetRequiredService<CartService>();
        var stripeService = Services.GetRequiredService<StripeService>();

        cartService.GetCartItemsAsync().Returns(Task.FromResult(cartItems));
        cartService.GetCartTotalAsync().Returns(Task.FromResult(cartTotal));
        // Assume Stripe init doesn't throw for this test
        stripeService.InitializeStripeElementsAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

        // Act
        var cut = RenderComponent<Checkout>();

        // Assert initial loading state
        cut.WaitForState(() => cut.FindAll("#card-element").Count > 0, TimeSpan.FromSeconds(2));

        // Verify cart loaded
        cut.Markup.ShouldContain(cartItems[0].Name);
        // Use string formatting consistent with how ToString("C") might render locally or use tolerance
        cut.Find("p > strong").TextContent.ShouldBe($"${cartTotal:F2}");

        // Verify StripeService was called (OnAfterRenderAsync runs automatically in bUnit render)
        await stripeService.Received(1).InitializeStripeElementsAsync("card-element");
    }

    [Fact]
    public void CheckoutPage_ShowsEmptyMessage_WhenCartIsEmpty()
    {
        // Arrange
        var cartService = Services.GetRequiredService<CartService>();
        cartService.GetCartItemsAsync().Returns(Task.FromResult(new List<CartItemDto>()));
        cartService.GetCartTotalAsync().Returns(Task.FromResult(0m));

        // Act
        var cut = RenderComponent<Checkout>();

        // Assert
        cut.WaitForState(() => !cut.Markup.Contains("Loading cart..."), TimeSpan.FromSeconds(1));
        cut.Markup.ShouldContain("Your cart is empty.");
        cut.FindAll("#card-element").ShouldBeEmpty();
    }

    [Fact]
    public async Task HandlePlaceOrder_SuccessfulPaymentAndApiCall_ClearsCartAndNavigates()
    {
        // Arrange
        var cartItems = _fixture.CreateMany<CartItemDto>(1).ToList();
        var cartService = Services.GetRequiredService<CartService>();
        var stripeService = Services.GetRequiredService<StripeService>();
        var apiClient = Services.GetRequiredService<ApiClient>();
        var navManager = Services.GetRequiredService<FakeNavigationManager>();

        cartService.GetCartItemsAsync().Returns(Task.FromResult(cartItems));
        cartService.GetCartTotalAsync().Returns(Task.FromResult(cartItems.Sum(i=>i.Price*i.Quantity)));

        var paymentMethodResult = _fixture.Build<StripePaymentMethodResult>()
                                         .With(r => r.PaymentMethodId, "pm_123")
                                         .With(r => r.ErrorMessage, (string?)null)
                                         .Create();
        stripeService.CreatePaymentMethodAsync().Returns(Task.FromResult(paymentMethodResult));

        var checkoutResponse = _fixture.Build<CheckoutResponseDto>()
                                     .With(r => r.Success, true)
                                     .With(r => r.OrderId, 12345)
                                     .Create();
        apiClient.CheckoutAsync(Arg.Is<CheckoutRequestDto>(req => req.StripePaymentMethodId == "pm_123"))
                 .Returns(Task.FromResult<CheckoutResponseDto?>(checkoutResponse));

        // Simulate Stripe being initialized
        var cut = RenderComponent<Checkout>(parameters => {}
             // Could pass parameters here if needed, or trigger lifecycle methods manually if needed for complex init
        );
         // Manually set stripeInitialized state if OnAfterRenderAsync logic is complex/unreliable in test
         await cut.InvokeAsync(() => cut.Instance.stripeInitialized = true); // Directly set the flag for test predictability
         cut.Render(); // Re-render with the flag set

         cut.WaitForState(() => !cut.Find("button.btn-primary").HasAttribute("disabled")); // Wait for button to be enabled

        // Act
        cut.Find("button.btn-primary").Click();

        // Assert
        cut.WaitForState(() => navManager.Uri.EndsWith($"/orderconfirmation/{checkoutResponse.OrderId}"), TimeSpan.FromSeconds(2));

        await stripeService.Received(1).CreatePaymentMethodAsync();
        await apiClient.Received(1).CheckoutAsync(Arg.Is<CheckoutRequestDto>(req => req.StripePaymentMethodId == "pm_123"));
        await cartService.Received(1).ClearCartAsync(true);

        navManager.Uri.ShouldEndWith($"/orderconfirmation/{checkoutResponse.OrderId}");
    }

    // Add similar tests for failure scenarios (Stripe error, API error) using Shouldly assertions
}

// Add internal visibility if testing internal members
// [assembly: InternalsVisibleTo("TShirtStore.BlazorApp.Tests")]