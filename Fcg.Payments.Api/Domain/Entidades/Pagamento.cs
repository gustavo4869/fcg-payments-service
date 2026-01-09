using Fcg.Payments.Api.Domain.Enum;

namespace Fcg.Payments.Api.Domain.Entidades
{
    public class Pagamento
    {
        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid UserId { get; private set; }
        public Guid GameId { get; private set; }
        public decimal Amount { get; private set; }
        public PagamentoStatusEnum Status { get; private set; } = PagamentoStatusEnum.Requested;
        public DateTime DataCriacao { get; private set; } = DateTime.UtcNow;

        protected Pagamento() { }

        public Pagamento(Guid userId, Guid gameId, decimal amount)
        {
            UserId = userId;
            GameId = gameId;
            Amount = amount;
            Status = PagamentoStatusEnum.Requested;
            DataCriacao = DateTime.UtcNow;
        }

        public void MarcarSucesso() => Status = PagamentoStatusEnum.Succeeded;
        public void MarcarFalha() => Status = PagamentoStatusEnum.Failed;
    }
}
