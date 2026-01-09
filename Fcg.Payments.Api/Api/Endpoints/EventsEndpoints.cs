using Fcg.Payments.Api.Infra.Events;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Fcg.Payments.Api.Api.Endpoints
{
    public static class EventsEndpoints
    {
        public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder app)
        {
            var g = app.MapGroup("/events").WithTags("EventStore");

            g.MapGet(
                "/{aggregateId:guid}",
                async (Guid aggregateId, IEventStore store, CancellationToken ct) =>
                {
                    var events = await store.GetByAggregateIdAsync(aggregateId, ct);
                    var dto = events.Select(e => new
                    {
                        e.EventId,
                        e.AggregateId,
                        e.EventType,
                        e.OccurredAt,
                        e.Version,
                        e.CorrelationId,
                        e.Payload
                    });

                    return TypedResults.Ok(dto);
                });

            return app;
        }
    }
}
