using Fcg.Payments.Api.Domain.Messaging;
using Microsoft.Extensions.Logging;

namespace Fcg.Payments.Api.Infra.Messaging
{
    /// <summary>
    /// No-op implementation when messaging is disabled via feature flag
    /// </summary>
    public class NoOpPaymentEventPublisher : IPaymentEventPublisher
    {
        private readonly ILogger<NoOpPaymentEventPublisher> _logger;

        public NoOpPaymentEventPublisher(ILogger<NoOpPaymentEventPublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishPaymentProcessedAsync(PaymentProcessedMessage message, CancellationToken ct = default)
        {
            _logger.LogDebug("Messaging disabled - skipping publish for payment {PaymentId}", message.PaymentId);
            return Task.CompletedTask;
        }
    }
}
