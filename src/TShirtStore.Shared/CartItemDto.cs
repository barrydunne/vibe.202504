namespace TShirtStore.Shared;

public record CartItemDto(int ProductId, string Name, decimal Price, int Quantity, string ImageUrl);