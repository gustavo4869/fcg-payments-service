using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
        var queue = _cfg["PaymentQueueName"] ?? "payment.pending";
        var connStr = _cfg["RabbitMqConnection"] ?? throw new InvalidOperationException("RabbitMqConnection missing");

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connStr),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };

        _conn = await factory.CreateConnectionAsync();
        _ch = await _conn.CreateChannelAsync();
        await _ch.BasicQosAsync(0, 10, false);

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

        await _ch.BasicConsumeAsync(queue: queue, autoAck: false, consumer: consumer);
        _logger.LogInformation("Worker consuming queue {Queue}", queue);

        await Task.Delay(Timeout.Infinite, stoppingToken);
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

    public override void Dispose()
    {
        try { _ch?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        try { _conn?.CloseAsync().GetAwaiter().GetResult(); } catch { }
        base.Dispose();
    }

    private sealed record PaymentPendingMessage(Guid PaymentId, Guid UserId, Guid GameId, decimal Amount);
}