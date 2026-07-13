using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ComputerInventory.Api.Services;

public class InviteCodeService : IInviteCodeService
{
    private readonly IInviteCodeRepository _inviteCodeRepository;
    private readonly ILogger<InviteCodeService> _logger;

    public InviteCodeService(IInviteCodeRepository inviteCodeRepository, ILogger<InviteCodeService> logger)
    {
        _inviteCodeRepository = inviteCodeRepository;
        _logger = logger;
    }

    public async Task<(string RawCode, InviteCode Entity)> CreateCodeAsync(string createdBy, int maxUses = 1, int expirationHours = 72, string? note = null)
    {
        var rawCode = GenerateReadableCode();
        var codeHash = HashCode(rawCode);

        var entity = new InviteCode
        {
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
            CreatedAt = DateTime.UtcNow,
            MaxUses = maxUses,
            CreatedBy = createdBy,
            Note = note
        };

        await _inviteCodeRepository.AddAsync(entity);

        _logger.LogInformation("Invite code created by {User}, max uses: {MaxUses}, expires at {ExpiresAt}",
            createdBy, maxUses, entity.ExpiresAt);

        return (rawCode, entity);
    }

    public async Task<bool> ValidateAndConsumeAsync(string rawCode)
    {
        var codeHash = HashCode(rawCode);

        var invite = await _inviteCodeRepository.GetByCodeHashAsync(codeHash);

        if (invite == null)
        {
            _logger.LogWarning("Invite code validation failed: code not found");
            return false;
        }

        if (invite.IsRevoked)
        {
            _logger.LogWarning("Invite code validation failed: code revoked (id: {Id})", invite.Id);
            return false;
        }

        if (invite.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Invite code validation failed: code expired at {ExpiresAt} (id: {Id})", invite.ExpiresAt, invite.Id);
            return false;
        }

        if (invite.UseCount >= invite.MaxUses)
        {
            _logger.LogWarning("Invite code validation failed: max uses reached ({UseCount}/{MaxUses}, id: {Id})",
                invite.UseCount, invite.MaxUses, invite.Id);
            return false;
        }

        invite.UseCount++;
        await _inviteCodeRepository.SaveChangesAsync();

        _logger.LogInformation("Invite code consumed ({UseCount}/{MaxUses}, created by {User})",
            invite.UseCount, invite.MaxUses, invite.CreatedBy);
        return true;
    }

    public async Task<List<InviteCode>> GetAllAsync()
    {
        return await _inviteCodeRepository.GetAllAsync();
    }

    public async Task<bool> RevokeAsync(int id)
    {
        var invite = await _inviteCodeRepository.GetByIdAsync(id);
        if (invite == null) return false;

        invite.IsRevoked = true;
        await _inviteCodeRepository.SaveChangesAsync();

        _logger.LogInformation("Invite code revoked (id: {Id}, created by {User})", id, invite.CreatedBy);
        return true;
    }

    public async Task CleanupExpiredAsync(int maxAgeDays = 30)
    {
        var deleted = await _inviteCodeRepository.CleanupExpiredAsync(maxAgeDays);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired invite codes", deleted);
        }
    }

    /// <summary>
    /// Generates a human-readable invite code (e.g. "ABCD-1234-EFGH").
    /// </summary>
    private static string GenerateReadableCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // No I/O/0/1 to avoid confusion
        var bytes = RandomNumberGenerator.GetBytes(12);
        var sb = new StringBuilder(14);
        for (int i = 0; i < 12; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append('-');
            sb.Append(chars[bytes[i] % chars.Length]);
        }
        return sb.ToString();
    }

    private static string HashCode(string code)
    {
        // Normalize to uppercase before hashing so case doesn't matter on input
        var normalized = code.Replace("-", "").ToUpperInvariant();
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
