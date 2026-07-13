using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IMaintenanceRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<MaintenanceRecord> Query { get; }

    Task<(int Open, int Scheduled, int CompletedThisMonth)> GetMaintenanceSummaryCountsAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<MaintenanceRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MaintenanceRecord>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetByComputerIdPagedAsync(int computerId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<MaintenanceRecord>> GetByStatusAsync(MaintenanceStatus status, CancellationToken cancellationToken = default);
    Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetByStatusPagedAsync(MaintenanceStatus status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<MaintenanceRecord>> GetScheduledBetweenDatesAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetScheduledBetweenDatesPagedAsync(DateTime start, DateTime end, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<MaintenanceRecord> AddAsync(MaintenanceRecord record, CancellationToken cancellationToken = default);
    Task UpdateAsync(MaintenanceRecord record, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
