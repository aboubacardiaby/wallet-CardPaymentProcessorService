namespace CardPaymentProcessorService.Models;

public record PaymentFlowResult(
    bool Success,
    string? CustomerId,
    string? PaymentIntentId,
    string? PaymentMethodId,
    string? Status,
    long? Amount,
    string? Currency,
    CardInfo? Card = null,
    string? ErrorMessage = null,
    string? ErrorStep = null
);

public record ProcessPaymentRequest(
    string Email,
    string? Name,
    string? Phone,
    long AmountInCents,
    string Currency = "usd",
    string? Description = null,
    CardDetails? Card = null,
    TestCardScenario? CardScenario = null
);

public record CardDetails(
    string Number,
    int ExpMonth,
    int ExpYear,
    string Cvc,
    string? CardholderName = null
);

public record CardInfo(
    string Brand,
    string Last4,
    int ExpMonth,
    int ExpYear
);

public record PaymentMethodResult(
    string Id,
    string Type,
    CardInfo Card,
    string? CustomerId
);

public enum TestCardScenario
{
    SuccessfulPayment,
    DeclinedCard,
    InsufficientFunds,
    ExpiredCard,
    ProcessingError
}
