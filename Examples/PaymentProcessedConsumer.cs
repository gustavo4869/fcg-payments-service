using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace Fcg.Payments.Consumer.Example
{
    /// <summary>
    /// Example consumer for payment processed events.
    /// This demonstrates how to consume messages published by the Payments API.
    /// </summary>
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

                _logger.LogInformation("Payment Processed Consumer started and listening for messages...");

                // Keep running until cancellation
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Consumer is shutting down...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in consumer");
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
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = await factory.CreateConnectionAsync("fcg-payments-consumer");
            _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

            var exchange = _configuration["Messaging:Exchange"] ?? "payments";
            var queue = _configuration["Messaging:Queue"] ?? "payments-processed";
            var routingKey = _configuration["Messaging:RoutingKey"] ?? "payment.processed";

            // Declare exchange (should already exist from publisher, but safe to redeclare)
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

            // Bind queue to exchange
            await _channel.QueueBindAsync(
                queue: queue,
                exchange: exchange,
                routingKey: routingKey,
                cancellationToken: ct);

            // Set prefetch count (process one message at a time)
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: ct);

            // Create consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += OnMessageReceivedAsync;

            await _channel.BasicConsumeAsync(
                queue: queue,
                autoAck: false, // Manual acknowledgment for reliability
                consumer: consumer,
                cancellationToken: ct);

            _logger.LogInformation(
                "Consumer initialized: Exchange={Exchange}, Queue={Queue}, RoutingKey={RoutingKey}",
                exchange, queue, routingKey);
        }

        private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs ea)
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                _logger.LogInformation("Received message: {MessageJson}", messageJson);

                // Deserialize message
                var message = JsonSerializer.Deserialize<PaymentProcessedMessage>(messageJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (message == null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    // Reject and don't requeue (dead letter if configured)
                    await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                // Process the message
                await ProcessPaymentEventAsync(message);

                // Acknowledge successful processing
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);

                _logger.LogInformation(
                    "Successfully processed payment event: PaymentId={PaymentId}, Status={Status}",
                    message.PaymentId, message.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message. Rejecting and requeueing...");

                // Reject and requeue for retry (consider max retry count in production)
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }

        private async Task ProcessPaymentEventAsync(PaymentProcessedMessage message)
        {
            // Example processing logic:
            // - Update analytics database
            // - Send notification to user
            // - Update game inventory
            // - Trigger reward system
            // - etc.

            _logger.LogInformation(
                "Processing payment: PaymentId={PaymentId}, UserId={UserId}, GameId={GameId}, Amount={Amount}, Status={Status}",
                message.PaymentId, message.UserId, message.GameId, message.Amount, message.Status);

            // Simulate async processing
            await Task.Delay(100);

            // Example: Different handling based on status
            if (message.Status == "Succeeded")
            {
                _logger.LogInformation("Payment succeeded - triggering reward system for user {UserId}", message.UserId);
                // TODO: Call reward service
            }
            else if (message.Status == "Failed")
            {
                _logger.LogWarning("Payment failed - notifying user {UserId}", message.UserId);
                // TODO: Send failure notification
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping consumer...");

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

    // Message contract (should match publisher)
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
}
