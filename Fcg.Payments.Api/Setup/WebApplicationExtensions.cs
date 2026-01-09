using Fcg.Payments.Api.Api.Endpoints;
using Fcg.Payments.Api.Api.Middleware;
using Fcg.Payments.Api.Infra;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace Fcg.Payments.Api.Setup
{
    public static class WebApplicationExtensions
    {
        public static WebApplication UseApiCore(this WebApplication app)
        {
            app.UseMiddleware<ErrorMiddleware>();
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseSwagger(c =>
            {
                c.PreSerializeFilters.Add((swagger, httpReq) =>
                {
                    var prefix = httpReq.Headers["X-Forwarded-Prefix"].FirstOrDefault();

                    // Quando vem via gateway, prefix será "/games" ou "/users" etc.
                    // Quando acessa direto o container (interno), prefix estará vazio.
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        swagger.Servers = new List<OpenApiServer>
                        {
                            new() { Url = prefix } // URL relativa ao host atual (gateway)
                        };
                    }
                    else
                    {
                        // Acesso direto (sem gateway): mantém raiz
                        swagger.Servers = new List<OpenApiServer>
                        {
                            new() { Url = "/" }
                        };
                    }
                });
            });
            app.UseSwaggerUI(opt =>
            {
                opt.SwaggerEndpoint("v1/swagger.json", "FIAP Cloud Games v1");
                opt.DisplayRequestDuration();
            });

            app.UseAuthentication();
            app.UseAuthorization();

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
