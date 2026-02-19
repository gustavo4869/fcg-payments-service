using Fcg.Payments.Api.Domain.Messaging;
using Microsoft.Extensions.Logging;

namespace Fcg.Payments.Api.Infra.Messaging
{
    public class NoOpPaymentRequestPublisher : IPaymentRequestPublisher
    {
        private readonly ILogger<NoOpPaymentRequestPublisher> _logger;

        public NoOpPaymentRequestPublisher(ILogger<NoOpPaymentRequestPublisher> logger)
        {
            _logger = logger;
        }

        public Task PublishPaymentPendingAsync(PaymentPendingMessage message, CancellationToken ct = default)
        {
            _logger.LogDebug(
                "NoOp: Would publish payment.pending for {PaymentId} (messaging disabled)",
                message.PaymentId);
            
            return Task.CompletedTask;
        }
    }
}
