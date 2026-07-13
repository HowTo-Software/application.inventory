using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface ISystemDetailsRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<SystemDetails> Query { get; }

    Task AddAsync(SystemDetails details, CancellationToken cancellationToken = default);
    Task<SystemDetails?> GetLatestByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task<List<SystemDetails>> GetHistoryByComputerIdAsync(int computerId, int limit = 10, CancellationToken cancellationToken = default);
    Task<List<SystemDetails>> GetLatestForAllComputersAsync(CancellationToken cancellationToken = default);
    Task<(List<SystemDetails> Items, int TotalCount)> GetPagedLatestForAllComputersAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<(List<SystemDetailsReport> Items, int TotalCount)> GetPagedSystemDetailsReportAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
