using System.Collections.Concurrent;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Simple in-memory caching service for expensive API operations.
/// Uses lazy loading and automatic expiration.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets or creates a cached value. If the key doesn't exist or has expired,
    /// the factory function is called to create the value.
    /// </summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

    /// <summary>
    /// Invalidates a specific cache key.
    /// </summary>
    void Invalidate(string key);

    /// <summary>
    /// Invalidates all cache entries matching a prefix.
    /// </summary>
    void InvalidatePrefix(string prefix);

    /// <summary>
    /// Clears all cache entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStats GetStats();
}

/// <summary>
/// LRU-based in-memory implementation of ICacheService.
/// Uses a dictionary + doubly-linked list for O(1) get, put, and eviction.
/// </summary>
public class InMemoryCacheService : ICacheService, IDisposable
{
    private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new();
    private readonly LinkedList<CacheEntry> _lruList = new();
    private readonly object _lock = new();
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly CacheServiceOptions _options;
    private readonly Timer _cleanupTimer;
    private bool _disposed;
    
    private long _hits;
    private long _misses;

    public InMemoryCacheService(ILogger<InMemoryCacheService> logger, CacheServiceOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new CacheServiceOptions();
        
        // Schedule periodic cleanup of expired entries
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, 
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
    {
        var effectiveExpiration = expiration ?? _options.DefaultExpiration;

        // Try to get existing entry
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node) && !node.Value.IsExpired)
            {
                Interlocked.Increment(ref _hits);
                _logger.LogDebug("Cache hit for key: {Key}", key);
                // Promote to most-recently-used
                _lruList.Remove(node);
                _lruList.AddLast(node);
                return (T)node.Value.Value!;
            }
        }

        // Cache miss - create new value
        Interlocked.Increment(ref _misses);
        _logger.LogDebug("Cache miss for key: {Key}", key);

        var value = await factory();
        
        var newEntry = new CacheEntry(key, value, DateTime.UtcNow.Add(effectiveExpiration));

        lock (_lock)
        {
            // Remove old entry if it exists (expired or being replaced)
            if (_cache.TryGetValue(key, out var existingNode))
            {
                _lruList.Remove(existingNode);
                _cache.Remove(key);
            }

            var newNode = _lruList.AddLast(newEntry);
            _cache[key] = newNode;

            // Evict LRU entries if over limit
            while (_cache.Count > _options.MaxEntries)
            {
                var lruNode = _lruList.First;
                if (lruNode == null) break;
                _lruList.RemoveFirst();
                _cache.Remove(lruNode.Value.Key);
            }
        }

        return value;
    }

    public void Invalidate(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _cache.Remove(key);
                _logger.LogDebug("Cache entry invalidated: {Key}", key);
            }
        }
    }

    public void InvalidatePrefix(string prefix)
    {
        lock (_lock)
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                }
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogDebug("Invalidated {Count} cache entries with prefix: {Prefix}", 
                    keysToRemove.Count, prefix);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            var count = _cache.Count;
            _cache.Clear();
            _lruList.Clear();
            _logger.LogInformation("Cache cleared. Removed {Count} entries", count);
        }
    }

    public CacheStats GetStats()
    {
        lock (_lock)
        {
            return new CacheStats
            {
                EntryCount = _cache.Count,
                Hits = _hits,
                Misses = _misses,
                HitRate = _hits + _misses > 0 
                    ? (double)_hits / (_hits + _misses) * 100 
                    : 0
            };
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        lock (_lock)
        {
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_cache.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cache.Remove(key);
                }
            }

            if (expiredKeys.Count > 0)
            {
                _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }

    private class CacheEntry
    {
        public string Key { get; }
        public object? Value { get; }
        public DateTime ExpiresAt { get; }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        public CacheEntry(string key, object? value, DateTime expiresAt)
        {
            Key = key;
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}

/// <summary>
/// Cache statistics.
/// </summary>
public class CacheStats
{
    public int EntryCount { get; set; }
    public long Hits { get; set; }
    public long Misses { get; set; }
    public double HitRate { get; set; }
}

/// <summary>
/// Configuration options for the cache service.
/// </summary>
public class CacheServiceOptions
{
    /// <summary>
    /// Default cache entry expiration.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum number of cache entries before eviction.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;

    /// <summary>
    /// Cache duration for the computer list endpoint.
    /// </summary>
    public int ComputerListCacheSeconds { get; set; } = 15;

    /// <summary>
    /// Cache duration for inventory stats.
    /// </summary>
    public int ComputerStatsCacheSeconds { get; set; } = 30;

    /// <summary>
    /// Cache duration for the dashboard overview.
    /// </summary>
    public int DashboardCacheSeconds { get; set; } = 15;

    /// <summary>
    /// Cache duration for map locations.
    /// </summary>
    public int MapLocationsCacheSeconds { get; set; } = 30;

    /// <summary>
    /// Cache duration for lookup data (departments, locations, manufacturers, etc.).
    /// </summary>
    public int LookupCacheSeconds { get; set; } = 300;
}

/// <summary>
/// Cache keys used throughout the application.
/// </summary>
public static class CacheKeys
{
    // Prefixes used for grouping and bulk invalidation
    public const string PrefixComputer = "computer:";
    public const string PrefixComputers = "computers:";

    public const string ComputerStats = $"{PrefixComputers}stats";
    public const string ComputerList = $"{PrefixComputers}list";
    public const string MapLocations = "map:locations";
    public const string MapLastRefresh = "map:lastrefresh";
    
    public static string ComputerById(int id) => $"{PrefixComputer}{id}";
    public static string ComputerBySerial(string serial) => $"{PrefixComputer}serial:{serial}";
    public static string ComputersByStatus(string status) => $"{PrefixComputers}status:{status}";
    public static string ComputersByLocation(string location) => $"{PrefixComputers}location:{location}";
    public static string ComputersByDepartment(string dept) => $"{PrefixComputers}department:{dept}";

    // Lookup data
    public const string PrefixLookup = "lookup:";
    public const string LookupComputerTypes = $"{PrefixLookup}computer-types";
    public const string LookupComputerStatuses = $"{PrefixLookup}computer-statuses";
    public const string LookupMaintenanceTypes = $"{PrefixLookup}maintenance-types";
    public const string LookupMaintenanceStatuses = $"{PrefixLookup}maintenance-statuses";
    public const string LookupUserRoles = $"{PrefixLookup}user-roles";
    public const string LookupVersionTypes = $"{PrefixLookup}version-types";
    public const string LookupDepartments = $"{PrefixLookup}departments";
    public const string LookupLocations = $"{PrefixLookup}locations";
    public const string LookupLocationTypes = $"{PrefixLookup}location-types";
    public const string LookupDepartmentTypes = $"{PrefixLookup}department-types";
    public const string LookupManufacturerTypes = $"{PrefixLookup}manufacturer-types";
    public const string LookupManufacturers = $"{PrefixLookup}manufacturers";
    public const string LookupOperatingSystems = $"{PrefixLookup}operating-systems";
    public static string LookupModels(int? manufacturerId) => manufacturerId.HasValue
        ? $"{PrefixLookup}models:{manufacturerId}"
        : $"{PrefixLookup}models:all";
}
