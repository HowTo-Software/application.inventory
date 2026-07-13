using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IPackageRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<PackageVersion> Query { get; }

    Task<List<PackageVersion>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PackageVersion?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PackageVersion?> GetByIdWithRulesAsync(int id, CancellationToken cancellationToken = default);
    Task<PackageVersion?> GetByVersionAsync(string version, string packageType = "Agent", CancellationToken cancellationToken = default);
    Task<PackageVersion?> GetActiveVersionAsync(string packageType = "Agent", CancellationToken cancellationToken = default);
    Task<List<PackageVersion>> GetByStatusAsync(PackageStatus status, string packageType = "Agent", CancellationToken cancellationToken = default);
    Task<List<PackageVersion>> GetByChannelAsync(string channel, string packageType = "Agent", CancellationToken cancellationToken = default);
    Task AddAsync(PackageVersion package, CancellationToken cancellationToken = default);
    Task UpdateAsync(PackageVersion package, CancellationToken cancellationToken = default);
    Task DeleteAsync(PackageVersion package, CancellationToken cancellationToken = default);
    Task<bool> VersionExistsAsync(string version, string packageType = "Agent", CancellationToken cancellationToken = default);
    Task IncrementDownloadCountAsync(int packageId, CancellationToken cancellationToken = default);
    
    // Distribution rules
    Task AddDistributionRuleAsync(PackageDistributionRule rule, CancellationToken cancellationToken = default);
    Task RemoveDistributionRuleAsync(int ruleId, CancellationToken cancellationToken = default);
    Task<List<PackageDistributionRule>> GetDistributionRulesAsync(int packageId, CancellationToken cancellationToken = default);
    
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
