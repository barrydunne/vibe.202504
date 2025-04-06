// Interop file for interacting with Stripe.js

let stripe = null;
let cardElement = null;
let stripeCardElementId = null; // Store the ID used for the element

// Function to initialize Stripe.js and mount the card element
function initializeStripe(publishableKey, cardElementId) {
    console.log("Initializing Stripe with key:", publishableKey, "Element ID:", cardElementId);
    stripeCardElementId = cardElementId; // Store for later use

    if (!stripe) {
        stripe = Stripe(publishableKey);
    }

    const elements = stripe.elements();

    // Check if element already exists before creating/mounting
    const existingElement = document.getElementById(stripeCardElementId);
    if (!existingElement) {
        console.error(`Stripe card element with ID '${stripeCardElementId}' not found in the DOM.`);
        return;
    }

     // Basic styling, customize as needed
    const style = {
        base: {
            fontSize: '16px',
            color: '#32325d',
            fontFamily: '"Helvetica Neue", Helvetica, sans-serif',
            fontSmoothing: 'antialiased',
            '::placeholder': {
                color: '#aab7c4'
            }
        },
        invalid: {
            color: '#fa755a',
            iconColor: '#fa755a'
        }
    };

    // Create an instance of the card Element. Check if it's already created for this instance.
    if (!cardElement) {
         cardElement = elements.create('card', { style: style, hidePostalCode: true });
         console.log("Stripe card element created.");
    } else {
        console.log("Re-using existing card element instance.");
        // Potentially unmount and remount if needed, or clear errors
         // cardElement.unmount(); // If re-mounting is required
    }


    // Add an instance of the card Element into the `card-element` <div>.
    try {
        // Check if already mounted before mounting again
        // This requires inspecting Stripe's internal state or tracking mounting state externally.
        // A simpler approach for now is just calling mount, Stripe might handle duplicates gracefully or log warnings.
         cardElement.mount(`#${stripeCardElementId}`);
         console.log(`Stripe card element mounted to #${stripeCardElementId}`);

          // Optional: Add event listeners for validation errors
         cardElement.on('change', function(event) {
            const displayError = document.getElementById('card-errors'); // Assuming you have an element with this ID
             if (displayError) {
                 if (event.error) {
                     displayError.textContent = event.error.message;
                 } else {
                     displayError.textContent = '';
                 }
             }
        });

    } catch (error) {
         console.error(`Error mounting Stripe card element to #${stripeCardElementId}:`, error);
         // Try to unmount if mounting fails repeatedly?
         // if (cardElement) cardElement.unmount();
    }


}

// Function to create a payment method
async function createPaymentMethod() {
    console.log("Attempting to create Stripe PaymentMethod...");
    if (!stripe || !cardElement) {
        console.error("Stripe or card element not initialized.");
        return { paymentMethodId: null, errorMessage: "Stripe is not initialized." };
    }

    const hostname = window.location.hostname;
    const isLocalhostTestMode = (hostname === "localhost" || hostname === "127.0.0.1");
    console.log(`JS: Hostname='${hostname}', IsLocalhostTestMode=${isLocalhostTestMode}`);
    if (isLocalhostTestMode) {
        const mockPaymentMethodId = `pm_${Date.now()}_mock${Math.random().toString(36).substring(2, 8)}`;
        console.warn(`JS (Test Mode Active): Forcing mock PaymentMethod ID: ${mockPaymentMethodId}`);
        // Simulate slight delay like a real API call might have
        await new Promise(resolve => setTimeout(resolve, 150));
        return { paymentMethodId: mockPaymentMethodId, errorMessage: null };
    }

    try {
        const { paymentMethod, error } = await stripe.createPaymentMethod({
            type: 'card',
            card: cardElement,
            // billing_details: { // Optional: Add billing details if collected
            //    name: 'Jenny Rosen',
            // },
        });
        console.log("JS createPaymentMethod Result - paymentMethod:", paymentMethod);
        console.log("JS createPaymentMethod Result - error:", error);

        if (error) {
            console.error("Stripe error creating PaymentMethod:", error);
            return { paymentMethodId: null, errorMessage: error.message };
        } else {
            console.log("Stripe PaymentMethod created successfully:", paymentMethod.id);
            return { paymentMethodId: paymentMethod.id, errorMessage: null };
        }
    } catch (ex) {
         console.error("Exception during createPaymentMethod:", ex);
         return { paymentMethodId: null, errorMessage: "An unexpected error occurred while processing payment details." };
    }
}

// Optional: Function to unmount the element if needed (e.g., when component is disposed)
function unmountStripeElement() {
    if (cardElement) {
        try {
             cardElement.unmount();
             console.log(`Stripe card element unmounted from #${stripeCardElementId}`);
             // Consider destroying the element instance if it won't be reused
             // cardElement.destroy();
             // cardElement = null;
        } catch (error) {
             console.error("Error unmounting Stripe element:", error);
        }
    }
}
