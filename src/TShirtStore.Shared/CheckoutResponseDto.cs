namespace TShirtStore.Shared;

public record CheckoutResponseDto(bool Success, string Message, int? OrderId);