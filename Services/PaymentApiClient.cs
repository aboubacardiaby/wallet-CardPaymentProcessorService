using System.Net.Http.Json;
using CardPaymentProcessorService.Models;

namespace CardPaymentProcessorService.Services;

public class PaymentApiClient : IPaymentApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentApiClient> _logger;

    public PaymentApiClient(HttpClient httpClient, ILogger<PaymentApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CustomerResult> CreateCustomerAsync(CustomerInfo customer, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating customer with email: {Email}", customer.Email);

        var metadata = customer.Metadata ?? new Dictionary<string, string>();
        metadata["company"] = "Kalipeh Services LLC"; // Add source metadata for tracking
        var request = new
        {
            email = customer.Email,
            name = customer.Name,
            phone = customer.Phone,
            metadata // IDE0028: collection initialization is already simplified by using the variable directly
        };

        var response = await _httpClient.PostAsJsonAsync("/api/payment/customer", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CustomerApiResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize customer response");

        _logger.LogInformation("Customer created with ID: {CustomerId}", result.Id);

        return new CustomerResult(result.Id, result.Email, result.Name);
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(string? customerId, PaymentRequest payment, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating payment intent for {Amount} {Currency}", payment.AmountInCents, payment.Currency);

        var request = new
        {
            amount = payment.AmountInCents,
            currency = payment.Currency,
            customerId = customerId,
            description = payment.Description,
            metadata = payment.Metadata
        };

        var response = await _httpClient.PostAsJsonAsync("/api/payment/intent", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaymentIntentApiResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize payment intent response");

        _logger.LogInformation("Payment intent created with ID: {PaymentIntentId}", result.Id);

        return new PaymentIntentResult(
            result.Id,
            result.ClientSecret,
            result.Status,
            result.Amount,
            result.Currency,
            result.CustomerId
        );
    }

    public async Task<PaymentConfirmResult> ConfirmPaymentAsync(string paymentIntentId, string paymentMethodId, CancellationToken ct = default)
    {
        _logger.LogInformation("Confirming payment intent: {PaymentIntentId}", paymentIntentId);

        var request = new
        {
            paymentIntentId = paymentIntentId,
            paymentMethodId = paymentMethodId
        };

        var response = await _httpClient.PostAsJsonAsync("/api/payment/confirm", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaymentIntentApiResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize confirm response");

        _logger.LogInformation("Payment confirmed with status: {Status}", result.Status);

        return new PaymentConfirmResult(
            result.Id,
            result.Status,
            result.Amount,
            result.Currency
        );
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentIntentId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting payment status for: {PaymentIntentId}", paymentIntentId);

        var response = await _httpClient.GetAsync($"/api/payment/{paymentIntentId}", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaymentStatusApiResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize status response");

        return new PaymentStatusResult(
            result.Id,
            result.Status,
            result.Amount,
            result.AmountReceived,
            result.Currency,
            result.PaymentMethodId,
            result.CustomerId,
            result.Created
        );
    }

    public async Task<PaymentMethodResult> CreatePaymentMethodAsync(CardDetails card, string? customerId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating payment method for card ending in: {Last4}", card.Number[^4..]);

        var request = new
        {
            card = new
            {
                number = card.Number,
                expMonth = card.ExpMonth,
                expYear = card.ExpYear,
                cvc = card.Cvc,
                cardholderName = card.CardholderName
            },
            customerId = customerId
        };

        var response = await _httpClient.PostAsJsonAsync("/api/payment/payment-method", request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<PaymentMethodApiResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize payment method response");

        _logger.LogInformation("Payment method created: {PaymentMethodId}, Brand: {Brand}", result.Id, result.Card.Brand);

        return new PaymentMethodResult(
            result.Id,
            result.Type,
            new CardInfo(result.Card.Brand, result.Card.Last4, result.Card.ExpMonth, result.Card.ExpYear),
            result.CustomerId
        );
    }

    // API response models for deserialization
    private record CustomerApiResponse(string Id, string Email, string? Name);

    private record PaymentIntentApiResponse(
        string Id,
        string ClientSecret,
        string Status,
        long Amount,
        string Currency,
        string? CustomerId
    );

    private record PaymentStatusApiResponse(
        string Id,
        string Status,
        long Amount,
        long AmountReceived,
        string Currency,
        string? PaymentMethodId,
        string? CustomerId,
        DateTime Created
    );

    private record PaymentMethodApiResponse(
        string Id,
        string Type,
        CardApiResponse Card,
        string? CustomerId
    );

    private record CardApiResponse(
        string Brand,
        string Last4,
        int ExpMonth,
        int ExpYear
    );
}
