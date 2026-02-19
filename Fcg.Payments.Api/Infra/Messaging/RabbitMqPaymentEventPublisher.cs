using Fcg.Payments.Api.Domain.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Fcg.Payments.Api.Infra.Messaging
{
    public class RabbitMqPaymentEventPublisher : IPaymentEventPublisher, IAsyncDisposable
    {
        private readonly ILogger<RabbitMqPaymentEventPublisher> _logger;
        private readonly MessagingOptions _options;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private IConnection? _connection;
        private bool _disposed;

        public RabbitMqPaymentEventPublisher(
            ILogger<RabbitMqPaymentEventPublisher> logger,
            IOptions<MessagingOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public async Task PublishPaymentProcessedAsync(PaymentProcessedMessage message, CancellationToken ct = default)
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
                        "Published payment processed event for {PaymentId} with status {Status} (attempt {Attempt})",
                        message.PaymentId, message.Status, attempt);
                    
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish payment event for {PaymentId} (attempt {Attempt}/{MaxRetries}). Retrying...",
                        message.PaymentId, attempt, maxRetries);
                    
                    // Invalidate connection on error so next attempt creates a new one
                    await InvalidateConnectionAsync();
                    
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to publish payment event for {PaymentId} after {MaxRetries} attempts. Event will NOT be published.",
                        message.PaymentId, maxRetries);
                    
                    // Invalidate connection for next time
                    await InvalidateConnectionAsync();
                    
                    // Don't throw - we don't want to break the processing flow
                    return;
                }
            }
        }

        private async Task PublishInternalAsync(PaymentProcessedMessage message, CancellationToken ct)
        {
            var connection = await GetOrCreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            
            // Declare exchange (idempotent operation)
            await channel.ExchangeDeclareAsync(
                exchange: _options.Exchange,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            // Declare queue (idempotent operation)
            await channel.QueueDeclareAsync(
                queue: _options.Queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: ct);

            // Bind queue to exchange with routing key (idempotent operation)
            await channel.QueueBindAsync(
                queue: _options.Queue,
                exchange: _options.Exchange,
                routingKey: _options.RoutingKey,
                cancellationToken: ct);

            _logger.LogDebug(
                "Ensured infrastructure: Exchange={Exchange}, Queue={Queue}, RoutingKey={RoutingKey}",
                _options.Exchange, _options.Queue, _options.RoutingKey);

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
                exchange: _options.Exchange,
                routingKey: _options.RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: body,
                cancellationToken: ct);

            _logger.LogDebug(
                "Published message to exchange {Exchange} with routing key {RoutingKey}",
                _options.Exchange, _options.RoutingKey);
        }

        private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken ct)
        {
            // Fast path: connection already exists and is open
            if (_connection != null && _connection.IsOpen)
            {
                return _connection;
            }

            // Slow path: need to create or recreate connection
            await _connectionLock.WaitAsync(ct);
            try
            {
                // Double-check pattern
                if (_connection != null && _connection.IsOpen)
                {
                    return _connection;
                }

                // Close existing connection if any
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

                // Create new connection
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

    public class MessagingOptions
    {
        public const string SectionName = "Messaging";

        public bool Enabled { get; set; }
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string VHost { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public string RoutingKey { get; set; } = string.Empty;
        public string Queue { get; set; } = string.Empty;
    }
}



