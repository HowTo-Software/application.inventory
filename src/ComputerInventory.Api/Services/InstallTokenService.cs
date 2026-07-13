using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ComputerInventory.Api.Services;

public class InstallTokenService : IInstallTokenService
{
    private readonly IInstallTokenRepository _installTokenRepository;
    private readonly ILogger<InstallTokenService> _logger;

    public InstallTokenService(IInstallTokenRepository installTokenRepository, ILogger<InstallTokenService> logger)
    {
        _installTokenRepository = installTokenRepository;
        _logger = logger;
    }

    public async Task<(string RawToken, InstallToken Entity)> CreateTokenAsync(string createdBy, int expirationMinutes = 60)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashToken(rawToken);

        var entity = new InstallToken
        {
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _installTokenRepository.AddAsync(entity);

        _logger.LogInformation("Install token created by {User}, expires at {ExpiresAt}", createdBy, entity.ExpiresAt);

        return (rawToken, entity);
    }

    public async Task<bool> ValidateAndConsumeTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);

        var token = await _installTokenRepository.GetByTokenHashAsync(tokenHash);

        if (token == null)
        {
            _logger.LogWarning("Install token validation failed: token not found");
            return false;
        }

        if (token.ConsumedAt != null)
        {
            _logger.LogWarning("Install token validation failed: token already consumed at {ConsumedAt}", token.ConsumedAt);
            return false;
        }

        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Install token validation failed: token expired at {ExpiresAt}", token.ExpiresAt);
            return false;
        }

        token.ConsumedAt = DateTime.UtcNow;
        await _installTokenRepository.SaveChangesAsync();

        _logger.LogInformation("Install token consumed (created by {User})", token.CreatedBy);
        return true;
    }

    public async Task CleanupExpiredTokensAsync(int maxAgeDays = 7)
    {
        var deleted = await _installTokenRepository.CleanupExpiredAsync(maxAgeDays);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired install tokens", deleted);
        }
    }

    private static string HashToken(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
