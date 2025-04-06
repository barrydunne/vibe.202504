using Microsoft.EntityFrameworkCore;
using TShirtStore.Api.Models;

namespace TShirtStore.Api.Data;

public static class DataSeeder
{
    public static async Task SeedProductsAsync(AppDbContext context)
    {
        if (!await context.Products.AnyAsync())
        {
            var products = new List<Product>
            {
                new Product { Name = "Classic C# Tee", Description = "A timeless classic for C# developers", Price = 19.99m, ImageUrl = "https://picsum.photos/seed/csharp/300/300" },
                new Product { Name = ".NET Core Rocket", Description = "Show your love for high-performance .NET", Price = 22.50m, ImageUrl = "https://picsum.photos/seed/dotnetcore/300/300" },
                new Product { Name = "Blazor Wasm Swag", Description = "Run C# in the browser!", Price = 21.00m, ImageUrl = "https://picsum.photos/seed/blazor/300/300" },
                new Product { Name = "Minimal API Master", Description = "Keep it simple, keep it fast", Price = 20.00m, ImageUrl = "https://picsum.photos/seed/minimalapi/300/300" },
                new Product { Name = "Docker Whale Rider", Description = "Containerize all the things!", Price = 23.95m, ImageUrl = "https://picsum.photos/seed/docker/300/300" },
                new Product { Name = "Async/Await Ninja", Description = "Non-blocking is the way", Price = 18.50m, ImageUrl = "https://picsum.photos/seed/async/300/300" },
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
            Console.WriteLine("--> Seeded initial product data.");
        }
         else
        {
            Console.WriteLine("--> Product data already exists. Seeding skipped.");
        }
    }
}