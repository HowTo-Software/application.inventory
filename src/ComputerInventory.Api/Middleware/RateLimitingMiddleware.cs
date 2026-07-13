using System.Net;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Rate limiting middleware that protects the API from abuse.
/// Supports different limits per endpoint category and client type.
/// Rate limit state is stored via <see cref="IRateLimitStore"/>, which can be
/// backed by an in-memory dictionary (single instance) or a distributed cache
/// such as Redis (multi-instance deployments).
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly IRateLimitStore _store;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IRateLimitStore store,
        RateLimitingOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _store = store;
        _options = options ?? new RateLimitingOptions();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for excluded paths
        if (ShouldSkipRateLimiting(context))
        {
            await _next(context);
            return;
        }

        var isValidAgent = IsValidAgentRequest(context);
        var clientKey = GetClientKey(context, isValidAgent);
        var category = GetEndpointCategory(context.Request.Path);
        var limit = GetRateLimit(category, context, isValidAgent);

        var result = await _store.CheckAndIncrementAsync(clientKey, limit.RequestsPerWindow, limit.WindowSeconds);

        if (result.IsLimited)
        {
            await HandleRateLimitExceeded(context, clientKey, result);
            return;
        }

        // Add rate limit headers
        AddRateLimitHeaders(context, result);

        await _next(context);
    }

    private bool ShouldSkipRateLimiting(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        
        // Skip health checks
        if (path.Contains("/health"))
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether the request carries a valid agent API key.
    /// Requests with an invalid or missing key are NOT treated as agent requests.
    /// </summary>
    private bool IsValidAgentRequest(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
            string.IsNullOrEmpty(providedKey))
            return false;

        var configuredKey = _options.AgentApiKey;
        return !string.IsNullOrEmpty(configuredKey) &&
               string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
    }

    private string GetClientKey(HttpContext context, bool isValidAgent)
    {
        // Use forwarded header when behind a reverse proxy (e.g. Docker web container)
        var clientIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                       ?? context.Connection.RemoteIpAddress?.ToString()
                       ?? "unknown";

        // Agents are identified per-IP so each device gets its own rate-limit bucket
        if (isValidAgent)
            return $"agent:{clientIp}";

        // Web users: combination of IP and user identity
        var userId = "anonymous";
        if (context.User.Identity?.IsAuthenticated == true)
        {
            userId = context.User.Identity.Name
                     ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? context.User.FindFirst("sub")?.Value
                     ?? $"authenticated:{clientIp}";
        }

        return $"{clientIp}:{userId}";
    }

    private static string GetEndpointCategory(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? "";

        // Categorize endpoints for different rate limits
        if (pathValue.Contains("/agent/"))
            return "agent";
        
        if (pathValue.Contains("/auth/login") || pathValue.Contains("/auth/register"))
            return "auth"; // Stricter limit for auth endpoints
        
        if (pathValue.Contains("/search") || pathValue.Contains("/stats"))
            return "query"; // Medium limit for expensive queries
        
        return "default";
    }

    private RateLimitConfig GetRateLimit(string category, HttpContext context, bool isValidAgent)
    {
        // Valid agents get their own per-agent limits (not multiplied)
        if (isValidAgent && category == "agent")
            return new RateLimitConfig(_options.AgentRequestsPerMinute, 60);

        // Authenticated web users get higher limits
        var isAuthenticated = context.User.Identity?.IsAuthenticated == true;
        var multiplier = isAuthenticated ? 2.0 : 1.0;

        var baseLimit = category switch
        {
            "agent" => new RateLimitConfig(_options.AgentRequestsPerMinute, 60),
            "auth" => new RateLimitConfig(_options.AuthRequestsPerMinute, 60),
            "query" => new RateLimitConfig(_options.QueryRequestsPerMinute, 60),
            _ => new RateLimitConfig(_options.DefaultRequestsPerMinute, 60)
        };

        return new RateLimitConfig(
            (int)(baseLimit.RequestsPerWindow * multiplier),
            baseLimit.WindowSeconds);
    }

    private async Task HandleRateLimitExceeded(HttpContext context, string clientKey, RateLimitResult result)
    {
        _logger.LogWarning(
            "Rate limit exceeded for client {ClientKey}. Limit: {Limit} requests per {Window} seconds. Path: {Path}",
            clientKey,
            result.Limit,
            60,
            context.Request.Path);

        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/problem+json";

        context.Response.Headers["Retry-After"] = result.RetryAfterSeconds.ToString();
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = "0";

        var problemDetails = new
        {
            type = "https://httpstatuses.com/429",
            title = "Too Many Requests",
            status = 429,
            detail = $"Rate limit exceeded. Please wait {result.RetryAfterSeconds} seconds before retrying.",
            instance = context.Request.Path.Value,
            retryAfter = result.RetryAfterSeconds
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static void AddRateLimitHeaders(HttpContext context, RateLimitResult result)
    {
        var remaining = Math.Max(0, result.Limit - result.RequestCount);
        
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = result.ResetTimeUnix.ToString();
    }
}

/// <summary>
/// Rate limit configuration for a category.
/// </summary>
public record RateLimitConfig(int RequestsPerWindow, int WindowSeconds);

/// <summary>
/// Configuration options for rate limiting middleware.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Default requests allowed per minute for uncategorized endpoints.
    /// </summary>
    public int DefaultRequestsPerMinute { get; set; } = 100;

    /// <summary>
    /// Requests allowed per minute per agent (identified by IP).
    /// </summary>
    public int AgentRequestsPerMinute { get; set; } = 1000;

    /// <summary>
    /// Requests allowed per minute for auth endpoints (login, register).
    /// </summary>
    public int AuthRequestsPerMinute { get; set; } = 10;

    /// <summary>
    /// Requests allowed per minute for expensive query endpoints (search, stats).
    /// </summary>
    public int QueryRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// The configured agent API key used to validate agent identity.
    /// When set, requests with a matching X-Api-Key header are rate-limited
    /// per agent IP instead of bypassing rate limiting entirely.
    /// </summary>
    public string? AgentApiKey { get; set; }

    /// <summary>
    /// Whether to enable rate limiting (can be disabled in development).
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Extension methods for registering the rate limiting middleware.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseCustomRateLimiting(this IApplicationBuilder app,
        IConfiguration configuration,
        Action<RateLimitingOptions>? configure = null)
    {
        var options = new RateLimitingOptions();
        configuration.GetSection("RateLimiting").Bind(options);
        configure?.Invoke(options);

        if (!options.Enabled)
            return app;

        return app.UseMiddleware<RateLimitingMiddleware>(options);
    }
}
