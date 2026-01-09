using Fcg.Payments.Api.Application.Pagamentos;
using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra;
using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Infra.Repositorio;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fcg.Payments.Api.Api.Setup
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApiCore(this IServiceCollection services, IConfiguration cfg)
        {
            var connectionString = cfg.GetConnectionString("DefaultConnection") ?? "Data Source=fcg.db";

            services.AddDbContext<PagamentoDbContext>(o => o.UseSqlite(connectionString));

            services.AddScoped<IPagamentoRepository, PagamentoRepository>();
            services.AddScoped<IEventStore, EfEventStore>();
            services.AddValidatorsFromAssemblyContaining<CriarPagamentoValidator>();

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddHealthChecks()
                .AddDbContextCheck<PagamentoDbContext>("efcore-db", failureStatus: HealthStatus.Unhealthy);

            return services;
        }
    }
}
