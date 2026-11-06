using System.Diagnostics;
using System.Security.Claims;

namespace FurnaceMonitor.Api.Middleware;

/// <summary>
/// Lightweight request logging that captures method, path, duration and caller.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var userName = context.User?.Identity?.Name ?? "anonymous";
        await _next(context);
        sw.Stop();

        _logger.LogInformation(
            "{Method} {Path} responded {StatusCode} in {Elapsed}ms (user: {User})",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            userName);
    }
}
