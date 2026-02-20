using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;

public sealed class PaymentQueueWorker : BackgroundService
{
    private readonly ILogger<PaymentQueueWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _cfg;
    private readonly Random _rnd = new();

    private IConnection? _conn;
    private IChannel? _ch;

    public PaymentQueueWorker(
        ILogger<PaymentQueueWorker> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration cfg)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Payment Queue Worker is starting...");

        var queue = _cfg["PaymentQueueName"] ?? "payment.pending";
        var connStr = _cfg["RabbitMqConnection"] ?? throw new InvalidOperationException("RabbitMqConnection missing");

        try
        {
            var factory = new ConnectionFactory
            {
                Uri = new Uri(connStr),
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };

            // 1. Conectar ao RabbitMQ com retry
            _logger.LogInformation("Connecting to RabbitMQ...");
            _conn = await CreateConnectionWithRetryAsync(factory, stoppingToken);
            _ch = await _conn.CreateChannelAsync();

            // 2. Declarar a fila (cria se não existir)
            _logger.LogInformation("Declaring queue '{QueueName}'...", queue);
            try
            {
                await _ch.QueueDeclareAsync(
                    queue: queue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                
                _logger.LogInformation("Queue '{QueueName}' is ready (created or already exists)", queue);
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
            {
                _logger.LogError(ex, "Queue '{QueueName}' does not exist and could not be created", queue);
                throw;
            }
            catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 406)
            {
                _logger.LogError(ex, 
                    "Queue '{QueueName}' exists with different configuration. " +
                    "Please delete the queue manually or update the configuration.", 
                    queue);
                throw;
            }

            // 3. Configurar QoS
            await _ch.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);
            _logger.LogInformation("QoS configured: prefetchCount=1");

            // 4. Criar consumidor
            var consumer = new AsyncEventingBasicConsumer(_ch);
            consumer.ReceivedAsync += async (_, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    await ProcessMessageAsync(message, stoppingToken);
                    await _ch.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Invalid JSON. Nack without requeue.");
                    await _ch.BasicNackAsync(ea.DeliveryTag, false, requeue: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Processing error. Nack with requeue.");
                    await _ch.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
                }
            };

            // 5. Começar a consumir
            var consumerTag = await _ch.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
            _logger.LogInformation(
                "Payment Queue Worker is now consuming from '{QueueName}' (tag: {ConsumerTag})", 
                queue, consumerTag);

            // 6. Manter worker vivo
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Payment Queue Worker is stopping gracefully...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Payment Queue Worker encountered a fatal error.");
            throw;
        }
        finally
        {
            await DisposeResourcesAsync();
            _logger.LogInformation("Payment Queue Worker stopped.");
        }
    }

    private async Task ProcessMessageAsync(string message, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<PaymentPendingMessage>(message, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (request == null || request.PaymentId == Guid.Empty)
        {
            _logger.LogWarning("Invalid message format: {Message}", message);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
        var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var idempotencyKey = $"payment-processed:{request.PaymentId}";
        var existing = await eventStore.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing != null)
        {
            _logger.LogInformation("Payment {PaymentId} already processed. Skipping.", request.PaymentId);
            return;
        }

        var payment = await repo.GetByIdAsync(request.PaymentId, ct);
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found", request.PaymentId);
            return;
        }

        var success = _rnd.NextDouble() > 0.3;
        if (success) payment.MarcarSucesso();
        else payment.MarcarFalha();

        await repo.UpdateAsync(payment, ct);

        var payload = JsonSerializer.Serialize(new
        {
            paymentId = payment.Id,
            userId = payment.UserId,
            gameId = payment.GameId,
            amount = payment.Amount,
            status = payment.Status.ToString(),
            occurredAt = DateTime.UtcNow
        });

        await eventStore.AppendAsync(
            payment.Id,
            success ? "PaymentSucceeded" : "PaymentFailed",
            payload,
            idempotencyKey,
            ct);

        _logger.LogInformation("Payment {PaymentId} -> {Status}", payment.Id, payment.Status);
    }

    private async Task<IConnection> CreateConnectionWithRetryAsync(
        ConnectionFactory factory,
        CancellationToken cancellationToken,
        int maxRetries = 10,
        int initialDelaySeconds = 2)
    {
        int attempt = 0;
        
        while (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                var connection = await factory.CreateConnectionAsync(cancellationToken);
                _logger.LogInformation(
                    "Successfully connected to RabbitMQ on attempt {Attempt}/{MaxRetries}", 
                    attempt, maxRetries);
                return connection;
            }
            catch (BrokerUnreachableException ex)
            {
                var delaySeconds = initialDelaySeconds * Math.Pow(2, attempt - 1);
                
                _logger.LogWarning(ex, 
                    "Failed to connect to RabbitMQ. Attempt {Attempt}/{MaxRetries}. " +
                    "Retrying in {Delay} seconds...", 
                    attempt, maxRetries, delaySeconds);
                
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
            }
            catch (AuthenticationFailureException ex)
            {
                _logger.LogError(ex, 
                    "Authentication failed with RabbitMQ. Please check credentials.");
                throw;
            }
        }
        
        throw new Exception($"Failed to connect to RabbitMQ after {maxRetries} attempts");
    }

    private async Task DisposeResourcesAsync()
    {
        if (_ch != null)
        {
            try
            {
                await _ch.CloseAsync();
                _logger.LogDebug("Channel closed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing channel");
            }
        }

        if (_conn != null)
        {
            try
            {
                await _conn.CloseAsync();
                _logger.LogDebug("Connection closed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing connection");
            }
        }
    }

    public override void Dispose()
    {
        try { _ch?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        try { _conn?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        base.Dispose();
    }

    private sealed record PaymentPendingMessage(Guid PaymentId, Guid UserId, Guid GameId, decimal Amount);
}