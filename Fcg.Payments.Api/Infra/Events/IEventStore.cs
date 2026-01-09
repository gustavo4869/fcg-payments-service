namespace Fcg.Payments.Api.Infra.Events
{
    public interface IEventStore
    {
        Task AppendAsync(Guid aggregateId, string eventType, string payloadJson, Guid? correlationId, CancellationToken ct);
        Task<IReadOnlyList<EventEntity>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken ct);
    }
}
