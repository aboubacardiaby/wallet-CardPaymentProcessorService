using CardPaymentProcessorService.Models;

namespace CardPaymentProcessorService.Services;

public interface IPaymentApiClient
{
    Task<CustomerResult> CreateCustomerAsync(CustomerInfo customer, CancellationToken ct = default);
    Task<PaymentIntentResult> CreatePaymentIntentAsync(string? customerId, PaymentRequest payment, CancellationToken ct = default);
    Task<PaymentConfirmResult> ConfirmPaymentAsync(string paymentIntentId, string paymentMethodId, CancellationToken ct = default);
    Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentIntentId, CancellationToken ct = default);
    Task<PaymentMethodResult> CreatePaymentMethodAsync(CardDetails card, string? customerId = null, CancellationToken ct = default);
}
