using System.Text.Json;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Caches agent update.json information to avoid disk I/O on every heartbeat.
/// Uses file watcher to automatically invalidate cache when update.json changes.
/// </summary>
public interface IUpdateInfoCacheService
{
    /// <summary>
    /// Gets the cached update information. Returns null if no update file exists.
    /// </summary>
    AgentUpdateInfo? GetUpdateInfo();
    
    /// <summary>
    /// Forces a reload of the update information from disk.
    /// </summary>
    void Invalidate();
}

public class UpdateInfoCacheService : IUpdateInfoCacheService, IDisposable
{
    private readonly ILogger<UpdateInfoCacheService> _logger;
    private readonly string _updateInfoPath;
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly object _lock = new();
    private readonly JsonSerializerOptions _jsonOptions;
    
    private AgentUpdateInfo? _cachedInfo;
    private DateTime _lastLoaded = DateTime.MinValue;
    private bool _disposed;
    
    // Minimum time between cache refreshes (prevents disk thrashing)
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(5);

    public UpdateInfoCacheService(ILogger<UpdateInfoCacheService> logger, AgentFileSettings fileSettings)
    {
        _logger = logger;
        
        var updatePath = fileSettings.GetUpdatePath();
        _updateInfoPath = fileSettings.GetUpdateJsonPath();
        
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        
        // Set up file watcher to invalidate cache when update.json changes
        if (Directory.Exists(updatePath))
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(updatePath, Path.GetFileName(_updateInfoPath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                
                _fileWatcher.Changed += OnUpdateFileChanged;
                _fileWatcher.Created += OnUpdateFileChanged;
                _fileWatcher.Deleted += OnUpdateFileChanged;
                
                _logger.LogInformation("UpdateInfoCacheService: Watching {Path} for changes", _updateInfoPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set up file watcher for update.json, falling back to periodic refresh");
            }
        }
        else
        {
            _logger.LogInformation("UpdateInfoCacheService: Update path {Path} does not exist yet", updatePath);
        }
        
        // Initial load
        LoadUpdateInfo();
    }

    private void OnUpdateFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Update file changed ({ChangeType}), invalidating cache", e.ChangeType);
        Invalidate();
    }

    public AgentUpdateInfo? GetUpdateInfo()
    {
        if (_disposed) return null;
        
        lock (_lock)
        {
            // Check if we need to refresh (file watcher might have missed something)
            var now = DateTime.UtcNow;
            if (_cachedInfo == null || now - _lastLoaded > TimeSpan.FromMinutes(1))
            {
                LoadUpdateInfo();
            }
            
            return _cachedInfo;
        }
    }

    public void Invalidate()
    {
        if (_disposed) return;
        
        lock (_lock)
        {
            // Debounce - don't refresh too often
            var now = DateTime.UtcNow;
            if (now - _lastLoaded < MinRefreshInterval)
            {
                _logger.LogDebug("Skipping cache refresh (too soon)");
                return;
            }
            
            LoadUpdateInfo();
        }
    }

    private void LoadUpdateInfo()
    {
        try
        {
            if (!File.Exists(_updateInfoPath))
            {
                _cachedInfo = null;
                _lastLoaded = DateTime.UtcNow;
                _logger.LogDebug("update.json not found at {Path}", _updateInfoPath);
                return;
            }
            
            _cachedInfo = UpdateJsonHelper.ParseUpdateJson(_updateInfoPath);
            _lastLoaded = DateTime.UtcNow;
            
            _logger.LogInformation("Loaded update info: Version={Version}", _cachedInfo?.Version ?? "null");
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
        {
            // File is being written to, retry later
            _logger.LogDebug("update.json is locked, will retry");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load update.json");
            _cachedInfo = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _fileWatcher?.Dispose();
    }
}


