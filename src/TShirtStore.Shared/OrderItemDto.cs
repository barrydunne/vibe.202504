namespace TShirtStore.Shared;

public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity);