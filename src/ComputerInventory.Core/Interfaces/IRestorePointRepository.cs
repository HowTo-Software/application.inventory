using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IRestorePointRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<RestorePoint> Query { get; }

    Task<RestorePoint?> GetByGuidAsync(Guid restorePointId, CancellationToken cancellationToken = default);
    Task<RestorePoint?> GetByGuidWithDetailsAsync(Guid restorePointId, CancellationToken cancellationToken = default);
    Task<List<RestorePoint>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task<RestorePoint?> GetLatestCompleteByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task AddAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default);
    Task DeleteAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default);
    Task<int> CleanupOldRestorePointsAsync(int computerId, int keepCount, CancellationToken cancellationToken = default);
    Task<(List<RestorePointSummaryItem> Items, int TotalCount)> GetSummaryAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}

public class RestorePointSummaryItem
{
    public int ComputerId { get; set; }
    public string Hostname { get; set; } = "Unknown";
    public string SerialNumber { get; set; } = "Unknown";
    public int TotalRestorePoints { get; set; }
    public DateTime LatestRestorePoint { get; set; }
    public DateTime OldestRestorePoint { get; set; }
    public long TotalSizeBytes { get; set; }
    public int RebootCount { get; set; }
}
