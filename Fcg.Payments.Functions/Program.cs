using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Fcg.Payments.Api.Infra;
using Fcg.Payments.Api.Infra.Repositorio;
using Fcg.Payments.Api.Infra.Events;
using Fcg.Payments.Api.Domain.Repositorio;
using System.IO;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration(cb => cb.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true))
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;
        var conn = cfg.GetConnectionString("DefaultConnection") ?? cfg["ConnectionStrings:DefaultConnection"] ?? "Data Source=fcg.db";

        // Resolve relative Data Source path to absolute so Functions can open the sqlite file
        // Expected format: "Data Source=path/to/file.db" (case-insensitive)
        var lower = conn.ToLowerInvariant();
        if (lower.StartsWith("data source="))
        {
            var parts = conn.Split('=', 2);
            if (parts.Length == 2)
            {
                var ds = parts[1].Trim().Trim('"');
                // if relative path, make it absolute based on application base directory
                if (!Path.IsPathRooted(ds))
                {
                    var baseDir = AppContext.BaseDirectory; // functions worker base dir
                    var full = Path.GetFullPath(Path.Combine(baseDir, ds));
                    var dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    conn = $"Data Source={full}";
                }
                else
                {
                    // ensure directory exists
                    var dir = Path.GetDirectoryName(ds);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                }
            }
        }

        services.AddDbContext<PagamentoDbContext>(o => o.UseSqlite(conn));

        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IEventStore, EfEventStore>();

        // ensure database exists / apply migrations on startup
        using var sp = services.BuildServiceProvider();
        try
        {
            var db = sp.GetRequiredService<PagamentoDbContext>();
            db.Database.Migrate();
        }
        catch (Exception ex)
        {
            // can't use ILogger here; write to console so Azure Functions host shows it
            Console.WriteLine($"Error applying migrations in Functions host: {ex}");
        }
    })
    .Build();

host.Run();
