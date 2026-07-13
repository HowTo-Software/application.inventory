using ComputerInventory.Core.Entities;

namespace ComputerInventory.Api.Services;

public interface IInstallTokenService
{
    /// <summary>
    /// Generates a new single-use install token. Returns the raw token value (not stored).
    /// </summary>
    Task<(string RawToken, InstallToken Entity)> CreateTokenAsync(string createdBy, int expirationMinutes = 60);

    /// <summary>
    /// Validates and consumes a token. Returns true if the token was valid and is now consumed.
    /// </summary>
    Task<bool> ValidateAndConsumeTokenAsync(string rawToken);

    /// <summary>
    /// Removes expired and consumed tokens older than the specified age.
    /// </summary>
    Task CleanupExpiredTokensAsync(int maxAgeDays = 7);
}
