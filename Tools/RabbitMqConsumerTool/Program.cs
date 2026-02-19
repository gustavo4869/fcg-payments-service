using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: true);

// Add logging
builder.Logging.AddConsole();

// Add the consumer as a hosted service
builder.Services.AddHostedService<PaymentProcessedConsumer>();

var host = builder.Build();
await host.RunAsync();

// Consumer implementation
public class PaymentProcessedConsumer : BackgroundService
{
    private readonly ILogger<PaymentProcessedConsumer> _logger;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;

    public PaymentProcessedConsumer(
        ILogger<PaymentProcessedConsumer> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitializeAsync(stoppingToken);

            _logger.LogInformation("?? Payment Consumer STARTED - Listening for messages...");
            _logger.LogInformation("Press Ctrl+C to stop");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("?? Consumer is shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? Fatal error in consumer");
            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["Messaging:Host"] ?? "localhost",
            Port = _configuration.GetValue<int>("Messaging:Port", 5672),
            UserName = _configuration["Messaging:Username"] ?? "guest",
            Password = _configuration["Messaging:Password"] ?? "guest",
            VirtualHost = _configuration["Messaging:VHost"] ?? "/",
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync("fcg-payments-test-consumer");
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        var exchange = _configuration["Messaging:Exchange"] ?? "payments";
        var queue = _configuration["Messaging:Queue"] ?? "payments-processed";
        var routingKey = _configuration["Messaging:RoutingKey"] ?? "payment.processed";

        // Declare exchange
        await _channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        // Declare queue
        await _channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        // Bind queue
        await _channel.QueueBindAsync(
            queue: queue,
            exchange: exchange,
            routingKey: routingKey,
            cancellationToken: ct);

        await _channel.BasicQosAsync(0, 1, false, ct);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await _channel.BasicConsumeAsync(queue, false, consumer, ct);

        _logger.LogInformation("? Consumer initialized: Exchange={Exchange}, Queue={Queue}", exchange, queue);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var body = ea.Body.ToArray();
            var messageJson = Encoding.UTF8.GetString(body);

            _logger.LogInformation("??????????????????????????????????????????");
            _logger.LogInformation("?? NEW MESSAGE RECEIVED");
            _logger.LogInformation("??????????????????????????????????????????");
            
            // Pretty print JSON
            var jsonDoc = JsonDocument.Parse(messageJson);
            var prettyJson = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            _logger.LogInformation("?? PAYLOAD:\n{Json}", prettyJson);
            
            // Print properties
            _logger.LogInformation("???  PROPERTIES:");
            _logger.LogInformation("   MessageId: {MessageId}", ea.BasicProperties.MessageId);
            _logger.LogInformation("   CorrelationId: {CorrelationId}", ea.BasicProperties.CorrelationId ?? "null");
            _logger.LogInformation("   Timestamp: {Timestamp}", DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime));
            _logger.LogInformation("   ContentType: {ContentType}", ea.BasicProperties.ContentType);
            _logger.LogInformation("   Routing Key: {RoutingKey}", ea.RoutingKey);
            _logger.LogInformation("   Exchange: {Exchange}", ea.Exchange);
            _logger.LogInformation("??????????????????????????????????????????");

            // Deserialize
            var message = JsonSerializer.Deserialize<PaymentProcessedMessage>(messageJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (message != null)
            {
                _logger.LogInformation("?? Payment {PaymentId} | User {UserId} | Status {Status} | Amount {Amount} {Currency}",
                    message.PaymentId, message.UserId, message.Status, message.Amount, message.Currency);
            }

            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "? Error processing message");
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("?? Stopping consumer...");
        
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            await _channel.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.CloseAsync(cancellationToken);
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}

public record PaymentProcessedMessage(
    Guid PaymentId,
    Guid? OrderId,
    Guid UserId,
    Guid GameId,
    string Status,
    decimal Amount,
    string Currency,
    DateTime ProcessedAt,
    Guid? CorrelationId
);
