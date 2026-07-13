using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Middleware that tracks request timing and adds performance headers to responses.
/// Useful for monitoring API performance and identifying slow endpoints.
/// </summary>
public class RequestTimingMiddleware
{
    private static readonly Meter s_meter = new("ComputerInventory.Api.RequestTiming", "1.0.0");
    private static readonly Histogram<double> s_requestDuration = s_meter.CreateHistogram<double>(
        "http.server.request.duration",
        unit: "ms",
        description: "Duration of HTTP server requests in milliseconds");
    private static readonly Counter<long> s_requestCount = s_meter.CreateCounter<long>(
        "http.server.request.count",
        unit: "{request}",
        description: "Total number of HTTP server requests");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;
    private readonly RequestTimingOptions _options;

    // Sliding window of recent slow-request timestamps for sustained-degradation alerting
    private readonly ConcurrentQueue<DateTime> _slowRequestTimestamps = new();
    private DateTime _lastSustainedAlert = DateTime.MinValue;
    private readonly object _alertLock = new();

    public RequestTimingMiddleware(
        RequestDelegate next,
        ILogger<RequestTimingMiddleware> logger,
        RequestTimingOptions? options = null)
    {
        _next = next;
        _logger = logger;
        _options = options ?? new RequestTimingOptions();
    }

    /// <summary>
    /// Key used to share the Stopwatch instance with other middleware via HttpContext.Items.
    /// </summary>
    public const string StopwatchItemKey = "RequestTiming.Stopwatch";

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        context.Items[StopwatchItemKey] = stopwatch;

        // Add response headers callback to include timing info
        context.Response.OnStarting(() =>
        {
            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            // Add timing headers
            if (_options.AddTimingHeaders)
            {
                context.Response.Headers["X-Response-Time"] = $"{elapsedMs}ms";
                context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
            }

            // Add server info header (optional)
            if (_options.AddServerHeader)
            {
                context.Response.Headers["X-Server-Version"] = _options.ServerVersion;
            }

            // Log slow requests and check for sustained degradation
            if (elapsedMs > _options.SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow request detected: {Method} {Path} took {ElapsedMs}ms (threshold: {Threshold}ms). TraceId: {TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    elapsedMs,
                    _options.SlowRequestThresholdMs,
                    context.TraceIdentifier);

                CheckSustainedDegradation();
            }

            // Track metrics (can be extended for Prometheus/AppInsights)
            TrackMetrics(context, elapsedMs);

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static void TrackMetrics(HttpContext context, long elapsedMs)
    {
        var tags = new TagList
        {
            { "http.request.method", context.Request.Method },
            { "http.route", context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value },
            { "http.response.status_code", context.Response.StatusCode }
        };

        s_requestDuration.Record(elapsedMs, tags);
        s_requestCount.Add(1, tags);
    }

    private void CheckSustainedDegradation()
    {
        var now = DateTime.UtcNow;
        _slowRequestTimestamps.Enqueue(now);

        // Prune entries outside the sliding window
        var cutoff = now.AddMinutes(-_options.SustainedDegradationWindowMinutes);
        while (_slowRequestTimestamps.TryPeek(out var oldest) && oldest < cutoff)
        {
            _slowRequestTimestamps.TryDequeue(out _);
        }

        var count = _slowRequestTimestamps.Count;
        if (count >= _options.SustainedDegradationThreshold)
        {
            // Throttle alerts: don't fire more than once per window
            lock (_alertLock)
            {
                if (now - _lastSustainedAlert < TimeSpan.FromMinutes(_options.SustainedDegradationWindowMinutes))
                    return;
                _lastSustainedAlert = now;
            }

            _logger.LogError(
                "Sustained performance degradation: {Count} slow requests (>{SlowThresholdMs}ms) in the last {WindowMinutes} minute(s) (threshold: {Threshold})",
                count,
                _options.SlowRequestThresholdMs,
                _options.SustainedDegradationWindowMinutes,
                _options.SustainedDegradationThreshold);
        }
    }
}

/// <summary>
/// Configuration options for request timing middleware.
/// </summary>
public class RequestTimingOptions
{
    /// <summary>
    /// Whether to add X-Response-Time and X-Request-Id headers.
    /// </summary>
    public bool AddTimingHeaders { get; set; } = true;

    /// <summary>
    /// Whether to add X-Server-Version header.
    /// </summary>
    public bool AddServerHeader { get; set; } = true;

    /// <summary>
    /// Server version string to include in headers.
    /// Defaults to the entry assembly's informational version, or its assembly version.
    /// </summary>
    public string ServerVersion { get; set; } =
        Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// Threshold in milliseconds for logging slow requests.
    /// </summary>
    public int SlowRequestThresholdMs { get; set; } = 1000;

    /// <summary>
    /// Number of slow requests within the sliding window that triggers a sustained-degradation alert.
    /// </summary>
    public int SustainedDegradationThreshold { get; set; } = 10;

    /// <summary>
    /// Sliding window duration in minutes for sustained-degradation detection.
    /// </summary>
    public int SustainedDegradationWindowMinutes { get; set; } = 5;
}

/// <summary>
/// Extension methods for registering the request timing middleware.
/// </summary>
public static class RequestTimingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestTiming(this IApplicationBuilder app,
        Action<RequestTimingOptions>? configure = null)
    {
        var options = new RequestTimingOptions();
        configure?.Invoke(options);

        return app.UseMiddleware<RequestTimingMiddleware>(options);
    }
}
