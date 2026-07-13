using System.Reflection;
using System.Text.Json;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Caches version file info (agent-version.txt, update.json, checksum.txt, package metadata)
/// to avoid disk I/O on every API call. Uses FileSystemWatcher to auto-invalidate on changes.
/// </summary>
public interface IVersionInfoCacheService
{
    /// <summary>
    /// Gets the cached thin client version info. Reads from disk only on first call or after invalidation.
    /// </summary>
    CachedThinClientVersionInfo GetThinClientVersionInfo();

    /// <summary>
    /// Gets the cached server version info (assembly version + build date).
    /// </summary>
    CachedServerVersionInfo GetServerVersionInfo();

    /// <summary>
    /// Forces a reload of thin client version info from disk.
    /// </summary>
    void Invalidate();
}

public class VersionInfoCacheService : IVersionInfoCacheService, IDisposable
{
    private readonly ILogger<VersionInfoCacheService> _logger;
    private readonly AgentFileSettings _fileSettings;
    private readonly string _agentDir;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _lock = new();
    private FileSystemWatcher? _fileWatcher;

    private CachedThinClientVersionInfo? _cachedThinClientInfo;
    private CachedServerVersionInfo? _cachedServerInfo;
    private DateTime _lastLoaded = DateTime.MinValue;
    private bool _disposed;

    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(1);

    public VersionInfoCacheService(ILogger<VersionInfoCacheService> logger, AgentFileSettings fileSettings)
    {
        _logger = logger;
        _fileSettings = fileSettings;
        _agentDir = _fileSettings.GetAgentPath();
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        SetupFileWatcher();
        LoadThinClientInfo();
    }

    private void SetupFileWatcher()
    {
        if (!Directory.Exists(_agentDir))
        {
            _logger.LogInformation("VersionInfoCacheService: Agent directory {Path} does not exist yet", _agentDir);
            return;
        }

        try
        {
            _fileWatcher = new FileSystemWatcher(_agentDir)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileChanged;
            _fileWatcher.Deleted += OnFileChanged;
            _fileWatcher.Renamed += OnFileRenamed;

            _logger.LogInformation("VersionInfoCacheService: Watching {Path} for changes", _agentDir);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set up file watcher for agent directory, falling back to periodic refresh");
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsRelevantFile(e.Name))
        {
            _logger.LogDebug("Version file changed: {Name} ({ChangeType}), invalidating cache", e.Name, e.ChangeType);
            Invalidate();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsRelevantFile(e.Name) || IsRelevantFile(e.OldName))
        {
            _logger.LogDebug("Version file renamed: {OldName} -> {Name}, invalidating cache", e.OldName, e.Name);
            Invalidate();
        }
    }

    private bool IsRelevantFile(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        return fileName.Equals(_fileSettings.VersionFileName, StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(_fileSettings.UpdateJsonFileName, StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(_fileSettings.ChecksumFileName, StringComparison.OrdinalIgnoreCase)
            || fileName.Equals(_fileSettings.InstallerFileName, StringComparison.OrdinalIgnoreCase);
    }

    public CachedThinClientVersionInfo GetThinClientVersionInfo()
    {
        if (_disposed) return new CachedThinClientVersionInfo();

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_cachedThinClientInfo == null || now - _lastLoaded > StaleThreshold)
            {
                LoadThinClientInfo();
            }

            return _cachedThinClientInfo ?? new CachedThinClientVersionInfo();
        }
    }

    public CachedServerVersionInfo GetServerVersionInfo()
    {
        if (_disposed) return new CachedServerVersionInfo();

        // Server info never changes at runtime — cache once
        if (_cachedServerInfo != null) return _cachedServerInfo;

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var buildDate = File.GetLastWriteTime(assembly.Location);

        _cachedServerInfo = new CachedServerVersionInfo
        {
            Version = version,
            BuildDate = buildDate
        };

        return _cachedServerInfo;
    }

    public void Invalidate()
    {
        if (_disposed) return;

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (now - _lastLoaded < MinRefreshInterval)
            {
                _logger.LogDebug("Skipping version info cache refresh (too soon)");
                return;
            }

            LoadThinClientInfo();
        }
    }

    private void LoadThinClientInfo()
    {
        var info = new CachedThinClientVersionInfo();

        try
        {
            var versionFile = _fileSettings.GetVersionFilePath();
            var updateJsonFile = _fileSettings.GetUpdateJsonPath();

            // Try agent-version.txt first
            if (File.Exists(versionFile))
            {
                info.Version = File.ReadAllText(versionFile).Trim();
                info.ReleasedAt = File.GetLastWriteTimeUtc(versionFile);
            }
            // Fall back to update/update.json
            else if (File.Exists(updateJsonFile))
            {
                try
                {
                    var updateInfo = UpdateJsonHelper.ParseUpdateJson(updateJsonFile);
                    if (updateInfo != null)
                    {
                        info.Version = updateInfo.Version ?? "Unknown";
                        info.Checksum = updateInfo.Checksum;
                        info.FileSize = updateInfo.FileSize;
                        info.ReleasedAt = updateInfo.ReleasedAt;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse update.json");
                }
            }

            // Check package file for size/checksum
            var packagePath = _fileSettings.GetInstallerPath();
            if (File.Exists(packagePath) && !info.FileSize.HasValue)
            {
                var fileInfo = new FileInfo(packagePath);
                info.FileSize = fileInfo.Length;

                var checksumFile = _fileSettings.GetChecksumFilePath();
                if (File.Exists(checksumFile) && string.IsNullOrEmpty(info.Checksum))
                {
                    info.Checksum = File.ReadAllText(checksumFile).Trim();
                }
            }

            _logger.LogDebug("Loaded thin client version info: Version={Version}", info.Version);
        }
        catch (IOException ex) when (ex.Message.Contains("being used by another process"))
        {
            _logger.LogDebug("Version file is locked, will retry later");
            return; // Keep previous cached value
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load version info from disk");
        }

        _cachedThinClientInfo = info;
        _lastLoaded = DateTime.UtcNow;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Created -= OnFileChanged;
            _fileWatcher.Deleted -= OnFileChanged;
            _fileWatcher.Renamed -= OnFileRenamed;
            _fileWatcher.Dispose();
        }
    }


}

public class CachedThinClientVersionInfo
{
    public string Version { get; set; } = "Unknown";
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class CachedServerVersionInfo
{
    public string Version { get; set; } = "1.0.0.0";
    public DateTime BuildDate { get; set; }
}
