using System.Text.Json;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Local cache service that stores data when server is unavailable.
/// Data is queued locally and synced when connection is restored.
/// </summary>
public interface ILocalCacheService
{
    Task SavePendingDataAsync<T>(string dataType, T data);
    Task SavePendingDataAsync<T>(string dataType, string id, T data);  // Overload with explicit ID
    Task<List<CachedItem<T>>> GetPendingDataAsync<T>(string dataType);
    Task RemovePendingDataAsync(string dataType, string id);
    Task ClearPendingDataAsync(string dataType);
    Task<int> GetPendingCountAsync(string dataType);
    Task SaveLastKnownStateAsync(string key, string value);
    Task<string?> GetLastKnownStateAsync(string key);
}

public class CachedItem<T>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public T? Data { get; set; }
    public int RetryCount { get; set; }
}

public class LocalCacheService : ILocalCacheService
{
    private readonly ILogger<LocalCacheService> _logger;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalCacheService(ILogger<LocalCacheService> logger)
    {
        _logger = logger;
        
        // Store cache in AppData to persist across updates
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(appData, "ComputerInventoryAgent", "cache");
        
        Directory.CreateDirectory(_cacheDirectory);
        _logger.LogInformation("Local cache directory: {Dir}", _cacheDirectory);
    }

    public async Task SavePendingDataAsync<T>(string dataType, T data)
    {
        await SavePendingDataAsync(dataType, Guid.NewGuid().ToString(), data);
    }

    public async Task SavePendingDataAsync<T>(string dataType, string id, T data)
    {
        await _lock.WaitAsync();
        try
        {
            var item = new CachedItem<T>
            {
                Id = id,
                Data = data,
                CachedAt = DateTime.UtcNow
            };

            var typeDir = Path.Combine(_cacheDirectory, dataType);
            Directory.CreateDirectory(typeDir);

            var filePath = Path.Combine(typeDir, $"{item.Id}.json");
            var json = JsonSerializer.Serialize(item, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("Cached {Type} data: {Id}", dataType, item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache {Type} data", dataType);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<CachedItem<T>>> GetPendingDataAsync<T>(string dataType)
    {
        var items = new List<CachedItem<T>>();

        await _lock.WaitAsync();
        try
        {
            var typeDir = Path.Combine(_cacheDirectory, dataType);
            if (!Directory.Exists(typeDir))
                return items;

            foreach (var file in Directory.GetFiles(typeDir, "*.json").OrderBy(f => f))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var item = JsonSerializer.Deserialize<CachedItem<T>>(json);
                    if (item != null)
                        items.Add(item);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read cache file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending {Type} data", dataType);
        }
        finally
        {
            _lock.Release();
        }

        return items;
    }

    public async Task RemovePendingDataAsync(string dataType, string id)
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_cacheDirectory, dataType, $"{id}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Removed cached {Type} data: {Id}", dataType, id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cached {Type} data: {Id}", dataType, id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearPendingDataAsync(string dataType)
    {
        await _lock.WaitAsync();
        try
        {
            var typeDir = Path.Combine(_cacheDirectory, dataType);
            if (Directory.Exists(typeDir))
            {
                Directory.Delete(typeDir, true);
                Directory.CreateDirectory(typeDir);
                _logger.LogInformation("Cleared all cached {Type} data", dataType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cached {Type} data", dataType);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetPendingCountAsync(string dataType)
    {
        await _lock.WaitAsync();
        try
        {
            var typeDir = Path.Combine(_cacheDirectory, dataType);
            if (!Directory.Exists(typeDir))
                return 0;
            
            return Directory.GetFiles(typeDir, "*.json").Length;
        }
        catch
        {
            return 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveLastKnownStateAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            var stateDir = Path.Combine(_cacheDirectory, "_state");
            Directory.CreateDirectory(stateDir);
            
            var filePath = Path.Combine(stateDir, $"{key}.txt");
            await File.WriteAllTextAsync(filePath, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state: {Key}", key);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetLastKnownStateAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_cacheDirectory, "_state", $"{key}.txt");
            if (File.Exists(filePath))
                return await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get state: {Key}", key);
        }
        finally
        {
            _lock.Release();
        }
        
        return null;
    }
}
