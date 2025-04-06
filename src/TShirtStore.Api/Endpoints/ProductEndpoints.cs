using Microsoft.EntityFrameworkCore;
using TShirtStore.Api.Data;
using TShirtStore.Shared;

namespace TShirtStore.Api.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/products").WithTags("Products");

        // GET /products - Anonymous access allowed
        group.MapGet("/", async (string? search, AppDbContext dbContext, ILogger<Program> logger) =>
        {
            try
            {
                var query = dbContext.Products.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
                }

                var products = await query
                    .OrderBy(p => p.Name)
                    .Select(p => new ProductDto(p.Id, p.Name, p.Description ?? "", p.Price, p.ImageUrl))
                    .ToListAsync();

                return Results.Ok(products);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching products. Search='{SearchTerm}'", search);
                return Results.Problem("An error occurred while fetching products.");
            }
        })
        .Produces<List<ProductDto>>()
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .WithName("GetProducts")
        .WithOpenApi(o => {
            o.Summary = "Gets a list of products, optionally filtered by search term.";
            o.Description = "Anonymous access is allowed.";
            return o;
        });

        // GET /products/{id} - Example endpoint requiring API Key auth
         group.MapGet("/{id:int}", async (int id, AppDbContext dbContext, ILogger<Program> logger) =>
        {
             try
            {
                var product = await dbContext.Products
                    .AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => new ProductDto(p.Id, p.Name, p.Description ?? "", p.Price, p.ImageUrl))
                    .FirstOrDefaultAsync();

                return product is not null ? Results.Ok(product) : Results.NotFound();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching product with ID {ProductId}", id);
                return Results.Problem($"An error occurred while fetching product {id}.");
            }
        })
        .RequireAuthorization("ApiKeyPolicy") // Requires API Key Authentication
        .Produces<ProductDto>()
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError)
        .Produces(StatusCodes.Status401Unauthorized) // If API Key is missing/invalid
        .Produces(StatusCodes.Status403Forbidden)   // If API Key is valid but lacks permissions (if policy had claims)
        .WithName("GetProductById")
        .WithOpenApi(o => {
            o.Summary = "Gets a specific product by its ID.";
            o.Description = "Requires API Key authentication.";
            return o;
        });
    }
}