namespace ComputerInventory.Core.Interfaces;

/// <summary>
/// Provides the username of the currently authenticated user.
/// Used by the audit interceptor to populate CreatedBy/UpdatedBy.
/// </summary>
public interface ICurrentUserService
{
    string? Username { get; }
}
