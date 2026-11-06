using System.Text.Json;
using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Exceptions;

namespace FurnaceMonitor.Api.Middleware;

/// <summary>
/// Centralised exception handler that maps <see cref="AppException"/> subclasses
/// to their HTTP status codes and logs unexpected failures.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            _logger.LogWarning("Handled application error {StatusCode}: {Message}", ex.StatusCode, ex.Message);
            await WriteResponseAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during request {Path}", context.Request.Path);
            await WriteResponseAsync(context, 500, "An unexpected error occurred. Please try again later.");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        var payload = JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions);
        await context.Response.WriteAsync(payload);
    }
}
