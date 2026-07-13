using System.Reflection;
using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Constants;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class DashboardController : ControllerBase
{
    private readonly IComputerRepository _computerRepository;
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IComputerHistoryRepository _historyRepository;
    private readonly HealthCheckService _healthCheckService;
    private readonly ICacheService _cacheService;
    private readonly CacheServiceOptions _cacheOptions;
    private readonly IUpdateInfoCacheService _updateInfoCache;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IComputerRepository computerRepository,
        IMaintenanceRepository maintenanceRepository,
        IComputerHistoryRepository historyRepository,
        HealthCheckService healthCheckService,
        ICacheService cacheService,
        CacheServiceOptions cacheOptions,
        IUpdateInfoCacheService updateInfoCache,
        ILogger<DashboardController> logger)
    {
        _computerRepository = computerRepository;
        _maintenanceRepository = maintenanceRepository;
        _historyRepository = historyRepository;
        _healthCheckService = healthCheckService;
        _cacheService = cacheService;
        _cacheOptions = cacheOptions;
        _updateInfoCache = updateInfoCache;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(DashboardDto), 200)]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _cacheService.GetOrCreateAsync(
            "dashboard:overview",
            async () => await BuildDashboardAsync(cancellationToken),
            TimeSpan.FromSeconds(_cacheOptions.DashboardCacheSeconds));

        return Ok(dashboard);
    }

    private async Task<DashboardDto> BuildDashboardAsync(CancellationToken cancellationToken)
    {
        var dto = new DashboardDto();

        // Get version info first (needed for dashboard counts query)
        var updateInfo = _updateInfoCache.GetUpdateInfo();
        var latestVersion = updateInfo?.Version;
        dto.LatestAgentVersion = latestVersion;

        // Database-level aggregate counts — avoids loading all Computer entities
        var counts = await _computerRepository.GetDashboardCountsAsync(latestVersion, cancellationToken);

        dto.TotalComputers = counts.TotalComputers;
        dto.OnlineComputers = counts.OnlineComputers;
        dto.OfflineComputers = counts.OfflineComputers;
        dto.AgentsInstalled = counts.AgentsInstalled;

        // Status counts
        var statusCounts = await _computerRepository.GetStatusCountsAsync(cancellationToken);
        dto.StatusCounts = statusCounts.ToDictionary(x => x.Key.ToString(), x => x.Value);

        // Agent update health
        dto.AgentsUpdating = counts.AgentsUpdating;
        dto.AgentsFailedUpdate = counts.AgentsFailedUpdate;
        dto.AgentsPendingUpdate = counts.AgentsPendingUpdate;

        dto.AgentsOnLatestVersion = counts.AgentsOnLatestVersion;
        dto.AgentsOnOlderVersion = counts.AgentsOnOlderVersion;

        // Maintenance summary
        var (open, scheduled, completedThisMonth) = await _maintenanceRepository.GetMaintenanceSummaryCountsAsync(cancellationToken);

        dto.OpenMaintenanceRecords = open;
        dto.ScheduledMaintenance = scheduled;
        dto.CompletedMaintenanceThisMonth = completedThisMonth;

        // System health
        var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);
        dto.ServerStatus = healthReport.Status.ToString();
        dto.ServerVersion = GetAssemblyVersion();
        dto.Uptime = GetUptime();
        dto.HealthCheckDurationMs = healthReport.TotalDuration.TotalMilliseconds;

        dto.HealthChecks = healthReport.Entries.Select(e => new DashboardHealthCheck
        {
            Name = e.Key,
            Status = e.Value.Status.ToString(),
            DurationMs = e.Value.Duration.TotalMilliseconds,
            Description = e.Value.Description
        }).ToList();

        // Cache stats
        var cacheStats = _cacheService.GetStats();
        dto.CacheStats = new DashboardCacheStats
        {
            EntryCount = cacheStats.EntryCount,
            Hits = cacheStats.Hits,
            Misses = cacheStats.Misses,
            HitRate = cacheStats.HitRate
        };

        // Environment
        dto.Environment = new DashboardEnvironmentInfo
        {
            MachineName = System.Environment.MachineName,
            ProcessorCount = System.Environment.ProcessorCount,
            OsVersion = System.Environment.OSVersion.ToString(),
            RuntimeVersion = System.Environment.Version.ToString(),
            Is64BitProcess = System.Environment.Is64BitProcess
        };

        // Recent activity — fetch only the 10 most recently updated computers
        var (recentItems, _) = await _computerRepository.GetAllAsync(
            1, 10, sortBy: "updatedat", sortDirection: "desc", cancellationToken: cancellationToken);

        dto.RecentActivity = recentItems
            .Where(c => c.UpdatedAt.HasValue)
            .Select(c => new DashboardRecentActivity
            {
                Timestamp = c.UpdatedAt ?? c.CreatedAt,
                Type = c.AgentStatus.IsOnline ? HistoryActions.Heartbeat : HistoryActions.StatusChange,
                Description = $"{c.Hostname} ({c.SerialNumber}) — last seen {(c.AgentStatus.LastSeen.HasValue ? GetTimeAgo(c.AgentStatus.LastSeen.Value) : "never")}"
            }).ToList();

        // Warnings
        if (counts.StaleAgents > 0)
            dto.Warnings.Add($"{counts.StaleAgents} agent(s) haven't checked in for over 24 hours");

        if (dto.AgentsFailedUpdate > 0)
            dto.Warnings.Add($"{dto.AgentsFailedUpdate} agent(s) have failed updates");

        if (counts.ExpiredWarranties > 0)
            dto.Warnings.Add($"{counts.ExpiredWarranties} computer(s) have expired warranties");

        if (dto.AgentsOnOlderVersion > 0)
            dto.Warnings.Add($"{dto.AgentsOnOlderVersion} agent(s) are running an older version");

        return dto;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "1.0.0";
    }

    private static string GetUptime()
    {
        var uptime = DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
        return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }

    private static string GetTimeAgo(DateTime utcTime)
    {
        var span = DateTime.UtcNow - utcTime;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
