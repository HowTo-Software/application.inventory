using ComputerInventory.Core.Entities;

namespace ComputerInventory.Api.Services;

public interface IInviteCodeService
{
    Task<(string RawCode, InviteCode Entity)> CreateCodeAsync(string createdBy, int maxUses = 1, int expirationHours = 72, string? note = null);
    Task<bool> ValidateAndConsumeAsync(string rawCode);
    Task<List<InviteCode>> GetAllAsync();
    Task<bool> RevokeAsync(int id);
    Task CleanupExpiredAsync(int maxAgeDays = 30);
}
