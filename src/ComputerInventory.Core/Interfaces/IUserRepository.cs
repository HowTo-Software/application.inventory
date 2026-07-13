using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Deferred IQueryable for composing custom queries without materializing.
    /// Returned as no-tracking by default.
    /// </summary>
    IQueryable<ApplicationUser> Query { get; }

    Task<(IEnumerable<ApplicationUser> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? searchTerm = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApplicationUser>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default);
    Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<(bool UsernameExists, bool EmailExists)> UserExistsAsync(string username, string email, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> GetByVerificationTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
