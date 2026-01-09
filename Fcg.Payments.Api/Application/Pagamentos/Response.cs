namespace Fcg.Payments.Api.Application.Pagamentos
{
    public sealed record PagamentoResponse(Guid Id, Guid UserId, Guid GameId, decimal Amount, string Status, DateTime DataCriacao);
}
