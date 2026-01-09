using Fcg.Payments.Api.Setup;

var builder = WebApplication.CreateBuilder(args);

// Configuração explícita de logging para Azure Container Apps e Application Insights
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Se Application Insights está configurado, adicionar provider
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["ApplicationInsights__ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("ApplicationInsights__ConnectionString")
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Logging.AddApplicationInsights(
        configureTelemetryConfiguration: (config) => config.ConnectionString = appInsightsConnectionString,
        configureApplicationInsightsLoggerOptions: (options) => { }
    );
}

builder.Services.AddApiCore(builder.Configuration);

var app = builder.Build();

// Log de inicialização
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== FCG Payments API Starting ===");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Application Insights Configured: {AppInsightsConfigured}", !string.IsNullOrWhiteSpace(appInsightsConnectionString));

app.UseApiCore();
app.MapV1Endpoints();

logger.LogInformation("=== FCG Payments API Started Successfully ===");
logger.LogInformation("Listening on: {Urls}", string.Join(", ", app.Urls));

app.Run();