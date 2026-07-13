using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IInstallTokenRepository
{
    Task<InstallToken> AddAsync(InstallToken token, CancellationToken cancellationToken = default);
    Task<InstallToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredAsync(int maxAgeDays, CancellationToken cancellationToken = default);
}
