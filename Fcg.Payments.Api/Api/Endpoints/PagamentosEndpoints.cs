using Fcg.Payments.Api.Application.Pagamentos;
using Fcg.Payments.Api.Domain.Entidades;
using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using System.Linq;

namespace Fcg.Payments.Api.Api.Endpoints
{
    public static class PagamentosEndpoints
    {
        public static IEndpointRouteBuilder MapPagamentosEndpoints(this IEndpointRouteBuilder app)
        {
            var g = app.MapGroup("/payments").WithTags("Payments");

            g.MapPost("",
                async (
                    CriarPagamentoRequest req,
                    IPagamentoRepository repo,
                    IEventStore eventStore,
                    IValidator<CriarPagamentoRequest> validator,
                    HttpContext http,
                    CancellationToken ct) =>
                {
                    var validationResult = await validator.ValidateAsync(req, ct);
                    if (!validationResult.IsValid)
                    {
                        return Results.ValidationProblem(validationResult.ToDictionary());
                    }

                    var correlationId = http.Request.Headers.TryGetValue("X-Correlation-ID", out var v) && Guid.TryParse(v, out var cid)
                        ? cid
                        : (Guid?)null;

                    var p = new Pagamento(req.UserId, req.GameId, req.Amount);
                    await repo.AddAsync(p, ct);

                    var payload = JsonSerializer.Serialize(new
                    {
                        paymentId = p.Id,
                        userId = p.UserId,
                        gameId = p.GameId,
                        amount = p.Amount,
                        status = p.Status.ToString(),
                        occurredAt = DateTime.UtcNow
                    });

                    await eventStore.AppendAsync(
                        aggregateId: p.Id,
                        eventType: "PaymentRequested",
                        payloadJson: payload,
                        correlationId: correlationId,
                        ct: ct);

                    return Results.Created($"/api/v1/payments/{p.Id}",
                        new PagamentoResponse(p.Id, p.UserId, p.GameId, p.Amount, p.Status.ToString(), p.DataCriacao));
                })
                .Accepts<CriarPagamentoRequest>("application/json")
                .Produces<PagamentoResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem(StatusCodes.Status400BadRequest)
                .RequireAuthorization();

            g.MapGet("/{id:guid}",
                async Task<Results<Ok<PagamentoResponse>, NotFound>> (
                    Guid id, IPagamentoRepository repo, CancellationToken ct) =>
                {
                    var p = await repo.GetByIdAsync(id, ct);
                    if (p is null) return TypedResults.NotFound();

                    return TypedResults.Ok(new PagamentoResponse(p.Id, p.UserId, p.GameId, p.Amount, p.Status.ToString(), p.DataCriacao));
                })
                .Produces<PagamentoResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .AllowAnonymous();

            g.MapGet("/by-user/{userId:guid}",
                async (Guid userId, IPagamentoRepository repo, CancellationToken ct) =>
                {
                    var list = await repo.GetByUserIdAsync(userId, ct);
                    var dto = list.Select(p => new PagamentoResponse(p.Id, p.UserId, p.GameId, p.Amount, p.Status.ToString(), p.DataCriacao)).ToList();
                    return Results.Ok(dto);
                })
                .Produces<List<PagamentoResponse>>(StatusCodes.Status200OK)
                .RequireAuthorization();

            return app;
        }
    }
}
