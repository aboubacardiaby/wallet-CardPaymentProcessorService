using CardPaymentProcessorService.Configuration;
using CardPaymentProcessorService.Models;
using CardPaymentProcessorService.Services;
using CardPaymentProcessorService.Webhooks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<PaymentApiSettings>(
    builder.Configuration.GetSection(PaymentApiSettings.SectionName));

// HTTP Client with retry policy--
builder.Services.AddHttpClient<IPaymentApiClient, PaymentApiClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<PaymentApiSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
})
.AddPolicyHandler(GetRetryPolicy());

// Services
builder.Services.AddSingleton<IPaymentMethodSimulator, PaymentMethodSimulator>();
builder.Services.AddScoped<IPaymentOrchestrator, PaymentOrchestrator>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Card Payment Processor Service",
        Version = "v1",
        Description = "Orchestrates the complete payment flow using CardPaymentApi"
    });
});

var app = builder.Build();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Card Payment Processor Service v1");
    options.RoutePrefix = string.Empty;
});

// API Endpoints
app.MapPost("/api/process/payment", async (
    ProcessPaymentRequest request,
    IPaymentOrchestrator orchestrator,
    CancellationToken ct) =>
{
    var customer = new CustomerInfo(request.Email, request.Name, request.Phone);
    var payment = new PaymentRequest(request.AmountInCents, request.Currency, request.Description);

    var result = await orchestrator.ProcessPaymentAsync(customer, payment, request.Card, request.CardScenario, ct);

    return result.Success ? Results.Ok(result) : Results.BadRequest(result);
})
.WithName("ProcessPayment")
.WithDescription("Executes the complete payment flow. Provide either 'card' (card details) or 'cardScenario' (test scenario)");

app.MapGet("/api/process/status/{paymentIntentId}", async (
    string paymentIntentId,
    IPaymentApiClient apiClient,
    CancellationToken ct) =>
{
    try
    {
        var status = await apiClient.GetPaymentStatusAsync(paymentIntentId, ct);
        return Results.Ok(status);
    }
    catch (HttpRequestException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
})
.WithName("GetPaymentStatus")
.WithDescription("Gets the current status of a payment intent");

// Webhook endpoints
app.MapWebhookEndpoints();

// Health check endpoint for Cloud Run
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithDescription("Health check endpoint for container orchestration")
    .ExcludeFromDescription();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
