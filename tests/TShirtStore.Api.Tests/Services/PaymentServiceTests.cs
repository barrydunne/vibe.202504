using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly; // <-- Add Shouldly using
using TShirtStore.Api.Services;
using TShirtStore.Shared;
using Xunit;

namespace TShirtStore.Api.Tests.Services;

[Trait("Category", "Unit")]
public class PaymentServiceTests
{
    // Unit testing Stripe interactions is limited without mocking interfaces.
    // These tests focus on input validation. Integration tests are preferred.

    [Theory, AutoData] // AutoData provides substitute logger and SUT
    public async Task CreatePaymentIntentAsync_WithZeroTotalAmount_ShouldFail(
        string paymentMethodId,
        [Frozen] ILogger<PaymentService> logger, // AutoFixture provides NSubstitute mock
        PaymentService sut)
    {
        // Arrange
        var cartItems = new List<CartItemDto>
        {
            new CartItemDto(1, "Test", 10.00m, 0, "img.jpg")
        };

        // Act
        var (succeeded, _, _, errorMessage) = await sut.CreatePaymentIntentAsync(cartItems, paymentMethodId);

        // Assert
        succeeded.ShouldBeFalse();
        errorMessage.ShouldBe("Total amount must be positive.");
    }

    [Theory, AutoData]
    public async Task CreatePaymentIntentAsync_WithNegativeTotalAmount_ShouldFail(
       string paymentMethodId,
       [Frozen] ILogger<PaymentService> logger,
       PaymentService sut)
    {
        // Arrange
        var cartItems = new List<CartItemDto>
        {
            new CartItemDto(1, "Test", -10.00m, 1, "img.jpg")
        };

        // Act
        var (succeeded, _, _, errorMessage) = await sut.CreatePaymentIntentAsync(cartItems, paymentMethodId);

        // Assert
        succeeded.ShouldBeFalse();
        errorMessage.ShouldBe("Total amount must be positive.");
    }
}