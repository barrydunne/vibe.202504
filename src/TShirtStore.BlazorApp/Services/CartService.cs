using Blazored.LocalStorage; // Need to add Nuget package: Blazored.LocalStorage
using TShirtStore.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TShirtStore.BlazorApp.Services;

public class CartService
{
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<CartService> _logger;
    private const string CartStorageKey = "tshirt_cart";

    public event Action? OnChange; // Event to notify components of cart changes

    public CartService(ILocalStorageService localStorage, ILogger<CartService> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
    }

    public async Task AddToCartAsync(ProductDto product, int quantity = 1)
    {
        if (product == null || quantity <= 0) return;

        var cart = await GetCartItemsAsync();
        var itemIndex = cart.FindIndex(item => item.ProductId == product.Id); // Find index

        if (itemIndex != -1) // Item exists
        {
            var existingItem = cart[itemIndex];
            // Create new record with updated quantity and replace in list
            cart[itemIndex] = existingItem with { Quantity = existingItem.Quantity + quantity };
            _logger.LogInformation("Updated quantity for Product {ProductId} in cart.", product.Id);
        }
        else // New item
        {
            cart.Add(new CartItemDto(product.Id, product.Name, product.Price, quantity, product.ImageUrl));
            _logger.LogInformation("Added Product {ProductId} to cart.", product.Id);
        }

        await SaveCartAsync(cart);
    }

    public async Task RemoveFromCartAsync(int productId)
    {
        var cart = await GetCartItemsAsync();
        var itemToRemove = cart.FirstOrDefault(item => item.ProductId == productId);

        if (itemToRemove != null)
        {
            cart.Remove(itemToRemove);
             _logger.LogInformation("Removed Product {ProductId} from cart.", productId);
            await SaveCartAsync(cart);
        }
    }

    public async Task UpdateQuantityAsync(int productId, int newQuantity)
    {
        if (newQuantity <= 0) {
            await RemoveFromCartAsync(productId);
            return;
        }

        var cart = await GetCartItemsAsync();
        var itemIndex = cart.FindIndex(item => item.ProductId == productId); // Find index

        if (itemIndex != -1) // Item exists
        {
            var itemToUpdate = cart[itemIndex];
            // Create new record with updated quantity and replace in list
            cart[itemIndex] = itemToUpdate with { Quantity = newQuantity };
            _logger.LogInformation("Updated quantity for Product {ProductId} to {Quantity}.", productId, newQuantity);
            await SaveCartAsync(cart);
        }
    }

    public async Task<List<CartItemDto>> GetCartItemsAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<List<CartItemDto>>(CartStorageKey) ?? new List<CartItemDto>();
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error reading cart from local storage. Returning empty cart.");
             // Attempt to clear potentially corrupted data
             await ClearCartAsync(notify: false); // Avoid infinite loop if ClearCart also fails
             return new List<CartItemDto>();
        }
    }

    public async Task ClearCartAsync(bool notify = true)
    {
        await _localStorage.RemoveItemAsync(CartStorageKey);
         _logger.LogInformation("Cart cleared.");
         if(notify) NotifyStateChanged();
    }

    public async Task<int> GetCartItemCountAsync()
    {
        var cart = await GetCartItemsAsync();
        return cart.Sum(item => item.Quantity);
    }

     public async Task<decimal> GetCartTotalAsync()
    {
        var cart = await GetCartItemsAsync();
        return cart.Sum(item => item.Price * item.Quantity);
    }


    private async Task SaveCartAsync(List<CartItemDto> cart)
    {
        try
        {
            await _localStorage.SetItemAsync(CartStorageKey, cart);
            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cart to local storage.");
            // Decide how to handle this - maybe notify user?
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

// Extension method to handle potential modification of record within a list
public static class CartItemExtensions {
     public static void UpdateQuantity(this List<CartItemDto> cart, int productId, int newQuantity) {
         var index = cart.FindIndex(i => i.ProductId == productId);
         if (index != -1) {
             var currentItem = cart[index];
             cart[index] = currentItem with { Quantity = newQuantity };
         }
     }
}