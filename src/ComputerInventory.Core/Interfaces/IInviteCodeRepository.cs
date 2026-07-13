using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IInviteCodeRepository
{
    Task<InviteCode> AddAsync(InviteCode code, CancellationToken cancellationToken = default);
    Task<InviteCode?> GetByCodeHashAsync(string codeHash, CancellationToken cancellationToken = default);
    Task<InviteCode?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<List<InviteCode>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredAsync(int maxAgeDays, CancellationToken cancellationToken = default);
}
