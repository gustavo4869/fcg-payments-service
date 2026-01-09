using Microsoft.EntityFrameworkCore;

namespace Fcg.Payments.Api.Infra.Events
{
    public sealed class EfEventStore : IEventStore
    {
        private readonly PagamentoDbContext _db;

        public EfEventStore(PagamentoDbContext db) => _db = db;

        public async Task AppendAsync(Guid aggregateId, string eventType, string payloadJson, Guid? correlationId, CancellationToken ct)
        {
            var lastVersion = await _db.Events
                .Where(e => e.AggregateId == aggregateId)
                .MaxAsync(e => (int?)e.Version, ct);

            var nextVersion = (lastVersion ?? 0) + 1;

            _db.Events.Add(new EventEntity
            {
                EventId = Guid.NewGuid(),
                AggregateId = aggregateId,
                EventType = eventType,
                OccurredAt = DateTime.UtcNow,
                Version = nextVersion,
                CorrelationId = correlationId,
                Payload = payloadJson
            });

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<EventEntity>> GetByAggregateIdAsync(Guid aggregateId, CancellationToken ct)
        {
            return await _db.Events
                .Where(e => e.AggregateId == aggregateId)
                .OrderBy(e => e.Version)
                .ToListAsync(ct);
        }
    }
}
