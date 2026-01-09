using FluentValidation;

namespace Fcg.Payments.Api.Application.Pagamentos
{
    public sealed class CriarPagamentoValidator : AbstractValidator<CriarPagamentoRequest>
    {
        public CriarPagamentoValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.GameId).NotEmpty();
            RuleFor(x => x.Amount).GreaterThan(0);
        }
    }
}
