using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Fcg.Payments.Api.Api.Middleware
{
    public sealed class ErrorMiddleware : IMiddleware
    {
        private readonly ILogger<ErrorMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ErrorMiddleware(ILogger<ErrorMiddleware> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
        {
            try
            {
                await next(ctx);

                if (ctx.Response.StatusCode == StatusCodes.Status404NotFound && !ctx.Response.HasStarted)
                {
                    var problem = CriarProblema(ctx, StatusCodes.Status404NotFound, "Recurso não encontrado");
                    await EscreverProblema(ctx, problem);
                }
            }
            catch (Exception ex)
            {
                var status = ex switch
                {
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    KeyNotFoundException => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status500InternalServerError
                };

                _logger.LogError(ex, "Unhandled exception (status {Status}). TraceId: {TraceId}", status, ctx.TraceIdentifier);

                var title = status switch
                {
                    401 => "Não autorizado",
                    404 => "Recurso não encontrado",
                    _ => "Erro interno"
                };

                var problem = CriarProblema(ctx, status, title, _env.IsDevelopment() ? ex.Message : null);
                await EscreverProblema(ctx, problem);
            }
        }

        private static ProblemDetails CriarProblema(HttpContext ctx, int status, string title, string? detail = null)
        {
            var p = new ProblemDetails
            {
                Type = "about:blank",
                Title = title,
                Detail = detail,
                Status = status,
                Instance = ctx.Request.Path
            };
            p.Extensions["traceId"] = ctx.TraceIdentifier;
            return p;
        }

        private static Task EscreverProblema(HttpContext ctx, ProblemDetails problem)
        {
            ctx.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "application/problem+json";
            var json = JsonSerializer.Serialize(problem);
            return ctx.Response.WriteAsync(json);
        }
    }
}
