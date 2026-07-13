using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IVersionHistoryRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<VersionHistory> Query { get; }

    Task<List<VersionHistory>> GetByTypeAsync(VersionType type, CancellationToken cancellationToken = default);
    Task<(List<VersionHistory> Items, int TotalCount)> GetByTypePagedAsync(VersionType type, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<(List<VersionHistory> ServerItems, int ServerTotal, List<VersionHistory> ThinClientItems, int ThinClientTotal)> GetAllVersionHistoryPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<VersionHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(VersionType type, string version, CancellationToken cancellationToken = default);
    Task<VersionHistory?> FindByTypeAndVersionAsync(VersionType type, string version, CancellationToken cancellationToken = default);
    Task<List<VersionHistory>> GetCurrentVersionsByTypeAsync(VersionType type, CancellationToken cancellationToken = default);
    Task AddAsync(VersionHistory versionHistory, CancellationToken cancellationToken = default);
    Task DeleteAsync(VersionHistory versionHistory, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
