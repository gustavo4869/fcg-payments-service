using Fcg.Payments.Api.Api.Endpoints;
using Fcg.Payments.Api.Infra;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Fcg.Payments.Api.Api.Setup
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseApiCore(this WebApplication app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(opt =>
            {
                opt.SwaggerEndpoint("/swagger/v1/swagger.json", "FCG Payments v1");
                opt.DisplayRequestDuration();
            });

            ApplyMigrationsAndSeed(app);

            app.MapHealthChecks("/health");

            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = _ => true,
                ResponseWriter = async (ctx, report) =>
                {
                    ctx.Response.ContentType = "application/json";
                    var payload = new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(kvp => new {
                            name = kvp.Key,
                            status = kvp.Value.Status.ToString(),
                            error = kvp.Value.Exception?.Message
                        })
                    };
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
                }
            });

            return app;
        }

        public static WebApplication MapV1Endpoints(this WebApplication app)
        {
            app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

            var api = app.MapGroup("/api/v1");
            api.MapPagamentosEndpoints();
            api.MapEventsEndpoints();
            return app;
        }

        private static void ApplyMigrationsAndSeed(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PagamentoDbContext>();

            db.Database.Migrate();
        }
    }
}
