namespace TShirtStore.Shared;

public record CheckoutRequestDto(List<CartItemDto> CartItems, string StripePaymentMethodId);