using System.Text.Json;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Agent update information from update.json.
/// Also used as the API response model for check-update endpoints.
/// </summary>
public class AgentUpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }

    // Reinstall support - when true, the Updater will perform a full reinstall instead of update
    public bool ReinstallRequired { get; set; }
    public string? InstallScriptUrl { get; set; }
}

/// <summary>
/// Centralized helper for reading, writing, and locating the agent update.json file.
/// Eliminates duplicated parsing logic across controllers and services.
/// </summary>
public static class UpdateJsonHelper
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Returns the default path to the agent update.json file.
    /// If settings are available, uses configured paths; otherwise falls back to hardcoded defaults.
    /// </summary>
    public static string GetDefaultUpdateJsonPath(AgentFileSettings? settings = null)
    {
        return settings?.GetUpdateJsonPath()
            ?? Path.Combine(AppContext.BaseDirectory, "agent", "update", "update.json");
    }

    /// <summary>
    /// Reads and deserializes update.json from the given file path.
    /// Returns null if the file does not exist or cannot be parsed.
    /// </summary>
    public static AgentUpdateInfo? ParseUpdateJson(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<AgentUpdateInfo>(json, ReadOptions);
    }

    /// <summary>
    /// Serializes and writes an AgentUpdateInfo to the given file path.
    /// Creates the parent directory if it does not exist.
    /// </summary>
    public static async Task WriteUpdateJsonAsync(string filePath, AgentUpdateInfo info, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null)
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(info, WriteOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }
}
