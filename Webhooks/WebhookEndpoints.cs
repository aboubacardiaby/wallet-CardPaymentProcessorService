using Stripe;

namespace CardPaymentProcessorService.Webhooks;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        app.MapPost("/webhook/stripe", HandleStripeWebhook)
            .WithName("StripeWebhook")
            .WithDescription("Receives Stripe webhook events");
    }

    private static async Task<IResult> HandleStripeWebhook(
        HttpContext context,
        IConfiguration configuration,
        ILogger<Program> logger)
    {
        var json = await new StreamReader(context.Request.Body).ReadToEndAsync();
        var webhookSecret = configuration["Stripe:WebhookSecret"];

        try
        {
            Event stripeEvent;

            if (string.IsNullOrEmpty(webhookSecret))
            {
                logger.LogWarning("Webhook secret not configured, parsing event without verification");
                stripeEvent = EventUtility.ParseEvent(json);
            }
            else
            {
                var signature = context.Request.Headers["Stripe-Signature"].ToString();
                stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);
            }

            logger.LogInformation("Received Stripe webhook event: {EventType}, ID: {EventId}",
                stripeEvent.Type, stripeEvent.Id);

            switch (stripeEvent.Type)
            {
                case EventTypes.PaymentIntentSucceeded:
                    var successIntent = stripeEvent.Data.Object as PaymentIntent;
                    logger.LogInformation("Payment succeeded: {PaymentIntentId}, Amount: {Amount}",
                        successIntent?.Id, successIntent?.AmountReceived);
                    break;

                case EventTypes.PaymentIntentPaymentFailed:
                    var failedIntent = stripeEvent.Data.Object as PaymentIntent;
                    logger.LogWarning("Payment failed: {PaymentIntentId}, Error: {Error}",
                        failedIntent?.Id, failedIntent?.LastPaymentError?.Message);
                    break;

                case EventTypes.ChargeRefunded:
                    var refundedCharge = stripeEvent.Data.Object as Charge;
                    logger.LogInformation("Charge refunded: {ChargeId}, Amount refunded: {Amount}",
                        refundedCharge?.Id, refundedCharge?.AmountRefunded);
                    break;

                case EventTypes.CustomerCreated:
                    var customer = stripeEvent.Data.Object as Customer;
                    logger.LogInformation("Customer created: {CustomerId}, Email: {Email}",
                        customer?.Id, customer?.Email);
                    break;

                default:
                    logger.LogDebug("Unhandled event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Results.Ok(new { received = true, eventType = stripeEvent.Type });
        }
        catch (StripeException ex)
        {
            logger.LogError(ex, "Stripe webhook verification failed");
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
