using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;
using Blazored.LocalStorage; // Need this for the interface ILocalStorageService
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using TShirtStore.BlazorApp.Services;
using TShirtStore.Shared;
using Xunit;

namespace TShirtStore.BlazorApp.Tests.Services;

[Trait("Category", "Unit")]
public class CartServiceTests
{
    private const string CartStorageKey = "tshirt_cart";

    // AutoFixture setup remains the same
    private static IFixture CreateFixture()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
        fixture.Register(() => Substitute.For<ILogger<CartService>>());
        fixture.Customize<ProductDto>(c => c.With(p => p.Price, Math.Abs(fixture.Create<decimal>())));
        fixture.Customize<CartItemDto>(c => c.With(p => p.Price, Math.Abs(fixture.Create<decimal>())));
        // ILocalStorageService mock is provided automatically by AutoFixture.AutoNSubstitute
        return fixture;
    }

    [Theory, AutoData]
    public async Task GetCartItemsAsync_EmptyStorage_ShouldReturnEmptyList(
        [Frozen] ILocalStorageService mockLocalStorage,
        CartService sut)
    {
        // Arrange
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                        .Returns(_ => new ValueTask<List<CartItemDto>?>((List<CartItemDto>?)null)); // Return null wrapped in ValueTask
        // --- END FIX ---

        // Act
        var result = await sut.GetCartItemsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        await mockLocalStorage.Received(1).GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>());
    }

    [Theory, AutoData]
    public async Task GetCartItemsAsync_WithExistingItems_ShouldReturnItems(
        List<CartItemDto> existingItems,
        [Frozen] ILocalStorageService mockLocalStorage,
        CartService sut)
    {
        // Arrange
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                        .Returns(_ => new ValueTask<List<CartItemDto>?>(existingItems)); // Return list wrapped in ValueTask
        // --- END FIX ---

        // Act
        var result = await sut.GetCartItemsAsync();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBe(existingItems);
        await mockLocalStorage.Received(1).GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>());
    }

    [Theory, AutoData]
    public async Task AddToCartAsync_NewItem_ShouldCallSetItemAsyncWithCorrectList(
        ProductDto productToAdd,
        [Frozen] ILocalStorageService mockLocalStorage,
        CartService sut)
    {
        // Arrange
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                        .Returns(_ => new ValueTask<List<CartItemDto>?>((List<CartItemDto>?)null));
        // --- END FIX ---

        List<CartItemDto>? savedCart = null;
        // --- NSubstitute ValueTask FIX ---
        // Configure mock to capture argument for SetItemAsync (returns ValueTask)
        // Use Returns with a lambda that performs the action and returns default ValueTask
        mockLocalStorage.SetItemAsync(CartStorageKey, Arg.Do<List<CartItemDto>>(list => savedCart = list), Arg.Any<CancellationToken>())
                        .Returns(_ => default(ValueTask)); // Return default ValueTask
        // --- END FIX ---


        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        await sut.AddToCartAsync(productToAdd, 2);

        // Assert
        // --- NSubstitute ValueTask FIX ---
        await mockLocalStorage.Received(1).SetItemAsync(CartStorageKey, Arg.Any<List<CartItemDto>>(), Arg.Any<CancellationToken>());
        // --- END FIX ---
        savedCart.ShouldNotBeNull();
        savedCart!.Count.ShouldBe(1);
        savedCart[0].ProductId.ShouldBe(productToAdd.Id);
        savedCart[0].Quantity.ShouldBe(2);
        // ... other property checks ...
        eventFired.ShouldBeTrue();
    }

    [Theory, AutoData]
    public async Task AddToCartAsync_ExistingItem_ShouldUpdateQuantityInListPassedToSetItemAsync(
         ProductDto productToAdd,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
    {
        // Arrange
        var initialCart = new List<CartItemDto> { /* ... create item ... */ };
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                        .Returns(_ => new ValueTask<List<CartItemDto>?>(initialCart));
        // --- END FIX ---

        List<CartItemDto>? savedCart = null;
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.SetItemAsync(CartStorageKey, Arg.Do<List<CartItemDto>>(list => savedCart = list), Arg.Any<CancellationToken>())
                        .Returns(_ => default(ValueTask));
        // --- END FIX ---

        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        await sut.AddToCartAsync(productToAdd, 3);

        // Assert
        // --- NSubstitute ValueTask FIX ---
        await mockLocalStorage.Received(1).SetItemAsync(CartStorageKey, Arg.Any<List<CartItemDto>>(), Arg.Any<CancellationToken>());
        // --- END FIX ---
        savedCart.ShouldNotBeNull();
        savedCart!.Count.ShouldBe(1);
        savedCart[0].Quantity.ShouldBe(1 + 3);
        eventFired.ShouldBeTrue();
    }

    [Theory, AutoData]
    public async Task RemoveFromCartAsync_ExistingItem_ShouldCallSetItemAsyncWithItemRemoved(
         CartItemDto itemToRemove,
         CartItemDto otherItem,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
    {
        // Arrange
        itemToRemove = itemToRemove with { ProductId = 1 };
        otherItem = otherItem with { ProductId = 2 };
        var initialCart = new List<CartItemDto> { itemToRemove, otherItem };
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                        .Returns(_ => new ValueTask<List<CartItemDto>?>(initialCart));
        // --- END FIX ---

        List<CartItemDto>? savedCart = null;
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.SetItemAsync(CartStorageKey, Arg.Do<List<CartItemDto>>(list => savedCart = list), Arg.Any<CancellationToken>())
                        .Returns(_ => default(ValueTask));
        // --- END FIX ---

        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        await sut.RemoveFromCartAsync(itemToRemove.ProductId);

        // Assert
        // --- NSubstitute ValueTask FIX ---
        await mockLocalStorage.Received(1).SetItemAsync(CartStorageKey, Arg.Any<List<CartItemDto>>(), Arg.Any<CancellationToken>());
        // --- END FIX ---
        savedCart.ShouldNotBeNull();
        savedCart!.Count.ShouldBe(1);
        savedCart[0].ProductId.ShouldBe(otherItem.ProductId);
        eventFired.ShouldBeTrue();
    }

     [Theory, AutoData]
    public async Task UpdateQuantityAsync_ValidQuantity_ShouldCallSetItemAsyncWithUpdatedQuantity(
         CartItemDto itemToUpdate,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
    {
        // Arrange
        itemToUpdate = itemToUpdate with { Quantity = 2 };
        var initialCart = new List<CartItemDto> { itemToUpdate };
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                       .Returns(_ => new ValueTask<List<CartItemDto>?>(initialCart));
        // --- END FIX ---

        List<CartItemDto>? savedCart = null;
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.SetItemAsync(CartStorageKey, Arg.Do<List<CartItemDto>>(list => savedCart = list), Arg.Any<CancellationToken>())
                       .Returns(_ => default(ValueTask));
        // --- END FIX ---

        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        await sut.UpdateQuantityAsync(itemToUpdate.ProductId, 5);

        // Assert
        // --- NSubstitute ValueTask FIX ---
        await mockLocalStorage.Received(1).SetItemAsync(CartStorageKey, Arg.Any<List<CartItemDto>>(), Arg.Any<CancellationToken>());
        // --- END FIX ---
        savedCart.ShouldNotBeNull();
        savedCart!.Count.ShouldBe(1);
        savedCart[0].Quantity.ShouldBe(5);
        eventFired.ShouldBeTrue();
    }

     [Theory]
     [InlineAutoData(0)]
     [InlineAutoData(-1)]
    public async Task UpdateQuantityAsync_InvalidQuantity_ShouldCallSetItemAsyncWithItemRemoved(
         int invalidQuantity,
         CartItemDto itemToUpdate,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
    {
        // Arrange
        itemToUpdate = itemToUpdate with { Quantity = 2 };
        var initialCart = new List<CartItemDto> { itemToUpdate };
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                       .Returns(_ => new ValueTask<List<CartItemDto>?>(initialCart));
        // --- END FIX ---

        List<CartItemDto>? savedCart = null;
        // --- NSubstitute ValueTask FIX ---
        mockLocalStorage.SetItemAsync(CartStorageKey, Arg.Do<List<CartItemDto>>(list => savedCart = list), Arg.Any<CancellationToken>())
                       .Returns(_ => default(ValueTask));
        // --- END FIX ---

        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        await sut.UpdateQuantityAsync(itemToUpdate.ProductId, invalidQuantity);

        // Assert
        // --- NSubstitute ValueTask FIX ---
        // RemoveFromCartAsync calls SaveCartAsync internally, which calls SetItemAsync
        await mockLocalStorage.Received(1).SetItemAsync(CartStorageKey, Arg.Any<List<CartItemDto>>(), Arg.Any<CancellationToken>());
        // --- END FIX ---
        savedCart.ShouldNotBeNull();
        savedCart.ShouldBeEmpty();
        eventFired.ShouldBeTrue();
    }

     [Theory, AutoData]
     public async Task GetCartItemCountAsync_ShouldReturnCorrectSum(
          List<CartItemDto> items,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
     {
         // Arrange
         items = items.Select(i => i with { Quantity = Math.Abs(i.Quantity) + 1}).ToList();
         var expectedCount = items.Sum(i => i.Quantity);
         // --- NSubstitute ValueTask FIX ---
         mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                       .Returns(_ => new ValueTask<List<CartItemDto>?>(items));
         // --- END FIX ---

         // Act
         var count = await sut.GetCartItemCountAsync();

         // Assert
         count.ShouldBe(expectedCount);
         await mockLocalStorage.Received(1).GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>());
     }

     [Theory, AutoData]
     public async Task GetCartTotalAsync_ShouldReturnCorrectSum(
          List<CartItemDto> items,
         [Frozen] ILocalStorageService mockLocalStorage,
         CartService sut)
     {
         // Arrange
         items = items.Select(i => i with { Quantity = Math.Abs(i.Quantity) + 1, Price = Math.Abs(i.Price)}).ToList();
         var expectedTotal = items.Sum(i => i.Price * i.Quantity);
         // --- NSubstitute ValueTask FIX ---
         mockLocalStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>())
                       .Returns(_ => new ValueTask<List<CartItemDto>?>(items));
         // --- END FIX ---

         // Act
         var total = await sut.GetCartTotalAsync();

         // Assert
         total.ShouldBe(expectedTotal);
         await mockLocalStorage.Received(1).GetItemAsync<List<CartItemDto>>(CartStorageKey, Arg.Any<CancellationToken>());
     }

     [Theory, AutoData]
     public async Task ClearCartAsync_ShouldCallRemoveItemAsyncAndNotify(
         [Frozen] ILocalStorageService mockLocalStorage, // Don't need initial items for this test
         CartService sut)
     {
         // Arrange
         // Setup RemoveItemAsync mock (returns ValueTask)
         // --- NSubstitute ValueTask FIX ---
         mockLocalStorage.RemoveItemAsync(CartStorageKey, Arg.Any<CancellationToken>())
                         .Returns(_ => default(ValueTask)); // Return default ValueTask
         // --- END FIX ---

         bool eventFired = false;
         sut.OnChange += () => eventFired = true;

         // Act
         await sut.ClearCartAsync();

         // Assert
         // --- NSubstitute ValueTask FIX ---
         await mockLocalStorage.Received(1).RemoveItemAsync(CartStorageKey, Arg.Any<CancellationToken>());
         // --- END FIX ---
         eventFired.ShouldBeTrue();
     }
}