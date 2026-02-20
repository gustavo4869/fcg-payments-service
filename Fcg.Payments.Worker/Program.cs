using Fcg.Payments.Api.Domain.Repositorio;
using Fcg.Payments.Api.Infra;
using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Infra.Repositorio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<PaymentQueueWorker>();

var conn = builder.Configuration.GetConnectionString("DefaultConnection") ?? builder.Configuration["ConnectionStrings:DefaultConnection"] ?? "Data Source=fcg.db";
var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "SQLite";

builder.Services.AddDbContext<PagamentoDbContext>(options =>
{
    if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(conn);
        Console.WriteLine("[INFO] Worker using PostgreSQL database provider");
    }
    else
    {
        options.UseSqlite(conn);
        Console.WriteLine("[INFO] Worker using SQLite database provider");
    }
});

builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();
builder.Services.AddScoped<IEventStore, EfEventStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<PagamentoDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations in Worker: {ex}");
    }
}

app.Run();