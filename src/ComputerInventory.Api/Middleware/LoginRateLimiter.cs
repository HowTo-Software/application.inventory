using System.Collections.Concurrent;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Tracks failed login attempts per username and enforces progressive lockout
/// to protect against brute-force attacks. Complements the general IP-based
/// rate limiting middleware with per-account protection.
/// </summary>
public interface ILoginRateLimiter
{
    /// <summary>
    /// Returns true if the account is currently locked out due to too many failures.
    /// When locked, <paramref name="retryAfterSeconds"/> indicates when the lockout expires.
    /// </summary>
    bool IsLockedOut(string username, out int retryAfterSeconds);

    /// <summary>
    /// Records a failed login attempt for the given username.
    /// </summary>
    void RecordFailure(string username);

    /// <summary>
    /// Resets the failure counter on successful login.
    /// </summary>
    void RecordSuccess(string username);
}

/// <summary>
/// In-memory implementation of login brute-force protection.
/// Progressive lockout: 5 failures → 1 min, 10 → 5 min, 15 → 15 min, 20+ → 30 min.
/// </summary>
public sealed class LoginRateLimiter : ILoginRateLimiter, IDisposable
{
    private readonly ConcurrentDictionary<string, LoginAttemptEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer _cleanupTimer;
    private readonly ILogger<LoginRateLimiter> _logger;

    public LoginRateLimiter(ILogger<LoginRateLimiter> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public bool IsLockedOut(string username, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;

        if (!_entries.TryGetValue(username, out var entry))
            return false;

        lock (entry)
        {
            if (entry.LockoutUntil is null || entry.LockoutUntil <= DateTime.UtcNow)
                return false;

            retryAfterSeconds = Math.Max(1, (int)(entry.LockoutUntil.Value - DateTime.UtcNow).TotalSeconds);
            return true;
        }
    }

    public void RecordFailure(string username)
    {
        var entry = _entries.GetOrAdd(username, _ => new LoginAttemptEntry());

        lock (entry)
        {
            // Reset counter if the previous lockout has fully expired
            if (entry.LockoutUntil is not null && entry.LockoutUntil <= DateTime.UtcNow)
            {
                // Keep the accumulated count so lockout escalates across windows
            }

            entry.FailureCount++;
            entry.LastAttempt = DateTime.UtcNow;

            var lockoutDuration = GetLockoutDuration(entry.FailureCount);
            if (lockoutDuration is not null)
            {
                entry.LockoutUntil = DateTime.UtcNow.Add(lockoutDuration.Value);
                _logger.LogWarning(
                    "Account '{Username}' locked out for {Seconds}s after {Failures} failed login attempts",
                    username, (int)lockoutDuration.Value.TotalSeconds, entry.FailureCount);
            }
        }
    }

    public void RecordSuccess(string username)
    {
        _entries.TryRemove(username, out _);
    }

    private static TimeSpan? GetLockoutDuration(int failureCount) => failureCount switch
    {
        >= 20 => TimeSpan.FromMinutes(30),
        >= 15 => TimeSpan.FromMinutes(15),
        >= 10 => TimeSpan.FromMinutes(5),
        >= 5 => TimeSpan.FromMinutes(1),
        _ => null
    };

    private void CleanupExpiredEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-60);
        foreach (var kvp in _entries)
        {
            if (kvp.Value.LastAttempt < cutoff)
                _entries.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }

    private class LoginAttemptEntry
    {
        public int FailureCount;
        public DateTime LastAttempt = DateTime.UtcNow;
        public DateTime? LockoutUntil;
    }
}
