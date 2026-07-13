using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;

namespace ComputerInventory.Api.Middleware;

/// <summary>
/// Result of a rate limit check.
/// </summary>
public record RateLimitResult(
    bool IsLimited,
    int RequestCount,
    int Limit,
    int RetryAfterSeconds,
    long ResetTimeUnix);

/// <summary>
/// Abstracts rate limit state storage so it can be backed by in-memory
/// (single instance) or a distributed cache (multi-instance deployments).
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Atomically checks whether the client has exceeded the rate limit
    /// and increments the request count if not.
    /// </summary>
    Task<RateLimitResult> CheckAndIncrementAsync(string clientKey, int maxRequests, int windowSeconds);
}

/// <summary>
/// In-memory rate limit store using ConcurrentDictionary.
/// Suitable for single-instance deployments.
/// </summary>
public sealed class InMemoryRateLimitStore : IRateLimitStore, IDisposable
{
    private readonly ConcurrentDictionary<string, WindowEntry> _entries = new();
    private readonly Timer _cleanupTimer;

    public InMemoryRateLimitStore()
    {
        _cleanupTimer = new Timer(CleanupExpiredEntries, null,
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<RateLimitResult> CheckAndIncrementAsync(string clientKey, int maxRequests, int windowSeconds)
    {
        var entry = _entries.GetOrAdd(clientKey, _ => new WindowEntry());
        RateLimitResult result;

        lock (entry)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if ((now - entry.WindowStart).TotalSeconds >= windowSeconds)
            {
                entry.WindowStart = now;
                entry.Count = 0;
            }

            entry.LastAccess = now;
            var resetTime = new DateTimeOffset(entry.WindowStart.AddSeconds(windowSeconds), TimeSpan.Zero);

            if (entry.Count >= maxRequests)
            {
                var retryAfter = Math.Max(1, (int)(resetTime - DateTimeOffset.UtcNow).TotalSeconds);
                result = new RateLimitResult(true, entry.Count, maxRequests, retryAfter, resetTime.ToUnixTimeSeconds());
            }
            else
            {
                entry.Count++;
                result = new RateLimitResult(false, entry.Count, maxRequests, 0, resetTime.ToUnixTimeSeconds());
            }
        }

        return Task.FromResult(result);
    }

    private void CleanupExpiredEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-5);
        foreach (var kvp in _entries)
        {
            if (kvp.Value.LastAccess < cutoff)
                _entries.TryRemove(kvp.Key, out _);
        }
    }

    public void Dispose() => _cleanupTimer.Dispose();

    private sealed class WindowEntry
    {
        public DateTime WindowStart = DateTime.UtcNow;
        public int Count;
        public DateTime LastAccess = DateTime.UtcNow;
    }
}

/// <summary>
/// Distributed rate limit store backed by IDistributedCache (e.g. Redis).
/// Each time window is stored as a separate cache entry with automatic expiry,
/// so all server instances share the same counters.
/// </summary>
public sealed class DistributedRateLimitStore : IRateLimitStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedRateLimitStore> _logger;

    public DistributedRateLimitStore(IDistributedCache cache, ILogger<DistributedRateLimitStore> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<RateLimitResult> CheckAndIncrementAsync(string clientKey, int maxRequests, int windowSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var windowBucket = now.ToUnixTimeSeconds() / windowSeconds;
        var cacheKey = $"rl:{clientKey}:{windowBucket}";
        var resetUnix = (windowBucket + 1) * windowSeconds;
        var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetUnix);

        int count;
        try
        {
            var data = await _cache.GetStringAsync(cacheKey);
            count = data != null && int.TryParse(data, out var parsed) ? parsed : 0;
        }
        catch (Exception ex)
        {
            // Fail open — if the cache is unavailable, allow the request
            _logger.LogWarning(ex, "Rate limit store read failed for {ClientKey}; allowing request", clientKey);
            return new RateLimitResult(false, 0, maxRequests, 0, resetUnix);
        }

        if (count >= maxRequests)
        {
            var retryAfter = Math.Max(1, (int)(resetTime - now).TotalSeconds);
            return new RateLimitResult(true, count, maxRequests, retryAfter, resetUnix);
        }

        count++;
        try
        {
            await _cache.SetStringAsync(cacheKey, count.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = resetTime.AddSeconds(windowSeconds)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rate limit store write failed for {ClientKey}", clientKey);
        }

        return new RateLimitResult(false, count, maxRequests, 0, resetUnix);
    }
}
