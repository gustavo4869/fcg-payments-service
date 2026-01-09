namespace Fcg.Payments.Api.Infra.Events
{
    public sealed class EventEntity
    {
        public Guid EventId { get; set; }
        public Guid AggregateId { get; set; }
        public string EventType { get; set; } = default!;
        public DateTime OccurredAt { get; set; }
        public int Version { get; set; }
        public Guid? CorrelationId { get; set; }
        public string Payload { get; set; } = default!;
    }
}
