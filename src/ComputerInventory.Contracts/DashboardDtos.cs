namespace ComputerInventory.Contracts;

/// <summary>
/// Pre-computed dashboard counts from database-level aggregation.
/// Avoids loading all Computer entities into memory.
/// </summary>
public class ComputerDashboardCounts
{
    public int TotalComputers { get; set; }
    public int OnlineComputers { get; set; }
    public int OfflineComputers { get; set; }
    public int AgentsInstalled { get; set; }
    public int AgentsUpdating { get; set; }
    public int AgentsFailedUpdate { get; set; }
    public int AgentsPendingUpdate { get; set; }
    public int AgentsOnLatestVersion { get; set; }
    public int AgentsOnOlderVersion { get; set; }
    public int StaleAgents { get; set; }
    public int ExpiredWarranties { get; set; }
}

/// <summary>
/// Aggregated system health and dashboard data for the admin overview.
/// </summary>
public class DashboardDto
{
    // Inventory Summary
    public int TotalComputers { get; set; }
    public int OnlineComputers { get; set; }
    public int OfflineComputers { get; set; }
    public int AgentsInstalled { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();

    // Agent Health
    public int AgentsUpdating { get; set; }
    public int AgentsFailedUpdate { get; set; }
    public int AgentsPendingUpdate { get; set; }
    public string? LatestAgentVersion { get; set; }
    public int AgentsOnLatestVersion { get; set; }
    public int AgentsOnOlderVersion { get; set; }

    // Maintenance
    public int OpenMaintenanceRecords { get; set; }
    public int ScheduledMaintenance { get; set; }
    public int CompletedMaintenanceThisMonth { get; set; }

    // System Health
    public string ServerStatus { get; set; } = "Unknown";
    public string ServerVersion { get; set; } = "";
    public string Uptime { get; set; } = "";
    public double HealthCheckDurationMs { get; set; }
    public List<DashboardHealthCheck> HealthChecks { get; set; } = new();
    public DashboardCacheStats? CacheStats { get; set; }
    public DashboardEnvironmentInfo? Environment { get; set; }

    // Recent Activity
    public List<DashboardRecentActivity> RecentActivity { get; set; } = new();

    // Warnings
    public List<string> Warnings { get; set; } = new();
}

public class DashboardHealthCheck
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public double DurationMs { get; set; }
    public string? Description { get; set; }
}

public class DashboardCacheStats
{
    public int EntryCount { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate { get; set; }
}

public class DashboardEnvironmentInfo
{
    public string MachineName { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string OsVersion { get; set; } = "";
    public string RuntimeVersion { get; set; } = "";
    public bool Is64BitProcess { get; set; }
}

public class DashboardRecentActivity
{
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}
