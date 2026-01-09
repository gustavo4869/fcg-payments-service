using Fcg.Payments.Api.Domain.Entidades;

namespace Fcg.Payments.Api.Domain.Repositorio
{
    public interface IPagamentoRepository
    {
        Task AddAsync(Pagamento p, CancellationToken ct);
        Task<Pagamento?> GetByIdAsync(Guid id, CancellationToken ct);
        Task UpdateAsync(Pagamento p, CancellationToken ct);
    }
}
