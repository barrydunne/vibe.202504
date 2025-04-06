namespace TShirtStore.Shared;

public record OrderDto(int Id, DateTime OrderDate, decimal TotalAmount, List<OrderItemDto> Items);