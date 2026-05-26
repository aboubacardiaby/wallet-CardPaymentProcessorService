using CardPaymentProcessorService.Models;

namespace CardPaymentProcessorService.Services;

public class PaymentOrchestrator : IPaymentOrchestrator
{
    private readonly IPaymentApiClient _paymentApiClient;
    private readonly IPaymentMethodSimulator _paymentMethodSimulator;
    private readonly ILogger<PaymentOrchestrator> _logger;

    public PaymentOrchestrator(
        IPaymentApiClient paymentApiClient,
        IPaymentMethodSimulator paymentMethodSimulator,
        ILogger<PaymentOrchestrator> logger)
    {
        _paymentApiClient = paymentApiClient;
        _paymentMethodSimulator = paymentMethodSimulator;
        _logger = logger;
    }

    public async Task<PaymentFlowResult> ProcessPaymentAsync(
        CustomerInfo customer,
        PaymentRequest payment,
        CardDetails? cardDetails = null,
        TestCardScenario? cardScenario = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting payment flow for customer: {Email}, amount: {Amount} {Currency}",
            customer.Email, payment.AmountInCents, payment.Currency);

        string? customerId = null;
        string? paymentIntentId = null;
        string? paymentMethodId = null;
        CardInfo? cardInfo = null;

        try
        {
            // Step 1: Create Customer
            _logger.LogInformation("Step 1: Creating customer...");
            var customerResult = await _paymentApiClient.CreateCustomerAsync(customer, ct);
            customerId = customerResult.CustomerId;
            _logger.LogInformation("Customer created: {CustomerId}", customerId);

            // Step 2: Create Payment Intent
            _logger.LogInformation("Step 2: Creating payment intent...");
            var intentResult = await _paymentApiClient.CreatePaymentIntentAsync(customerId, payment, ct);
            paymentIntentId = intentResult.PaymentIntentId;
            _logger.LogInformation("Payment intent created: {PaymentIntentId}, Status: {Status}",
                paymentIntentId, intentResult.Status);

            // Step 3: Get or Create Payment Method
            if (cardDetails != null)
            {
                // Check if this is a known test card number
                var testCard = _paymentMethodSimulator.GetTestPaymentMethodFromCardNumber(cardDetails.Number);
                if (testCard.HasValue)
                {
                    _logger.LogInformation("Step 3: Using test payment method for card number ****{Last4}",
                        cardDetails.Number[^4..]);
                    paymentMethodId = testCard.Value.PaymentMethodId;
                    cardInfo = new CardInfo(
                        testCard.Value.CardInfo.Brand,
                        cardDetails.Number[^4..],
                        cardDetails.ExpMonth,
                        cardDetails.ExpYear
                    );
                    _logger.LogInformation("Mapped to test payment method: {PaymentMethodId}, Card: {Brand} ****{Last4}",
                        paymentMethodId, cardInfo.Brand, cardInfo.Last4);
                }
                else
                {
                    // Create payment method from provided card details via API (for production)
                    _logger.LogInformation("Step 3: Creating payment method from card details...");
                    var pmResult = await _paymentApiClient.CreatePaymentMethodAsync(cardDetails, customerId, ct);
                    paymentMethodId = pmResult.Id;
                    cardInfo = pmResult.Card;
                    _logger.LogInformation("Payment method created: {PaymentMethodId}, Card: {Brand} ****{Last4}",
                        paymentMethodId, cardInfo.Brand, cardInfo.Last4);
                }
            }
            else
            {
                // Use simulated test payment method
                var scenario = cardScenario ?? TestCardScenario.SuccessfulPayment;
                _logger.LogInformation("Step 3: Using test payment method for scenario: {Scenario}", scenario);
                paymentMethodId = _paymentMethodSimulator.GetTestPaymentMethodId(scenario);
                _logger.LogInformation("Using test payment method: {PaymentMethodId}", paymentMethodId);
            }

            // Step 4: Confirm Payment
            _logger.LogInformation("Step 4: Confirming payment...");
            var confirmResult = await _paymentApiClient.ConfirmPaymentAsync(paymentIntentId, paymentMethodId, ct);
            _logger.LogInformation("Payment confirmed with status: {Status}", confirmResult.Status);

            // Step 5: Get Final Status
            _logger.LogInformation("Step 5: Getting final payment status...");
            var statusResult = await _paymentApiClient.GetPaymentStatusAsync(paymentIntentId, ct);
            _logger.LogInformation("Final payment status: {Status}, Amount received: {AmountReceived}",
                statusResult.Status, statusResult.AmountReceived);

            var success = statusResult.Status == "succeeded";

            return new PaymentFlowResult(
                Success: success,
                CustomerId: customerId,
                PaymentIntentId: paymentIntentId,
                PaymentMethodId: paymentMethodId,
                Status: statusResult.Status,
                Amount: statusResult.Amount,
                Currency: statusResult.Currency,
                Card: cardInfo,
                ErrorMessage: success ? null : $"Payment ended with status: {statusResult.Status}"
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during payment flow");
            return new PaymentFlowResult(
                Success: false,
                CustomerId: customerId,
                PaymentIntentId: paymentIntentId,
                PaymentMethodId: paymentMethodId,
                Status: "failed",
                Amount: payment.AmountInCents,
                Currency: payment.Currency,
                Card: cardInfo,
                ErrorMessage: ex.Message,
                ErrorStep: GetCurrentStep(customerId, paymentIntentId, paymentMethodId)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during payment flow");
            return new PaymentFlowResult(
                Success: false,
                CustomerId: customerId,
                PaymentIntentId: paymentIntentId,
                PaymentMethodId: paymentMethodId,
                Status: "error",
                Amount: payment.AmountInCents,
                Currency: payment.Currency,
                Card: cardInfo,
                ErrorMessage: ex.Message,
                ErrorStep: GetCurrentStep(customerId, paymentIntentId, paymentMethodId)
            );
        }
    }

    private static string GetCurrentStep(string? customerId, string? paymentIntentId, string? paymentMethodId)
    {
        if (customerId == null) return "CreateCustomer";
        if (paymentIntentId == null) return "CreatePaymentIntent";
        if (paymentMethodId == null) return "CreatePaymentMethod";
        return "ConfirmPayment";
    }
}
