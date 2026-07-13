using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IComputerHistoryRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<ComputerHistory> Query { get; }

    Task<IEnumerable<ComputerHistory>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<ComputerHistory> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? action = null, string? search = null, DateTime? from = null, DateTime? to = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<List<string>> GetDistinctActionsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ComputerHistory history, CancellationToken cancellationToken = default);
    /// <summary>
    /// Insert a history record via <c>sp_add_computer_history</c> stored procedure.
    /// Executes immediately in its own transaction — not batched with EF change tracking.
    /// </summary>
    Task AddHistorySpAsync(int computerId, string action, string? changedBy = null, string? oldValue = null, string? newValue = null, string? notes = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Track a history record in the change tracker without calling SaveChanges.
    /// Use with a subsequent SaveChangesAsync call to batch multiple changes.
    /// </summary>
    void Track(ComputerHistory history);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
