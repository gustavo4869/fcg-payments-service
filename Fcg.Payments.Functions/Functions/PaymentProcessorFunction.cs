using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fcg.Payments.Functions.Functions
{
    /// <summary>
    /// Processa pagamentos pendentes consumindo mensagens da fila payment.pending.
    /// Aplica a mesma lógica de processamento, mas via event-driven ao invés de polling.
    /// </summary>
    public class PaymentProcessorFunction
    {
        private readonly ILogger<PaymentProcessorFunction> _logger;
        private readonly IPagamentoRepository _repo;
        private readonly IEventStore _eventStore;
        private readonly Random _rnd = new();

        public PaymentProcessorFunction(
            ILoggerFactory loggerFactory, 
            IPagamentoRepository repo, 
            IEventStore eventStore)
        {
            _logger = loggerFactory.CreateLogger<PaymentProcessorFunction>();
            _repo = repo;
            _eventStore = eventStore;
        }

        /// <summary>
        /// Consume payment.pending messages from RabbitMQ and process them.
        /// Queue name is configured via PaymentQueueName setting (default: payment.pending)
        /// </summary>
        [Function("PaymentProcessorFunction")]
        public async Task Run(
            [RabbitMQTrigger("%PaymentQueueName%", ConnectionStringSetting = "RabbitMqConnection")] string message,
            FunctionContext context)
        {
            var correlationId = context.BindingContext.BindingData.TryGetValue("CorrelationId", out var corrId)
                ? corrId?.ToString()
                : null;

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId ?? "none"
            });

            _logger.LogInformation(
                "Processing payment from queue. CorrelationId={CorrelationId}",
                correlationId);

            try
            {
                var request = JsonSerializer.Deserialize<PaymentPendingMessage>(message, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (request == null || request.PaymentId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid message format or empty PaymentId. Message: {Message}", message);
                    return;
                }

                _logger.LogInformation(
                    "Processing payment {PaymentId} for User={UserId}, Game={GameId}, Amount={Amount}",
                    request.PaymentId, request.UserId, request.GameId, request.Amount);

                // Check idempotency: se já foi processado, ignorar
                var idempotencyKey = $"payment-processed:{request.PaymentId}";
                var existingEvent = await _eventStore.GetByIdempotencyKeyAsync(idempotencyKey, context.CancellationToken);

                if (existingEvent != null)
                {
                    _logger.LogInformation(
                        "Payment {PaymentId} already processed (idempotent). Skipping.",
                        request.PaymentId);
                    return;
                }

                // Buscar pagamento no banco
                var payment = await _repo.GetByIdAsync(request.PaymentId, context.CancellationToken);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found in database", request.PaymentId);
                    return;
                }

                // Aplicar lógica de processamento (simula gateway de pagamento)
                var success = _rnd.NextDouble() > 0.3; // 70% success rate
                
                if (success)
                {
                    payment.MarcarSucesso();
                    _logger.LogInformation("Payment {PaymentId} processed successfully", request.PaymentId);
                }
                else
                {
                    payment.MarcarFalha();
                    _logger.LogWarning("Payment {PaymentId} failed", request.PaymentId);
                }

                // Atualizar no banco
                await _repo.UpdateAsync(payment, context.CancellationToken);

                // Armazenar evento local
                var eventPayload = JsonSerializer.Serialize(new
                {
                    paymentId = payment.Id,
                    userId = payment.UserId,
                    gameId = payment.GameId,
                    amount = payment.Amount,
                    status = payment.Status.ToString(),
                    occurredAt = DateTime.UtcNow,
                    correlationId
                });

                await _eventStore.AppendAsync(
                    payment.Id,
                    success ? "PaymentSucceeded" : "PaymentFailed",
                    eventPayload,
                    idempotencyKey: idempotencyKey,
                    context.CancellationToken);

                _logger.LogInformation(
                    "Payment {PaymentId} completed with status {Status}",
                    payment.Id, payment.Status);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize message. Message: {Message}", message);
                throw; // Requeue message
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment. Message: {Message}", message);
                throw; // Requeue message
            }
        }

        private sealed record PaymentPendingMessage(
            Guid PaymentId,
            Guid UserId,
            Guid GameId,
            decimal Amount
        );
    }
}

