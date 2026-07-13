using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Redis-backed distributed implementation of ICacheService.
/// Falls back to allowing factory calls if Redis is temporarily unavailable.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly CacheServiceOptions _options;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private long _hits;
    private long _misses;

    public RedisCacheService(IDistributedCache cache, ILogger<RedisCacheService> logger, CacheServiceOptions options)
    {
        _cache = cache;
        _logger = logger;
        _options = options;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var effectiveExpiration = expiration ?? _options.DefaultExpiration;

        try
        {
            var cached = await _cache.GetStringAsync(key);
            if (cached != null)
            {
                Interlocked.Increment(ref _hits);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(cached, _jsonOptions)!;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis read failed for key {Key}; falling through to factory", key);
        }

        Interlocked.Increment(ref _misses);
        _logger.LogDebug("Cache miss for key: {Key}", key);

        var value = await factory();

        try
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = effectiveExpiration
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis write failed for key {Key}", key);
        }

        return value;
    }

    public void Invalidate(string key)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Cache entry invalidated: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis remove failed for key {Key}", key);
        }
    }

    public void InvalidatePrefix(string prefix)
    {
        // IDistributedCache does not support key scanning natively.
        // For prefix invalidation with Redis, we track keys by prefix in a Redis set.
        // As a pragmatic fallback, we invalidate well-known keys matching the prefix.
        var knownKeys = GetKnownKeysForPrefix(prefix);
        var removed = 0;

        foreach (var key in knownKeys)
        {
            try
            {
                _cache.Remove(key);
                removed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis remove failed for key {Key} during prefix invalidation", key);
            }
        }

        if (removed > 0)
        {
            _logger.LogDebug("Invalidated {Count} cache entries with prefix: {Prefix}", removed, prefix);
        }
    }

    public void Clear()
    {
        // IDistributedCache has no Clear(); invalidate all well-known keys.
        var allPrefixes = new[] { CacheKeys.PrefixComputer, CacheKeys.PrefixComputers, CacheKeys.PrefixLookup, "map:", "dashboard:" };
        var totalRemoved = 0;

        foreach (var prefix in allPrefixes)
        {
            var keys = GetKnownKeysForPrefix(prefix);
            foreach (var key in keys)
            {
                try { _cache.Remove(key); totalRemoved++; } catch { /* best effort */ }
            }
        }

        _logger.LogInformation("Cache clear requested. Removed {Count} known entries", totalRemoved);
    }

    public CacheStats GetStats()
    {
        return new CacheStats
        {
            EntryCount = -1, // Cannot determine entry count from IDistributedCache
            Hits = _hits,
            Misses = _misses,
            HitRate = _hits + _misses > 0
                ? (double)_hits / (_hits + _misses) * 100
                : 0
        };
    }

    /// <summary>
    /// Returns well-known cache keys that start with the given prefix.
    /// This enables prefix invalidation without Redis SCAN commands.
    /// </summary>
    private static List<string> GetKnownKeysForPrefix(string prefix)
    {
        var keys = new List<string>();

        if (prefix == CacheKeys.PrefixComputers || prefix == CacheKeys.PrefixComputer)
        {
            keys.Add(CacheKeys.ComputerStats);
            keys.Add(CacheKeys.ComputerList);
        }

        if (prefix == CacheKeys.PrefixLookup || prefix.StartsWith("lookup:", StringComparison.OrdinalIgnoreCase))
        {
            keys.Add(CacheKeys.LookupComputerTypes);
            keys.Add(CacheKeys.LookupComputerStatuses);
            keys.Add(CacheKeys.LookupMaintenanceTypes);
            keys.Add(CacheKeys.LookupMaintenanceStatuses);
            keys.Add(CacheKeys.LookupUserRoles);
            keys.Add(CacheKeys.LookupVersionTypes);
            keys.Add(CacheKeys.LookupDepartments);
            keys.Add(CacheKeys.LookupLocations);
            keys.Add(CacheKeys.LookupManufacturers);
            keys.Add(CacheKeys.LookupOperatingSystems);
            keys.Add(CacheKeys.LookupLocationTypes);
            keys.Add(CacheKeys.LookupDepartmentTypes);
            keys.Add(CacheKeys.LookupManufacturerTypes);
            keys.Add(CacheKeys.LookupModels(null));
        }

        if (prefix.StartsWith("map:", StringComparison.OrdinalIgnoreCase))
        {
            keys.Add(CacheKeys.MapLocations);
            keys.Add(CacheKeys.MapLastRefresh);
        }

        if (prefix.StartsWith("dashboard:", StringComparison.OrdinalIgnoreCase))
        {
            keys.Add("dashboard:overview");
        }

        return keys;
    }
}
