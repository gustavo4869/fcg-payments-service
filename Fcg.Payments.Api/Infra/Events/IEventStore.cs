namespace Fcg.Payments.Api.Infra.Events
{
    public interface IEventStore
    {
        Task AppendAsync(Guid aggregateId, string eventType, string payloadJson, string? idempotencyKey, CancellationToken ct);
        Task<IReadOnlyList<EventEntity>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken ct);
        Task<EventEntity?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
    }
}
