using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly; // <-- Add Shouldly using
using TShirtStore.Api.Data;
using TShirtStore.Api.Models;
using TShirtStore.Api.Services;
using TShirtStore.Shared;
using Xunit;

namespace TShirtStore.Api.Tests.Services;

[Trait("Category", "Unit")]
public class OrderServiceTests
{
    // AutoFixture setup for automatic mocking and data generation
    private static IFixture CreateFixture()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        fixture.Register(() => dbOptions);
        fixture.Register<Func<AppDbContext>>(() => () => new AppDbContext(fixture.Create<DbContextOptions<AppDbContext>>()));
        fixture.Register(() => Substitute.For<ILogger<OrderService>>());
        fixture.Register(() => Substitute.For<ILogger<PaymentService>>());

        fixture.Customize<Product>(c => c.With(p => p.Price, Math.Abs(fixture.Create<decimal>())));
        fixture.Customize<CartItemDto>(c => c.With(p => p.Price, Math.Abs(fixture.Create<decimal>())));

        using (var context = fixture.Create<Func<AppDbContext>>()())
        {
            if (!context.Products.Any())
            {
                var products = fixture.Build<Product>()
                                      .Without(p => p.Id) // Let DB generate IDs if not set
                                      .CreateMany(3).ToList();
                context.Products.AddRange(products);
                context.SaveChanges();
                fixture.Register(() => products); // Register seeded products
            }
        }
        return fixture;
    }

    // Helper to get seeded products (assumes fixture setup ran)
    private List<Product> GetSeededProducts(IFixture fixture)
    {
        // Resolve products seeded by the fixture setup
        try { return fixture.Create<List<Product>>(); }
        catch { return new List<Product>(); } // Fallback if not seeded/registered
    }

    [Theory, AutoData]
    public async Task ProcessOrderAsync_ValidCartAndPayment_ShouldSucceedAndSaveOrder(
        string userId,
        string paymentMethodId,
        string paymentIntentId,
        string clientSecret,
        [Frozen] PaymentService mockPaymentService, // Frozen uses AutoNSubstitute mock
        OrderService sut) // SUT created by AutoFixture with mocked deps
    {
        // Arrange
        var fixture = CreateFixture(); // Create fixture instance
        var contextFactory = fixture.Create<Func<AppDbContext>>();
        var seededProducts = GetSeededProducts(fixture);
        seededProducts.Count.ShouldBeGreaterThanOrEqualTo(2); // Ensure we have products

        var cartItems = new List<CartItemDto>
        {
            new CartItemDto(seededProducts[0].Id, seededProducts[0].Name, seededProducts[0].Price, 2, seededProducts[0].ImageUrl),
            new CartItemDto(seededProducts[1].Id, seededProducts[1].Name, seededProducts[1].Price, 1, seededProducts[1].ImageUrl)
        };

        decimal expectedTotal = (seededProducts[0].Price * 2) + (seededProducts[1].Price * 1);

        mockPaymentService.CreatePaymentIntentAsync(
                Arg.Is<List<CartItemDto>>(list => list.Count == 2),
                paymentMethodId, "usd")
            .Returns((true, paymentIntentId, clientSecret, string.Empty));

        // Act
        var result = await sut.ProcessOrderAsync(userId, cartItems, paymentMethodId);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.OrderId.ShouldNotBeNull();
        result.OrderId!.Value.ShouldBeGreaterThan(0);
        result.Message.ShouldBe("Order placed successfully.");

        await mockPaymentService.Received(1).CreatePaymentIntentAsync(
             Arg.Is<List<CartItemDto>>(list => list.Count == 2 && list.Any(ci => ci.ProductId == seededProducts[0].Id)),
             paymentMethodId, "usd");

        using var verifyContext = contextFactory();
        var savedOrder = await verifyContext.Orders
                                        .Include(o => o.Items)
                                        .FirstOrDefaultAsync(o => o.Id == result.OrderId.Value);

        savedOrder.ShouldNotBeNull();
        savedOrder!.UserId.ShouldBe(userId);
        savedOrder.TotalAmount.ShouldBe(expectedTotal);
        savedOrder.StripePaymentIntentId.ShouldBe(paymentIntentId);
        savedOrder.Items.ShouldNotBeNull();
        savedOrder.Items.Count.ShouldBe(2);
        savedOrder.Items.ShouldContain(item => item.ProductId == seededProducts[0].Id && item.Quantity == 2 && item.UnitPrice == seededProducts[0].Price);
        savedOrder.Items.ShouldContain(item => item.ProductId == seededProducts[1].Id && item.Quantity == 1 && item.UnitPrice == seededProducts[1].Price);
    }

    [Theory, AutoData]
    public async Task ProcessOrderAsync_PaymentFails_ShouldFailAndNotSaveOrder(
        string userId,
        string paymentMethodId,
        string paymentError,
        [Frozen] Func<AppDbContext> contextFactory,
        [Frozen] PaymentService mockPaymentService,
        OrderService sut)
    {
        // Arrange
        Product seededProduct;
        using (var ctx = contextFactory()) {
             if (!await ctx.Products.AnyAsync()) {
                 ctx.Products.Add(new Product { Name = "P1", Price = 10, ImageUrl="p1.jpg"}); await ctx.SaveChangesAsync();
             }
             seededProduct = await ctx.Products.FirstAsync();
        }
        var cartItems = new List<CartItemDto> { new CartItemDto(seededProduct.Id, seededProduct.Name, seededProduct.Price, 1, seededProduct.ImageUrl) };

        mockPaymentService.CreatePaymentIntentAsync(Arg.Any<List<CartItemDto>>(), paymentMethodId, "usd")
            .Returns((false, "pi_fail", "cs_fail", paymentError));

        // Act
        var result = await sut.ProcessOrderAsync(userId, cartItems, paymentMethodId);

        // Assert
        result.Success.ShouldBeFalse();
        result.OrderId.ShouldBeNull();
        result.Message.ShouldBe($"Payment failed: {paymentError}");

        using var verifyContext = contextFactory();
        var orderExists = await verifyContext.Orders.AnyAsync(o => o.UserId == userId);
        orderExists.ShouldBeFalse();
    }

    [Theory, AutoData]
    public async Task ProcessOrderAsync_InvalidProductId_ShouldFail(
        string userId,
        List<CartItemDto> cartItems, // AutoFixture creates a list
        string paymentMethodId,
        [Frozen] PaymentService mockPaymentService,
        OrderService sut)
    {
        // Arrange
        cartItems.ShouldNotBeEmpty(); // Ensure AutoFixture provided items

        // --- FIX: Create a new list with a modified item ---
        // 1. Take a copy of the list excluding the item to modify (or all items).
        var invalidCartItems = cartItems.Skip(1).ToList(); // Example: take all but the first

        // 2. Create a new DTO instance based on the first item, but with an invalid ID.
        var invalidItem = cartItems.First() with { ProductId = 99999 }; // Use 'with' expression

        // 3. Add the modified item to the new list.
        invalidCartItems.Insert(0, invalidItem);
        // --- END FIX ---

        // Act
        // Use the modified list containing the item with the invalid ID
        var result = await sut.ProcessOrderAsync(userId, invalidCartItems, paymentMethodId);

        // Assert
        result.Success.ShouldBeFalse();
        result.OrderId.ShouldBeNull();
        result.Message.ShouldBe("One or more products in your cart are invalid.");

        await mockPaymentService.DidNotReceive().CreatePaymentIntentAsync(Arg.Any<List<CartItemDto>>(), Arg.Any<string>(), Arg.Any<string>());
    }

     [Theory, AutoData]
    public async Task ProcessOrderAsync_ZeroQuantity_ShouldFail(
         string userId,
         CartItemDto cartItem,
         string paymentMethodId,
         [Frozen] Func<AppDbContext> contextFactory,
         [Frozen] PaymentService mockPaymentService,
         OrderService sut)
    {
        // Arrange
        Product product;
         using (var ctx = contextFactory())
        {
             if (!await ctx.Products.AnyAsync()) {
                 ctx.Products.Add(new Product { Name = "PZero", Price = 10, ImageUrl="pZ.jpg"}); await ctx.SaveChangesAsync();
             }
            product = await ctx.Products.FirstAsync();
        }
         cartItem = cartItem with { ProductId = product.Id, Quantity = 0, Name = product.Name, Price = product.Price, ImageUrl = product.ImageUrl };

        // Act
        var result = await sut.ProcessOrderAsync(userId, new List<CartItemDto> { cartItem }, paymentMethodId);

        // Assert
        result.Success.ShouldBeFalse();
        result.OrderId.ShouldBeNull();
        result.Message.ShouldBe($"Invalid quantity for product '{product.Name}'.");

        await mockPaymentService.DidNotReceive().CreatePaymentIntentAsync(Arg.Any<List<CartItemDto>>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // Note: Testing DB save failure after successful payment requires more advanced mocking
    // or potentially integration testing techniques. This example focuses on core logic paths.
}