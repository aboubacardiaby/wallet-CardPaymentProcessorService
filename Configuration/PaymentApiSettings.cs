namespace CardPaymentProcessorService.Configuration;

public class PaymentApiSettings
{
    public const string SectionName = "PaymentApi";

    public string BaseUrl { get; set; } = "http://localhost:5124";
    public int TimeoutSeconds { get; set; } = 30;
}
