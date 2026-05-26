namespace CardPaymentProcessorService.Models;

public record PaymentRequest(
    long AmountInCents,
    string Currency = "usd",
    string? Description = null,
    Dictionary<string, string>? Metadata = null
);

public record PaymentIntentResult(
    string PaymentIntentId,
    string ClientSecret,
    string Status,
    long Amount,
    string Currency,
    string? CustomerId
);

public record PaymentConfirmResult(
    string PaymentIntentId,
    string Status,
    long Amount,
    string Currency
);

public record PaymentStatusResult(
    string PaymentIntentId,
    string Status,
    long Amount,
    long AmountReceived,
    string Currency,
    string? PaymentMethodId,
    string? CustomerId,
    DateTime Created
);
