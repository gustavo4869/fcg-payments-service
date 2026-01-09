using Fcg.Payments.Api.Domain.Enum;
using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fcg.Payments.Api.Infra.HostedServices
{
    public class PaymentProcessorHostedService : BackgroundService
    {
        private readonly ILogger<PaymentProcessorHostedService> _logger;
        private readonly IServiceProvider _provider;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
        private readonly Random _rnd = new();

        public PaymentProcessorHostedService(ILogger<PaymentProcessorHostedService> logger, IServiceProvider provider)
        {
            _logger = logger;
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Payment processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _provider.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IPagamentoRepository>();
                    var eventStore = scope.ServiceProvider.GetRequiredService<IEventStore>();

                    var pendings = await repo.GetPendingAsync(stoppingToken);
                    foreach (var p in pendings)
                    {
                        // simulate processing time
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

                        var success = _rnd.NextDouble() > 0.3; // 70% success

                        if (success)
                        {
                            p.MarcarSucesso();
                        }
                        else
                        {
                            p.MarcarFalha();
                        }

                        await repo.UpdateAsync(p, stoppingToken);

                        var payload = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            paymentId = p.Id,
                            userId = p.UserId,
                            gameId = p.GameId,
                            amount = p.Amount,
                            status = p.Status.ToString(),
                            occurredAt = DateTime.UtcNow
                        });

                        await eventStore.AppendAsync(p.Id, success ? "PaymentSucceeded" : "PaymentFailed", payload, correlationId: null, ct: stoppingToken);

                        _logger.LogInformation("Processed payment {PaymentId} result={Status}", p.Id, p.Status);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing payments");
                }

                await Task.Delay(_interval, stoppingToken);
            }

            _logger.LogInformation("Payment processor stopping");
        }
    }
}
