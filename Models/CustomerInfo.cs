namespace CardPaymentProcessorService.Models;

public record CustomerInfo(
    string Email,
    string? Name = null,
    string? Phone = null,
    Dictionary<string, string>? Metadata = null
);

public record CustomerResult(
    string CustomerId,
    string Email,
    string? Name
);
