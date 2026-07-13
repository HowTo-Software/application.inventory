using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IComputerRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<Computer> Query { get; }

    Task<(IEnumerable<Computer> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? searchTerm = null, string? status = null, string? type = null, string? location = null, string? department = null, string? connection = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<ComputerDashboardCounts> GetDashboardCountsAsync(string? latestAgentVersion, CancellationToken cancellationToken = default);
    Task<Computer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Computer?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default);
    /// <summary>
    /// Get by serial number without requiring AgentStatus join (handles orphaned computers).
    /// Set includeDeleted to bypass the soft-delete query filter.
    /// </summary>
    Task<Computer?> GetBySerialNumberBasicAsync(string serialNumber, bool includeDeleted = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Computer>> GetByStatusAsync(ComputerStatus status, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Computer> Items, int TotalCount)> GetByStatusAsync(ComputerStatus status, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Computer>> GetByLocationAsync(int locationId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Computer> Items, int TotalCount)> GetByLocationAsync(int locationId, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Computer>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Computer> Items, int TotalCount)> GetByDepartmentAsync(int departmentId, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<Computer>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Computer> Items, int TotalCount)> SearchAsync(string searchTerm, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<Computer> AddAsync(Computer computer, CancellationToken cancellationToken = default);
    Task UpdateAsync(Computer computer, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<Dictionary<ComputerStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if a computer exists by ID without loading the full entity.
    /// More efficient than GetByIdAsync when you only need existence check.
    /// </summary>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a computer by ID without loading the entity first.
    /// Uses ExecuteDelete for better performance.
    /// </summary>
    Task<bool> DeleteDirectAsync(int id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update computer and add history record in a single transaction.
    /// </summary>
    Task UpdateWithHistoryAsync(Computer computer, ComputerHistory history, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save any pending changes. Use sparingly - prefer methods that handle their own saves.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear the change tracker to discard dirty entity state (e.g. after a failed insert).
    /// </summary>
    void ClearChangeTracker();

    /// <summary>
    /// Get multiple computers by their IDs, with full includes for display.
    /// </summary>
    Task<IEnumerable<Computer>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk update the status of multiple computers in a single transaction.
    /// Returns the number of records updated.
    /// </summary>
    Task<int> BulkUpdateStatusAsync(IEnumerable<int> ids, ComputerStatus status, string? updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the preferred location source for a computer's agent status via stored procedure.
    /// Returns true if exactly one row was updated.
    /// </summary>
    Task<bool> SetLocationSourcePreferenceAsync(int computerId, string preference, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all computers with their GeoLocation navigation property loaded.
    /// </summary>
    Task<List<Computer>> GetAllWithGeoLocationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a computer by ID with its GeoLocation navigation property loaded.
    /// </summary>
    Task<Computer?> GetByIdWithGeoLocationAsync(int computerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get agent statuses as a dictionary keyed by ComputerId.
    /// </summary>
    Task<Dictionary<int, ComputerAgentStatus>> GetAgentStatusDictionaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get agent status for a specific computer.
    /// </summary>
    Task<ComputerAgentStatus?> GetAgentStatusAsync(int computerId, CancellationToken cancellationToken = default);
}
