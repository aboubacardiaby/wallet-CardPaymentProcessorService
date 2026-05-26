using CardPaymentProcessorService.Models;

namespace CardPaymentProcessorService.Services;

public interface IPaymentMethodSimulator
{
    string GetTestPaymentMethodId(TestCardScenario scenario);
    (string PaymentMethodId, CardInfo CardInfo)? GetTestPaymentMethodFromCardNumber(string cardNumber);
}

public class PaymentMethodSimulator : IPaymentMethodSimulator
{
    private static readonly Dictionary<TestCardScenario, string> TestPaymentMethods = new()
    {
        { TestCardScenario.SuccessfulPayment, "pm_card_visa" },
        { TestCardScenario.DeclinedCard, "pm_card_visa_chargeDeclined" },
        { TestCardScenario.InsufficientFunds, "pm_card_visa_chargeDeclinedInsufficientFunds" },
        { TestCardScenario.ExpiredCard, "pm_card_chargeDeclinedExpiredCard" },
        { TestCardScenario.ProcessingError, "pm_card_chargeDeclinedProcessingError" }
    };

    // Maps test card numbers to (PaymentMethodId, Brand)
    private static readonly Dictionary<string, (string Id, string Brand)> TestCardNumbers = new()
    {
        { "4242424242424242", ("pm_card_visa", "visa") },
        { "4000056655665556", ("pm_card_visa_debit", "visa") },
        { "5555555555554444", ("pm_card_mastercard", "mastercard") },
        { "5200828282828210", ("pm_card_mastercard_debit", "mastercard") },
        { "378282246310005", ("pm_card_amex", "amex") },
        { "6011111111111117", ("pm_card_discover", "discover") },
        { "4000000000000002", ("pm_card_visa_chargeDeclined", "visa") },
        { "4000000000009995", ("pm_card_visa_chargeDeclinedInsufficientFunds", "visa") },
        { "4000000000000069", ("pm_card_chargeDeclinedExpiredCard", "visa") },
        { "4000000000000119", ("pm_card_chargeDeclinedProcessingError", "visa") },
    };

    public string GetTestPaymentMethodId(TestCardScenario scenario)
    {
        return TestPaymentMethods.TryGetValue(scenario, out var paymentMethodId)
            ? paymentMethodId
            : TestPaymentMethods[TestCardScenario.SuccessfulPayment];
    }

    public (string PaymentMethodId, CardInfo CardInfo)? GetTestPaymentMethodFromCardNumber(string cardNumber)
    {
        // Remove spaces and dashes
        var cleanNumber = cardNumber.Replace(" ", "").Replace("-", "");

        if (TestCardNumbers.TryGetValue(cleanNumber, out var result))
        {
            var last4 = cleanNumber[^4..];
            var cardInfo = new CardInfo(result.Brand, last4, 12, 2027);
            return (result.Id, cardInfo);
        }

        return null;
    }
}
