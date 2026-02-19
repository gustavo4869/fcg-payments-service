using Fcg.Payments.Api.Domain.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Fcg.Payments.Api.Infra.Messaging
{
    public class RabbitMqPaymentRequestPublisher : IPaymentRequestPublisher, IAsyncDisposable
    {
        private readonly ILogger<RabbitMqPaymentRequestPublisher> _logger;
        private readonly MessagingOptions _options;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private IConnection? _connection;
        private bool _disposed;

        public RabbitMqPaymentRequestPublisher(
            ILogger<RabbitMqPaymentRequestPublisher> logger,
            IOptions<MessagingOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task PublishPaymentPendingAsync(PaymentPendingMessage message, CancellationToken ct = default)
        {
            if (!_options.Enabled)
            {
                _logger.LogDebug("Messaging disabled - skipping publish for payment {PaymentId}", message.PaymentId);
                return;
            }

            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;
                    await PublishInternalAsync(message, ct);
                    
                    _logger.LogInformation(
                        "Published payment.pending event for {PaymentId} (attempt {Attempt})",
                        message.PaymentId, attempt);
                    
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish payment.pending for {PaymentId} (attempt {Attempt}/{MaxRetries}). Retrying...",
                        message.PaymentId, attempt, maxRetries);
                    
                    await InvalidateConnectionAsync();
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to publish payment.pending for {PaymentId} after {MaxRetries} attempts.",
                        message.PaymentId, maxRetries);
                    
                    await InvalidateConnectionAsync();
                    return;
                }
            }
        }

        private async Task PublishInternalAsync(PaymentPendingMessage message, CancellationToken ct)
        {
            var connection = await GetOrCreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            
            // Single queue for payment processing
            const string queue = "payment.pending";
            const string exchange = "payments";
            const string routingKey = "payment.pending";

            await channel.ExchangeDeclareAsync(
                exchange: exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            await channel.QueueBindAsync(
                queue: queue,
                exchange: exchange,
                routingKey: routingKey,
                cancellationToken: ct);

            _logger.LogDebug(
                "Ensured infrastructure: Exchange={Exchange}, Queue={Queue}, RoutingKey={RoutingKey}",
                exchange, queue, routingKey);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));

            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };
            
            if (message.CorrelationId.HasValue)
            {
                properties.CorrelationId = message.CorrelationId.Value.ToString();
            }

            await channel.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: ct);

            _logger.LogDebug(
                "Published message to exchange {Exchange} with routing key {RoutingKey}",
                exchange, routingKey);
        }

        private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken ct)
        {
            if (_connection != null && _connection.IsOpen)
            {
                return _connection;
            }

            await _connectionLock.WaitAsync(ct);
            try
            {
                if (_connection != null && _connection.IsOpen)
                {
                    return _connection;
                }

                if (_connection != null)
                {
                    try
                    {
                        await _connection.CloseAsync(ct);
                        _connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error closing existing connection");
                    }
                    _connection = null;
                }

                _logger.LogDebug("Creating new RabbitMQ connection to {Host}:{Port}", _options.Host, _options.Port);
                
                var factory = new ConnectionFactory
                {
                    HostName = _options.Host,
                    Port = _options.Port,
                    UserName = _options.Username,
                    Password = _options.Password,
                    VirtualHost = _options.VHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat = TimeSpan.FromSeconds(60),
                    RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
                };

                _connection = await factory.CreateConnectionAsync($"fcg-payments-publisher-{Environment.MachineName}", ct);
                
                _logger.LogInformation(
                    "RabbitMQ connection established to {Host}:{Port} (VHost: {VHost})",
                    _options.Host, _options.Port, _options.VHost);

                return _connection;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task InvalidateConnectionAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                if (_connection != null)
                {
                    try
                    {
                        await _connection.CloseAsync();
                        _connection.Dispose();
                        _logger.LogDebug("Invalidated RabbitMQ connection");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error invalidating connection");
                    }
                    finally
                    {
                        _connection = null;
                    }
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _connectionLock.WaitAsync();
            try
            {
                if (_connection != null)
                {
                    await _connection.CloseAsync();
                    _connection.Dispose();
                    _logger.LogInformation("RabbitMQ connection disposed");
                }

                _disposed = true;
            }
            finally
            {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
        }
    }
}
