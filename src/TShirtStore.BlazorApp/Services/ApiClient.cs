using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using TShirtStore.Shared;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace TShirtStore.BlazorApp.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly ILogger<ApiClient> _logger;


    public ApiClient(HttpClient httpClient, IAccessTokenProvider tokenProvider, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var tokenResult = await _tokenProvider.RequestAccessToken();

        if (tokenResult.TryGetToken(out var token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);
            _logger.LogDebug("Access token acquired successfully.");
        }
        else
        {
            // Update log message to use newer property names if available, or simplify
            _logger.LogWarning("Failed to acquire access token. Status: {Status}, InteractiveRequestUrl: {Url}", // Updated property name
                tokenResult.Status, tokenResult.InteractiveRequestUrl); // Use InteractiveRequestUrl
            _httpClient.DefaultRequestHeaders.Authorization = null; // Ensure no stale token
        }
        return _httpClient;
    }

      private async Task<HttpClient> GetClientAsync(bool requireAuth = false)
    {
        if (requireAuth)
        {
            return await GetAuthenticatedClientAsync();
        }
        // Ensure auth header is clear for anonymous requests if it was set previously
        _httpClient.DefaultRequestHeaders.Authorization = null;
        return _httpClient;
    }

    // --- Product Methods ---
    public async Task<List<ProductDto>?> GetProductsAsync(string? searchTerm = null)
    {
        try
        {
            var client = await GetClientAsync(requireAuth: false); // Anonymous access
            var url = "/products";
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                url += $"?search={Uri.EscapeDataString(searchTerm)}";
            }
            return await client.GetFromJsonAsync<List<ProductDto>>(url);
        }
        catch (AccessTokenNotAvailableException ex) // Should not happen for anonymous
        {
             _logger.LogError(ex, "Access token error during anonymous GetProductsAsync call.");
             ex.Redirect(); // Should not be needed here, but as safety
             return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching products. Search='{SearchTerm}'", searchTerm);
            return null; // Or throw custom exception / return error state
        }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Unexpected error fetching products.");
            return null;
         }
    }

     // Example of calling an API-Key protected endpoint (not typically done from frontend)
     /*
    public async Task<ProductDto?> GetProductByIdAsync(int id, string apiKey, string apiSecret)
    {
        try
        {
            var client = await GetClientAsync(requireAuth: false); // No user auth needed
            client.DefaultRequestHeaders.Remove("X-Api-Key"); // Ensure clean headers
            client.DefaultRequestHeaders.Remove("X-Api-Secret");
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
            client.DefaultRequestHeaders.Add("X-Api-Secret", apiSecret);

            var response = await client.GetAsync($"/products/{id}");

             // Clear API Key headers after use
             client.DefaultRequestHeaders.Remove("X-Api-Key");
             client.DefaultRequestHeaders.Remove("X-Api-Secret");


            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ProductDto>();
            }
             else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
             {
                 return null;
             }
             else
             {
                 _logger.LogError("API Key protected request failed with status {StatusCode} for ProductId {ProductId}", response.StatusCode, id);
                 // Handle specific errors like 401/403 if needed
                 return null;
             }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling API-Key protected endpoint GetProductByIdAsync for {ProductId}", id);
             // Clear API Key headers in case of exception
             _httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
             _httpClient.DefaultRequestHeaders.Remove("X-Api-Secret");
            return null;
        }
    }
    */


    // --- Order Methods ---
    public async Task<CheckoutResponseDto?> CheckoutAsync(CheckoutRequestDto checkoutRequest)
    {
         try
        {
             var client = await GetClientAsync(requireAuth: true); // Requires user authentication
             var response = await client.PostAsJsonAsync("/orders/checkout", checkoutRequest);

             if (response.IsSuccessStatusCode)
             {
                return await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
             }
             else
             {
                // Attempt to read error details if available (ProblemDetails or custom DTO)
                var errorContent = await response.Content.ReadAsStringAsync();
                 _logger.LogWarning("Checkout API call failed with status {StatusCode}. Response: {ErrorResponse}", response.StatusCode, errorContent);

                 try {
                     // Try parsing as standard CheckoutResponseDto first for expected failures
                     var errorDto = await response.Content.ReadFromJsonAsync<CheckoutResponseDto>();
                    if (errorDto != null) return errorDto; // Return the failure response from API
                 } catch (JsonException) { /* Ignore if not parsable as CheckoutResponseDto */ }

                 // Fallback for unexpected errors
                 return new CheckoutResponseDto(false, $"Checkout failed. Status: {response.StatusCode}. Please try again or contact support.", null);
             }
        }
        catch (AccessTokenNotAvailableException ex)
        {
             _logger.LogWarning("Access token not available for checkout. Redirecting.");
             ex.Redirect();
             return new CheckoutResponseDto(false, "Authentication required.", null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during checkout.");
            return new CheckoutResponseDto(false, "Network error during checkout. Please try again.", null);
        }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Unexpected error during checkout.");
             return new CheckoutResponseDto(false, "An unexpected error occurred during checkout.", null);
         }
    }

    public async Task<List<OrderDto>?> GetOrderHistoryAsync()
    {
         try
        {
            var client = await GetClientAsync(requireAuth: true); // Requires user authentication
            return await client.GetFromJsonAsync<List<OrderDto>>("/orders");
        }
        catch (AccessTokenNotAvailableException ex)
        {
             _logger.LogWarning("Access token not available for getting order history. Redirecting.");
             ex.Redirect();
             return null;
        }
         catch (HttpRequestException ex)
        {
             _logger.LogError(ex, "HTTP error fetching order history.");
             return null;
        }
         catch (Exception ex)
         {
             _logger.LogError(ex, "Unexpected error fetching order history.");
             return null;
         }
    }
}