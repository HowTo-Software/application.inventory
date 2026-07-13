using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IComputerOwnershipRepository
{
    Task<ComputerOwnership?> GetAsync(int computerId, int userId);
    Task<IEnumerable<ComputerOwnership>> GetByComputerIdAsync(int computerId);
    Task<IEnumerable<ComputerOwnership>> GetByUserIdAsync(int userId);
    Task<bool> HasAccessAsync(int computerId, int userId, ComputerAccessLevel minimumLevel = ComputerAccessLevel.Viewer);
    Task AddAsync(ComputerOwnership ownership);
    Task RemoveAsync(int computerId, int userId);
    Task RemoveAllForComputerAsync(int computerId);
}
