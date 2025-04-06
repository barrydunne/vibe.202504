using Microsoft.EntityFrameworkCore;
using TShirtStore.Api.Data;
using TShirtStore.Api.Models;
using TShirtStore.Shared;

namespace TShirtStore.Api.Services;

public class OrderService
{
    private readonly AppDbContext _dbContext;
    private readonly PaymentService _paymentService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(AppDbContext dbContext, PaymentService paymentService, ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
        _logger = logger;
    }

     // Renamed result tuple for clarity
     public record ProcessOrderResult(bool Success, int? OrderId, string? Message);

    public async Task<ProcessOrderResult> ProcessOrderAsync(string userId, List<CartItemDto> cartItems, string stripePaymentMethodId)
    {
        // 1. Validate Cart Items against DB Products (optional but recommended)
        var productIds = cartItems.Select(ci => ci.ProductId).ToList();
        var products = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        if (products.Count != productIds.Count)
        {
            var missingIds = productIds.Except(products.Select(p => p.Id));
            _logger.LogWarning("Order processing failed for User {UserId}: Invalid product IDs found: {MissingProductIds}", userId, string.Join(", ", missingIds));
            return new ProcessOrderResult(false, null, "One or more products in your cart are invalid.");
        }

         // Recalculate total on the server-side based on DB prices
        decimal serverCalculatedTotal = 0;
        var orderItems = new List<OrderItem>();

        foreach (var cartItem in cartItems)
        {
             var product = products.First(p => p.Id == cartItem.ProductId); // We know it exists now
             if (cartItem.Quantity <= 0)
             {
                  _logger.LogWarning("Order processing failed for User {UserId}: Invalid quantity for Product {ProductId}", userId, product.Id);
                  return new ProcessOrderResult(false, null, $"Invalid quantity for product '{product.Name}'.");
             }

             decimal itemTotal = product.Price * cartItem.Quantity;
             serverCalculatedTotal += itemTotal;

             orderItems.Add(new OrderItem
             {
                 ProductId = product.Id,
                 Product = product, // Link the entity
                 ProductName = product.Name,
                 UnitPrice = product.Price, // Price at time of order
                 Quantity = cartItem.Quantity
             });
        }

        _logger.LogInformation("Server calculated order total: {TotalAmount} for User {UserId}", serverCalculatedTotal, userId);

        // 2. Process Payment via PaymentService (Stripe Mock)
        var paymentResult = await _paymentService.CreatePaymentIntentAsync(cartItems, stripePaymentMethodId);

        if (!paymentResult.Succeeded)
        {
            _logger.LogWarning("Payment failed for User {UserId}. Reason: {PaymentError}", userId, paymentResult.ErrorMessage);
            return new ProcessOrderResult(false, null, $"Payment failed: {paymentResult.ErrorMessage}");
        }

        _logger.LogInformation("PaymentIntent {PaymentIntentId} succeeded for User {UserId}", paymentResult.PaymentIntentId, userId);


        // 3. Create and Save Order to Database
        var order = new Order
        {
            UserId = userId,
            OrderDate = DateTime.UtcNow,
            TotalAmount = serverCalculatedTotal, // Use server-calculated total
            StripePaymentIntentId = paymentResult.PaymentIntentId,
            Items = orderItems // Assign the list created earlier
        };

        // Attach items to the order (EF Core relationship handling)
        // No need for explicit `order.Items.Add` if `orderItems` were created correctly linked.

        _dbContext.Orders.Add(order);

        try
        {
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order {OrderId} successfully saved for User {UserId}", order.Id, userId);
            return new ProcessOrderResult(true, order.Id, "Order placed successfully.");
        }
        catch (DbUpdateException ex)
        {
            // TODO: Handle potential payment reversal/refund here if DB save fails after successful payment.
            // This requires a more robust transaction/saga pattern in production.
             _logger.LogError(ex, "Failed to save order to database for User {UserId} after successful payment {PaymentIntentId}. Manual intervention might be needed.", userId, paymentResult.PaymentIntentId);
             // Attempt to inform the user, but the payment was likely processed.
             return new ProcessOrderResult(false, null, "Order could not be saved after payment processing. Please contact support.");
        }
         catch (Exception ex)
         {
              _logger.LogError(ex, "Unexpected error saving order for User {UserId}", userId);
              // TODO: Consider payment refund logic here too.
              return new ProcessOrderResult(false, null, "An unexpected error occurred while saving your order.");
         }
    }
}