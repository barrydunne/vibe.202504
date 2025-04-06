namespace TShirtStore.Api.Models;

public class OrderItem
{
    public int Id { get; set; }

    // Foreign key property (EF Core convention)
    public int OrderId { get; set; }
    // Navigation property
    public Order Order { get; set; } = null!; // Initialize to null!, EF Core will populate
    
    public int ProductId { get; set; }
    public required Product Product { get; set; } // Link back to Product
    public required string ProductName { get; set; } // Denormalized for easy display
    public decimal UnitPrice { get; set; } // Price at the time of order
    public int Quantity { get; set; }
}