using Fcg.Payments.Api.Domain.Entidades;
using Fcg.Payments.Api.Infra.Events;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;

namespace Fcg.Payments.Api.Infra
{
    public class PagamentoDbContext : DbContext
    {
        public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
        public DbSet<EventEntity> Events => Set<EventEntity>();

        public PagamentoDbContext(DbContextOptions<PagamentoDbContext> opts) : base(opts) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pagamento>(b =>
            {
                b.ToTable("Pagamentos");
                b.HasKey(x => x.Id);
                b.Property(x => x.Amount);
                b.Property(x => x.Status).HasConversion<int>();
                b.Property(x => x.DataCriacao);
            });

            modelBuilder.Entity<EventEntity>(b =>
            {
                b.ToTable("Events");
                b.HasKey(x => x.EventId);
                b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
                b.Property(x => x.Payload).IsRequired();
                b.HasIndex(x => x.AggregateId);
                b.HasIndex(x => x.OccurredAt);
            });
        }
    }
}
