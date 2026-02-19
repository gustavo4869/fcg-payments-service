namespace Fcg.Payments.Api.Domain.Messaging
{
    public interface IPaymentEventPublisher
    {
        Task PublishPaymentProcessedAsync(PaymentProcessedMessage message, CancellationToken ct = default);
    }
    
    public sealed record PaymentProcessedMessage(
        Guid PaymentId,
        Guid? OrderId,
        Guid UserId,
        Guid GameId,
        string Status,
        decimal Amount,
        string Currency,
        DateTime ProcessedAt,
        Guid? CorrelationId
    );
}
