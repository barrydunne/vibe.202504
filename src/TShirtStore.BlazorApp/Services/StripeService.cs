using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace TShirtStore.BlazorApp.Services;

// Represents the result from stripe.createPaymentMethod
public class StripePaymentMethodResult
{
    public string? PaymentMethodId { get; set; }
    public string? ErrorMessage { get; set; }
}


public class StripeService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly string _publishableKey;

    public StripeService(IJSRuntime jsRuntime, string publishableKey)
    {
        _jsRuntime = jsRuntime;
        _publishableKey = publishableKey;
    }


    public async Task InitializeStripeElementsAsync(string cardElementId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("initializeStripe", _publishableKey, cardElementId);
            Console.WriteLine("StripeService: Call to initializeStripe completed.");
        }
        catch (JSException jsEx) {
             Console.WriteLine($"StripeService: JSException during InitializeStripeElementsAsync: {jsEx.Message}");
             throw new InvalidOperationException("Failed to initialize Stripe elements via JS interop.", jsEx);
        }
        catch (Exception ex) {
             Console.WriteLine($"StripeService: Generic Exception during InitializeStripeElementsAsync: {ex.Message}");
             throw;
        }
    }

     public async Task<StripePaymentMethodResult> CreatePaymentMethodAsync()
     {
          try {
             return await _jsRuntime.InvokeAsync<StripePaymentMethodResult>("createPaymentMethod");
          } catch (JSException jsEx) {
              Console.WriteLine($"StripeService: JSException during CreatePaymentMethodAsync: {jsEx.Message}");
              return new StripePaymentMethodResult { ErrorMessage = $"JavaScript error creating payment method: {jsEx.Message}" };
          } catch (Exception ex) {
               Console.WriteLine($"StripeService: Generic Exception during CreatePaymentMethodAsync: {ex.Message}");
                return new StripePaymentMethodResult { ErrorMessage = $"Unexpected error creating payment method: {ex.Message}" };
          }
     }
}
