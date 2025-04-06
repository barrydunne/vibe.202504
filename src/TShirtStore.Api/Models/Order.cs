namespace TShirtStore.Api.Models;

public class Order
{
    public int Id { get; set; }
    public required string UserId { get; set; } // Keycloak User ID
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public required string StripePaymentIntentId { get; set; } // Link to payment
    public required List<OrderItem> Items { get; set; } = new();
}
