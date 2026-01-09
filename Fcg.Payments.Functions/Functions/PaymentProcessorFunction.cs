using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Fcg.Payments.Functions.Functions
{
    public class PaymentProcessorFunction
    {
        private readonly ILogger<PaymentProcessorFunction> _logger;
        private readonly IPagamentoRepository _repo;
        private readonly IEventStore _eventStore;
        private readonly Random _rnd = new();

        public PaymentProcessorFunction(ILoggerFactory loggerFactory, IPagamentoRepository repo, IEventStore eventStore)
        {
            _logger = loggerFactory.CreateLogger<PaymentProcessorFunction>();
            _repo = repo;
            _eventStore = eventStore;
        }

        // Timer trigger every 10 seconds
        [Function("PaymentProcessorFunction")]
        public async Task Run([TimerTrigger("*/10 * * * * *")] TimerInfo timer, FunctionContext ctx)
        {
            _logger.LogInformation("PaymentProcessorFunction running at: {Now}", DateTime.UtcNow);

            var pendings = await _repo.GetPendingAsync(CancellationToken.None);
            foreach (var p in pendings)
            {
                var success = _rnd.NextDouble() > 0.3;
                if (success) p.MarcarSucesso(); else p.MarcarFalha();

                await _repo.UpdateAsync(p, CancellationToken.None);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    paymentId = p.Id,
                    userId = p.UserId,
                    gameId = p.GameId,
                    amount = p.Amount,
                    status = p.Status.ToString(),
                    occurredAt = DateTime.UtcNow
                });

                await _eventStore.AppendAsync(p.Id, success ? "PaymentSucceeded" : "PaymentFailed", payload, null, CancellationToken.None);

                _logger.LogInformation("Processed payment {PaymentId} result={Status}", p.Id, p.Status);
            }
        }
    }
}
