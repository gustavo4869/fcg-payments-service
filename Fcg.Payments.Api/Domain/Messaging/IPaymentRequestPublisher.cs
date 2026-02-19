namespace Fcg.Payments.Api.Domain.Messaging
{
    public interface IPaymentRequestPublisher
    {
        Task PublishPaymentPendingAsync(PaymentPendingMessage message, CancellationToken ct = default);
    }
    
    public sealed record PaymentPendingMessage(
        Guid PaymentId,
        Guid UserId,
        Guid GameId,
        decimal Amount,
        Guid? CorrelationId
    );
}
