namespace ComputerInventory.Contracts;

/// <summary>
/// Real-time update progress message pushed via SignalR.
/// </summary>
public class UpdateProgressMessage
{
    public int ComputerId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public int UpdateStatus { get; set; }
    public int UpdateProgress { get; set; }
    public string? UpdateStatusMessage { get; set; }
    public DateTime? UpdateStartedAt { get; set; }
    public bool HasUpdateErrorLogs { get; set; }
    public bool ForceUpdate { get; set; }
    public bool ReinstallAttempted { get; set; }
}
