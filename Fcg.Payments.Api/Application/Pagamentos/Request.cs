namespace Fcg.Payments.Api.Application.Pagamentos
{
    public sealed record CriarPagamentoRequest(Guid UserId, Guid GameId, decimal Amount);
}
