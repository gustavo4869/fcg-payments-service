using Fcg.Payments.Api.Domain.Entidades;
using Fcg.Payments.Api.Domain.Repositorio;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Payments.Api.Infra.Repositorio
{
    public class PagamentoRepository : IPagamentoRepository
    {
        private readonly PagamentoDbContext _db;
        public PagamentoRepository(PagamentoDbContext db) => _db = db;

        public async Task AddAsync(Pagamento p, CancellationToken ct)
        {
            _db.Pagamentos.Add(p);
            await _db.SaveChangesAsync(ct);
        }

        public Task<Pagamento?> GetByIdAsync(Guid id, CancellationToken ct)
            => _db.Pagamentos.FirstOrDefaultAsync(x => x.Id == id, ct);

        public async Task UpdateAsync(Pagamento p, CancellationToken ct)
        {
            _db.Pagamentos.Update(p);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<Pagamento>> GetPendingAsync(CancellationToken ct)
        {
            return await _db.Pagamentos
                .Where(p => p.Status == Domain.Enum.PagamentoStatusEnum.Requested)
                .OrderBy(p => p.DataCriacao)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Pagamento>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            return await _db.Pagamentos
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.DataCriacao)
                .ToListAsync(ct);
        }
    }
}
