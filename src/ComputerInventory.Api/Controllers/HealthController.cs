using System.Reflection;
using ComputerInventory.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ComputerInventory.Api.Controllers;

/// <summary>
/// Health check endpoints for monitoring and client connectivity testing.
/// Provides basic health checks and detailed system status.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly ILogger<HealthController> _logger;
    private readonly HealthCheckService _healthCheckService;
    private readonly ICacheService? _cacheService;

    public HealthController(
        ILogger<HealthController> logger,
        HealthCheckService healthCheckService,
        ICacheService? cacheService = null)
    {
        _logger = logger;
        _healthCheckService = healthCheckService;
        _cacheService = cacheService;
    }

    /// <summary>
    /// Simple health check - returns 200 OK if server is running.
    /// Use this for load balancer health checks.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), 200)]
    [ProducesResponseType(503)]
    public IActionResult Get()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion(),
            Uptime = GetUptime()
        });
    }

    /// <summary>
    /// Ping endpoint for quick connectivity check.
    /// Minimal response for latency testing.
    /// </summary>
    [HttpGet("ping")]
    [ProducesResponseType(typeof(string), 200)]
    public IActionResult Ping()
    {
        return Ok("pong");
    }

    /// <summary>
    /// Detailed health check with all system components.
    /// Use this for monitoring dashboards.
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(typeof(DetailedHealthResponse), 200)]
    [ProducesResponseType(typeof(DetailedHealthResponse), 503)]
    public async Task<IActionResult> GetDetailed(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new DetailedHealthResponse
        {
            Status = report.Status.ToString(),
            Timestamp = DateTime.UtcNow,
            Version = GetAssemblyVersion(),
            Uptime = GetUptime(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Checks = report.Entries.Select(e => new HealthCheckEntry
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Duration = e.Value.Duration.TotalMilliseconds,
                Description = e.Value.Description,
                Data = e.Value.Data?.ToDictionary(d => d.Key, d => d.Value),
                Exception = e.Value.Exception?.Message
            }).ToList()
        };

        // Add cache stats if available
        if (_cacheService != null)
        {
            response.CacheStats = _cacheService.GetStats();
        }

        // Add environment info
        response.Environment = new EnvironmentInfo
        {
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            OSVersion = Environment.OSVersion.ToString(),
            RuntimeVersion = Environment.Version.ToString(),
            Is64BitProcess = Environment.Is64BitProcess
        };

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        return StatusCode(statusCode, response);
    }

    /// <summary>
    /// Liveness probe - checks if the application is running.
    /// Use this for Kubernetes liveness probes.
    /// </summary>
    [HttpGet("live")]
    [ProducesResponseType(200)]
    public IActionResult Live()
    {
        return Ok(new { status = "Alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe - checks if the application is ready to accept traffic.
    /// Use this for Kubernetes readiness probes.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(
            c => c.Tags.Contains("ready"),
            cancellationToken);

        if (report.Status == HealthStatus.Unhealthy)
        {
            var unhealthyEntry = report.Entries.FirstOrDefault(e => e.Value.Status == HealthStatus.Unhealthy);
            return StatusCode(503, new
            {
                status = "NotReady",
                timestamp = DateTime.UtcNow,
                reason = unhealthyEntry.Value.Description ?? "Unknown"
            });
        }

        return Ok(new { status = "Ready", timestamp = DateTime.UtcNow });
    }

    private static string GetAssemblyVersion()
    {
        return _assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? _assembly.GetName().Version?.ToString()
            ?? "1.0.0";
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}

/// <summary>
/// Basic health response.
/// </summary>
public class HealthResponse
{
    public string Status { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Uptime { get; set; } = "";
}

/// <summary>
/// Detailed health response with all system checks.
/// </summary>
public class DetailedHealthResponse : HealthResponse
{
    public double TotalDuration { get; set; }
    public List<HealthCheckEntry> Checks { get; set; } = new();
    public CacheStats? CacheStats { get; set; }
    public EnvironmentInfo? Environment { get; set; }
}

/// <summary>
/// Individual health check entry.
/// </summary>
public class HealthCheckEntry
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public double Duration { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public string? Exception { get; set; }
}

/// <summary>
/// Environment information.
/// </summary>
public class EnvironmentInfo
{
    public string MachineName { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string OSVersion { get; set; } = "";
    public string RuntimeVersion { get; set; } = "";
    public bool Is64BitProcess { get; set; }
}
