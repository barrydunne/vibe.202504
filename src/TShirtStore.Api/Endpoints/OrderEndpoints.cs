using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TShirtStore.Api.Data;
using TShirtStore.Api.Models;
using TShirtStore.Api.Services;
using TShirtStore.Shared;

namespace TShirtStore.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/orders").WithTags("Orders");

        // POST /orders/checkout - Requires User JWT Token
        group.MapPost("/checkout", [Authorize(Policy = "UserPolicy")]
        async (CheckoutRequestDto request, ClaimsPrincipal user, OrderService orderService, ILogger<Program> logger) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized(); // Should not happen if [Authorize] works
            }

            logger.LogInformation("Checkout initiated by User {UserId}", userId);

             if (request.CartItems == null || !request.CartItems.Any())
            {
                return Results.BadRequest("Cart cannot be empty.");
            }
             if (string.IsNullOrEmpty(request.StripePaymentMethodId))
            {
                 return Results.BadRequest("Payment method ID is required.");
            }


            try
            {
                var result = await orderService.ProcessOrderAsync(userId, request.CartItems, request.StripePaymentMethodId);

                if (result.Success && result.OrderId.HasValue)
                {
                    logger.LogInformation("Checkout successful for User {UserId}, OrderId {OrderId}", userId, result.OrderId.Value);
                    return Results.Ok(new CheckoutResponseDto(true, "Order placed successfully.", result.OrderId));
                }
                else
                {
                    logger.LogWarning("Checkout failed for User {UserId}. Reason: {Reason}", userId, result.Message);
                    return Results.BadRequest(new CheckoutResponseDto(false, result.Message ?? "Checkout failed.", null));
                }
            }
            catch (Exception ex) // Catch broader exceptions from service layer
            {
                logger.LogError(ex, "Error during checkout process for User {UserId}", userId);
                // Don't expose raw exception details unless configured (use ProblemDetails)
                return Results.Problem($"An unexpected error occurred during checkout: {ex.Message}");
            }
        })
        .Produces<CheckoutResponseDto>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("Checkout")
        .WithOpenApi(o => {
            o.Summary = "Processes the checkout, creates a payment intent, and saves the order.";
            o.Description = "Requires user authentication (JWT Bearer token).";
            return o;
         });


        // GET /orders - Requires User JWT Token
        group.MapGet("/", [Authorize(Policy = "UserPolicy")]
        async (ClaimsPrincipal user, AppDbContext dbContext, ILogger<Program> logger) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            logger.LogInformation("Fetching order history for User {UserId}", userId);

            try
            {
                 var orders = await dbContext.Orders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId)
                    .Include(o => o.Items) // Include order items
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new OrderDto( // Project to DTO
                        o.Id,
                        o.OrderDate,
                        o.TotalAmount,
                        o.Items.Select(oi => new OrderItemDto(
                            oi.ProductId,
                            oi.ProductName,
                            oi.UnitPrice,
                            oi.Quantity
                        )).ToList()
                    ))
                    .ToListAsync();

                return Results.Ok(orders);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching order history for User {UserId}", userId);
                return Results.Problem("An error occurred while fetching your order history.");
            }
        })
        .Produces<List<OrderDto>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("GetOrderHistory")
         .WithOpenApi(o => {
             o.Summary = "Gets the order history for the authenticated user.";
             o.Description = "Requires user authentication (JWT Bearer token).";
             return o;
         });
    }
}