namespace ComputerInventory.Web.Services;

/// <summary>
/// Tracks login session state.
/// The actual auth token is stored in an httpOnly cookie (not accessible to JavaScript).
/// This provider tracks whether the user has an active session for UI purposes.
/// </summary>
public class AuthTokenProvider
{
    private bool _isLoggedIn;
    private DateTime? _expiresAtUtc;

    /// <summary>
    /// Assumed access-token lifetime in minutes (matches server default).
    /// Used to calculate when to show the "expiring soon" warning.
    /// </summary>
    private const int DefaultExpirationMinutes = 60;

    /// <summary>
    /// How many minutes before expiration to show the warning.
    /// </summary>
    public const int WarningThresholdMinutes = 5;

    /// <summary>
    /// Event fired when the session has likely expired
    /// </summary>
    public event Action? TokenExpired;

    /// <summary>
    /// Event fired when the session is about to expire (within WarningThresholdMinutes).
    /// </summary>
    public event Action? TokenExpiring;

    public bool HasToken => _isLoggedIn;

    /// <summary>
    /// Returns the remaining time until the access token is expected to expire,
    /// or null if the user is not logged in.
    /// </summary>
    public TimeSpan? TimeUntilExpiration =>
        _expiresAtUtc.HasValue ? _expiresAtUtc.Value - DateTime.UtcNow : null;

    /// <summary>
    /// Mark the user as logged in (token is in httpOnly cookie)
    /// </summary>
    public void MarkLoggedIn()
    {
        _isLoggedIn = true;
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(DefaultExpirationMinutes);
    }

    /// <summary>
    /// Called after a successful token refresh to reset the expiration window.
    /// </summary>
    public void MarkRefreshed()
    {
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(DefaultExpirationMinutes);
    }

    public void ClearToken()
    {
        _isLoggedIn = false;
        _expiresAtUtc = null;
    }

    /// <summary>
    /// Called when a refresh attempt fails, indicating the session is truly expired.
    /// </summary>
    public void NotifyExpired()
    {
        _isLoggedIn = false;
        _expiresAtUtc = null;
        TokenExpired?.Invoke();
    }

    /// <summary>
    /// Check remaining time and fire TokenExpiring if within the warning threshold.
    /// </summary>
    public void CheckExpiration()
    {
        if (!_isLoggedIn || !_expiresAtUtc.HasValue) return;

        var remaining = _expiresAtUtc.Value - DateTime.UtcNow;
        if (remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(WarningThresholdMinutes))
        {
            TokenExpiring?.Invoke();
        }
    }
}
