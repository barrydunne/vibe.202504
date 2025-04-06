using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using System.Net.Http;
using TShirtStore.Api.Options;
using TShirtStore.Shared; // Assuming this namespace holds StripeOptions if defined there, otherwise adjust

namespace TShirtStore.Api.Services;

public class PaymentService
{
    private readonly ILogger<PaymentService> _logger;
    private readonly StripeClient _stripeClient; // Use StripeClient
    private readonly bool _isMockApi; // Flag to check if using mock API

    // Inject IHttpClientFactory and IOptions<StripeOptions>
    public PaymentService(
        ILogger<PaymentService> logger,
        IOptions<StripeOptions> stripeOptions, // Inject IOptions
        IHttpClientFactory httpClientFactory) // Inject Factory
    {
        _logger = logger;

        var options = stripeOptions.Value; // Get options instance

        // Create a specific HttpClient configured for Stripe Mock
        // The factory configuration happens in Program.cs
        var httpClient = httpClientFactory.CreateClient("StripeMockClient");

        // Note: BaseAddress is now typically configured in Program.cs via AddHttpClient
        _logger.LogInformation("Using HttpClient with BaseAddress: {ApiBase} for Stripe.", httpClient.BaseAddress);
        if(httpClient.BaseAddress == null)
        {
             _logger.LogWarning("Stripe mock ApiBase was not configured via HttpClient factory. Stripe calls might fail or hit live API.");
        }

        _isMockApi = options.ApiBase.Contains("mock"); // Check if using mock API

        // Create an instance of StripeClient with the specific HttpClient and API key
        // Stripe.net v43+ uses SystemNetHttpClient wrapper
        _stripeClient = new StripeClient(
            apiKey: options.SecretKey,
            httpClient: new SystemNetHttpClient(httpClient),
            apiBase: options.ApiBase);

        _logger.LogInformation("PaymentService initialized with StripeClient instance.");
        _logger.LogInformation("StripeClient.ApiBase explicitly set to: {ApiBase}", _stripeClient.ApiBase);
    }

    public async Task<(bool Succeeded, string PaymentIntentId, string ClientSecret, string ErrorMessage)> CreatePaymentIntentAsync(
        List<CartItemDto> cartItems,
        string paymentMethodId,
        string currency = "usd")
    {
        try
        {
            long totalAmount = (long)(cartItems.Sum(item => item.Price * item.Quantity) * 100); // Stripe expects amount in cents

            if (totalAmount <= 0)
            {
                _logger.LogWarning("CreatePaymentIntent failed: Total amount must be positive. Calculated: {TotalAmount}", totalAmount);
                return (false, string.Empty, string.Empty, "Total amount must be positive.");
            }

            _logger.LogInformation("Creating PaymentIntent via StripeClient for amount {Amount} {Currency}", totalAmount / 100.0m, currency.ToUpper());

            // Create PaymentIntent parameters
            var createOptions = new PaymentIntentCreateOptions
            {
                Amount = totalAmount,
                Currency = currency,
                PaymentMethod = paymentMethodId,
                // ConfirmationMethod = "automatic" (default) is fine for mock usually
                // Confirm = true, // Let Stripe decide based on ConfirmationMethod (automatic default)
                Metadata = new Dictionary<string, string>
                {
                    { "Integration", "TShirtStoreDev" }
                    // Add other relevant metadata
                }
            };

            // Use the StripeClient instance to create the service
            var service = new PaymentIntentService(_stripeClient);
            PaymentIntent paymentIntent = await service.CreateAsync(createOptions);

            _logger.LogInformation("PaymentIntent {PaymentIntentId} created with status {Status}", paymentIntent.Id, paymentIntent.Status);

            // Check the status - stripe-mock might just return 'succeeded' directly
            if (paymentIntent.Status == "succeeded")
            {
                return (true, paymentIntent.Id, paymentIntent.ClientSecret, string.Empty);
            }
            // Handle other statuses if needed (e.g., 'requires_action' for real payments)
            else
            {
                bool isMockSuccess = paymentIntent.Status == "succeeded" ||
                        paymentIntent.Status == "requires_payment_method" || // Common initial status
                        paymentIntent.Status == "requires_confirmation" || // Another possible initial status
                        paymentIntent.Status == "requires_action";        // Potentially this too
                if (_isMockApi && isMockSuccess)
                {
                    _logger.LogInformation("PaymentIntent {PaymentIntentId} considered successful in mock environment (Status: {Status}).", paymentIntent.Id, paymentIntent.Status);
                    // Return success even if status wasn't "succeeded" from the mock
                    return (true, paymentIntent.Id, paymentIntent.ClientSecret ?? string.Empty, string.Empty); // Include ClientSecret if available
                }

                string errorMessage = paymentIntent.LastPaymentError?.Message ?? $"Payment processing resulted in status: {paymentIntent.Status}";
                _logger.LogWarning("PaymentIntent {PaymentIntentId} did not succeed. Status: {Status}, Error: {Error}",
                    paymentIntent.Id, paymentIntent.Status, errorMessage);
                return (false, paymentIntent.Id, paymentIntent.ClientSecret, errorMessage);
            }
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe API error during PaymentIntent creation: SC={StatusCode}, Type={Type}, Code={Code}, Msg={StripeError}",
                ex.HttpStatusCode, ex.StripeError?.Type, ex.StripeError?.Code, ex.StripeError?.Message ?? ex.Message);
            return (false, string.Empty, string.Empty, $"Payment processing error: {ex.StripeError?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment intent creation.");
            return (false, string.Empty, string.Empty, "An unexpected error occurred during payment processing.");
        }
    }
}
