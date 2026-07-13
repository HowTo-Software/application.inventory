using ComputerInventory.Core.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Health check for database connectivity.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IHealthCheckRepository _healthCheckRepository;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(IHealthCheckRepository healthCheckRepository, ILogger<DatabaseHealthCheck> logger)
    {
        _healthCheckRepository = healthCheckRepository;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var computerCount = await _healthCheckRepository.GetHealthCheckCountAsync(cancellationToken);
            var queryTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            var data = new Dictionary<string, object>
            {
                ["computerCount"] = computerCount,
                ["queryTimeMs"] = queryTime,
                ["provider"] = _healthCheckRepository.GetProviderName()
            };

            // Warn if query is slow
            if (queryTime > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database is slow. Query took {queryTime:F0}ms",
                    data: data);
            }

            return HealthCheckResult.Healthy("Database is healthy", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                exception: ex);
        }
    }
}

/// <summary>
/// Health check for disk space availability.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private readonly long _minimumFreeSpaceBytes;

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger, long minimumFreeSpaceGb = 1)
    {
        _logger = logger;
        _minimumFreeSpaceBytes = minimumFreeSpaceGb * 1024 * 1024 * 1024;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var drive = new DriveInfo(Path.GetPathRoot(currentDirectory) ?? "C:");

            var freeSpaceGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var totalSpaceGb = drive.TotalSize / (1024.0 * 1024 * 1024);
            var usedPercent = (1 - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100;

            var data = new Dictionary<string, object>
            {
                ["drive"] = drive.Name,
                ["freeSpaceGb"] = Math.Round(freeSpaceGb, 2),
                ["totalSpaceGb"] = Math.Round(totalSpaceGb, 2),
                ["usedPercent"] = Math.Round(usedPercent, 1)
            };

            if (drive.AvailableFreeSpace < _minimumFreeSpaceBytes)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Low disk space: {freeSpaceGb:F2} GB remaining",
                    data: data));
            }

            if (usedPercent > 90)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Disk space is {usedPercent:F1}% used",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space is healthy: {freeSpaceGb:F2} GB free",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk space health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Disk space health check failed",
                exception: ex));
        }
    }
}

/// <summary>
/// Health check for memory usage.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly ILogger<MemoryHealthCheck> _logger;

    public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSetMb = process.WorkingSet64 / (1024.0 * 1024);
            var privateMemoryMb = process.PrivateMemorySize64 / (1024.0 * 1024);
            var gcMemoryMb = GC.GetTotalMemory(false) / (1024.0 * 1024);

            var data = new Dictionary<string, object>
            {
                ["workingSetMb"] = Math.Round(workingSetMb, 2),
                ["privateMemoryMb"] = Math.Round(privateMemoryMb, 2),
                ["gcMemoryMb"] = Math.Round(gcMemoryMb, 2),
                ["gen0Collections"] = GC.CollectionCount(0),
                ["gen1Collections"] = GC.CollectionCount(1),
                ["gen2Collections"] = GC.CollectionCount(2)
            };

            // Warn if memory usage is high (over 1GB working set)
            if (workingSetMb > 1024)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"High memory usage: {workingSetMb:F0} MB working set",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory usage: {workingSetMb:F0} MB working set",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Memory health check failed",
                exception: ex));
        }
    }
}

/// <summary>
/// Health check for the agent update package availability.
/// </summary>
public class AgentPackageHealthCheck : IHealthCheck
{
    private readonly ILogger<AgentPackageHealthCheck> _logger;
    private readonly AgentFileSettings _fileSettings;

    public AgentPackageHealthCheck(ILogger<AgentPackageHealthCheck> logger, AgentFileSettings fileSettings)
    {
        _logger = logger;
        _fileSettings = fileSettings;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var agentPath = _fileSettings.GetAgentPath();
            var updatePath = _fileSettings.GetUpdatePath();
            var updateInfoPath = _fileSettings.GetUpdateJsonPath();

            var data = new Dictionary<string, object>
            {
                ["agentPath"] = agentPath,
                ["agentPathExists"] = Directory.Exists(agentPath)
            };

            if (!Directory.Exists(agentPath))
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Agent directory not found",
                    data: data));
            }

            if (File.Exists(updateInfoPath))
            {
                var updateInfo = File.ReadAllText(updateInfoPath);
                data["hasUpdatePackage"] = true;
                data["updateInfoSize"] = updateInfo.Length;
            }
            else
            {
                data["hasUpdatePackage"] = false;
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "Agent package is available",
                data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent package health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Agent package health check failed",
                exception: ex));
        }
    }
}

/// <summary>
/// Extension methods for registering health checks.
/// </summary>
public static class HealthCheckExtensions
{
    public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
            .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: new[] { "ready" })
            .AddCheck<MemoryHealthCheck>("memory", tags: new[] { "ready" })
            .AddCheck<AgentPackageHealthCheck>("agent-package", tags: new[] { "ready" });

        return services;
    }
}
