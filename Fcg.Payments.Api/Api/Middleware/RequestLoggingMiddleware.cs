using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Fcg.Payments.Api.Api.Middleware
{
    public sealed class RequestLoggingMiddleware : IMiddleware
    {
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        private const int MaxBodyBytesToLog = 4096;
        private static readonly string[] SensitiveKeys = ["senha", "password", "token"];

        public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
            => _logger = logger;

        public async Task InvokeAsync(HttpContext ctx, RequestDelegate next)
        {
            var correlationId = ctx.Request.Headers.TryGetValue("X-Correlation-ID", out var cid) && !string.IsNullOrWhiteSpace(cid)
                ? cid.ToString()
                : Guid.NewGuid().ToString("n");
            ctx.Response.Headers["X-Correlation-ID"] = correlationId;

            var userId = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId ?? "",
                ["Role"] = role ?? ""
            });


            string? bodyPreview = null;
            if (ShouldLogBody(ctx.Request))
            {
                bodyPreview = await ReadBodyPreviewAsync(ctx.Request);
                bodyPreview = MaskSensitiveJson(bodyPreview);
            }

            var sw = Stopwatch.StartNew();
            var path = ctx.Request.Path.ToString();
            var method = ctx.Request.Method;
            var ua = ctx.Request.Headers.UserAgent.ToString();
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            int status = 500;

            try
            {
                await next(ctx);
                status = ctx.Response.StatusCode;
            }
            catch
            {
                status = 500;
                throw;
            }
            finally
            {
                sw.Stop();
                var level = status >= 500 ? LogLevel.Error :
                            status >= 400 ? LogLevel.Warning : LogLevel.Information;

                if (bodyPreview is null)
                {
                    _logger.Log(level,
                        "HTTP {Method} {Path} -> {Status} in {ElapsedMs} ms | UA={UserAgent} IP={IP} CorrelationId={CorrelationId} UserId={UserId} Role={Role}",
                        method, path, status, sw.Elapsed.TotalMilliseconds, ua, ip, correlationId, userId, role);
                }
                else
                {
                    _logger.Log(level,
                        "HTTP {Method} {Path} -> {Status} in {ElapsedMs} ms | UA={UserAgent} IP={IP} CorrelationId={CorrelationId} UserId={UserId} Role={Role} | Body={Body}",
                        method, path, status, sw.Elapsed.TotalMilliseconds, ua, ip, correlationId, userId, role, bodyPreview);
                }
            }
        }

        private static bool ShouldLogBody(HttpRequest req)
        {
            if (!string.Equals(req.Method, "POST", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(req.Method, "PUT", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(req.Method, "PATCH", StringComparison.OrdinalIgnoreCase))
                return false;

            if (req.Path.HasValue && req.Path.Value!.Contains("/auth/login", StringComparison.OrdinalIgnoreCase))
                return false;

            return req.ContentLength is > 0
                && string.Equals(req.ContentType, "application/json", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string?> ReadBodyPreviewAsync(HttpRequest req)
        {
            req.EnableBuffering();
            var len = (int)Math.Min(req.ContentLength ?? 0, MaxBodyBytesToLog);
            if (len <= 0) { req.Body.Position = 0; return null; }

            using var ms = new MemoryStream(capacity: len);
            await req.Body.CopyToAsync(ms);
            var bytes = ms.ToArray();
            req.Body.Position = 0;

            var text = Encoding.UTF8.GetString(bytes);
            return text;
        }

        private static string? MaskSensitiveJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var masked = MaskElement(doc.RootElement);
                return JsonSerializer.Serialize(masked);
            }
            catch
            {
                return json;
            }

            static object? MaskElement(JsonElement el)
            {
                return el.ValueKind switch
                {
                    JsonValueKind.Object => el.EnumerateObject()
                        .ToDictionary(
                            p => p.Name,
                            p => SensitiveKeys.Any(k => string.Equals(k, p.Name, StringComparison.OrdinalIgnoreCase))
                                    ? (object)"***"
                                    : MaskElement(p.Value) ?? null),
                    JsonValueKind.Array => el.EnumerateArray().Select(MaskElement).ToArray(),
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }
        }
    }
}
