namespace ComputerInventory.ThinClient.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken);
    Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken);
    string GetCurrentVersion();
}

public class UpdateInfo
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
