using System.Text.RegularExpressions;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Middleware that adds correlation ID to requests for distributed tracing.
/// If a correlation ID is provided in the request, it is used; otherwise, a new one is generated.
/// </summary>
public partial class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const int MaxCorrelationIdLength = 64;

    // Only allow alphanumeric characters, hyphens, and underscores
    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$")]
    private static partial Regex SafeCorrelationIdPattern();

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to get correlation ID from request header
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        // Validate: must be non-empty, within length limit, and contain only safe characters
        if (string.IsNullOrEmpty(correlationId)
            || correlationId.Length > MaxCorrelationIdLength
            || !SafeCorrelationIdPattern().IsMatch(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        // Set the trace identifier for logging
        context.TraceIdentifier = correlationId;

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the correlation ID middleware.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
