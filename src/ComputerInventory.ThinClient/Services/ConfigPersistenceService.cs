using System.Text.Json;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Service to persist and restore agent configuration across updates and reinstalls.
/// Saves configuration to a well-known location outside the install directory so it survives upgrades.
/// On startup, checks for a saved config file and restores settings to appsettings.json.
/// </summary>
public interface IConfigPersistenceService
{
    /// <summary>
    /// Save current configuration to the persistence location.
    /// Call this before an update to preserve settings.
    /// </summary>
    Task SaveConfigurationAsync();
    
    /// <summary>
    /// Check if there's a persisted configuration file and restore it.
    /// Call this on startup to restore settings after an update/reinstall.
    /// </summary>
    /// <returns>True if configuration was restored</returns>
    Task<bool> RestoreConfigurationAsync();
    
    /// <summary>
    /// Check if a persisted configuration exists
    /// </summary>
    bool HasPersistedConfiguration();
}

public class ConfigPersistenceService : IConfigPersistenceService
{
    private readonly ILogger<ConfigPersistenceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _persistencePath;
    private readonly string _appSettingsPath;
    
    private const string ConfigFileName = "agent-config-backup.json";

    public ConfigPersistenceService(ILogger<ConfigPersistenceService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Store in ProgramData which is accessible to SYSTEM account and persists across updates
        _persistencePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ComputerInventory",
            ConfigFileName);
        
        // The appsettings.json in the install directory
        _appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        
        // Ensure directory exists
        var dir = Path.GetDirectoryName(_persistencePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public bool HasPersistedConfiguration()
    {
        return File.Exists(_persistencePath);
    }

    public async Task SaveConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Saving configuration to {Path}", _persistencePath);
            
            // Read current appsettings.json
            if (!File.Exists(_appSettingsPath))
            {
                _logger.LogWarning("appsettings.json not found at {Path}", _appSettingsPath);
                return;
            }

            var currentConfig = await File.ReadAllTextAsync(_appSettingsPath);
            
            // Parse and add metadata
            var configDoc = JsonDocument.Parse(currentConfig);
            var persistedConfig = new PersistedConfiguration
            {
                SavedAt = DateTime.UtcNow,
                AgentVersion = typeof(ConfigPersistenceService).Assembly.GetName().Version?.ToString() ?? "unknown",
                MachineName = Environment.MachineName,
                Configuration = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentConfig) 
                    ?? new Dictionary<string, JsonElement>()
            };

            var json = JsonSerializer.Serialize(persistedConfig, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(_persistencePath, json);
            _logger.LogInformation("Configuration saved successfully. ApiSettings.BaseUrl: {Url}", 
                _configuration["ApiSettings:BaseUrl"]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
        }
    }

    public async Task<bool> RestoreConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_persistencePath))
            {
                _logger.LogDebug("No persisted configuration found at {Path}", _persistencePath);
                return false;
            }

            _logger.LogInformation("Found persisted configuration at {Path}", _persistencePath);
            
            var json = await File.ReadAllTextAsync(_persistencePath);
            var persisted = JsonSerializer.Deserialize<PersistedConfiguration>(json);
            
            if (persisted?.Configuration == null)
            {
                _logger.LogWarning("Persisted configuration is empty or invalid");
                await DeletePersistedConfigAsync();
                return false;
            }

            _logger.LogInformation("Restoring configuration saved at {SavedAt} from agent v{Version} on {Machine}",
                persisted.SavedAt, persisted.AgentVersion, persisted.MachineName);

            // Read current appsettings to merge (preserve any new settings from update)
            Dictionary<string, JsonElement> currentConfig;
            if (File.Exists(_appSettingsPath))
            {
                var currentJson = await File.ReadAllTextAsync(_appSettingsPath);
                currentConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(currentJson) 
                    ?? new Dictionary<string, JsonElement>();
            }
            else
            {
                currentConfig = new Dictionary<string, JsonElement>();
            }

            // Merge persisted ApiSettings and AgentSettings into current config
            // These are the user-configurable settings we want to preserve
            var sectionsToRestore = new[] { "ApiSettings", "AgentSettings", "FileLogging" };
            
            foreach (var section in sectionsToRestore)
            {
                if (persisted.Configuration.TryGetValue(section, out var savedSection))
                {
                    currentConfig[section] = savedSection;
                    _logger.LogDebug("Restored section: {Section}", section);
                }
            }

            // Write merged config back
            var mergedJson = JsonSerializer.Serialize(currentConfig, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(_appSettingsPath, mergedJson);
            _logger.LogInformation("Configuration restored successfully");

            // Delete the persisted config file after successful restore
            await DeletePersistedConfigAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore configuration");
            return false;
        }
    }

    private async Task DeletePersistedConfigAsync()
    {
        try
        {
            if (File.Exists(_persistencePath))
            {
                File.Delete(_persistencePath);
                _logger.LogInformation("Deleted persisted configuration file");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete persisted configuration file");
        }
        await Task.CompletedTask;
    }
}

/// <summary>
/// Model for the persisted configuration file
/// </summary>
public class PersistedConfiguration
{
    public DateTime SavedAt { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public Dictionary<string, JsonElement> Configuration { get; set; } = new();
}
