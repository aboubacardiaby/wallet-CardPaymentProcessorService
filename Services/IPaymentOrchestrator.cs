using CardPaymentProcessorService.Models;

namespace CardPaymentProcessorService.Services;

public interface IPaymentOrchestrator
{
    Task<PaymentFlowResult> ProcessPaymentAsync(
        CustomerInfo customer,
        PaymentRequest payment,
        CardDetails? cardDetails = null,
        TestCardScenario? cardScenario = null,
        CancellationToken ct = default
    );
}
