namespace ComputerInventory.Contracts;

/// <summary>
/// Structured analysis result for agent update error logs.
/// </summary>
public class ErrorAnalysisResult
{
    /// <summary>Computer identifier for this analysis</summary>
    public int ComputerId { get; set; }
    
    /// <summary>When the analysis was performed</summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Overall severity: Low, Medium, High, Critical</summary>
    public string Severity { get; set; } = "Medium";
    
    /// <summary>Short summary of the root cause</summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>The update phase where the failure occurred</summary>
    public string FailedPhase { get; set; } = "Unknown";
    
    /// <summary>Individual errors extracted from the logs</summary>
    public List<IdentifiedError> Errors { get; set; } = new();
    
    /// <summary>Ordered remediation steps to resolve the issue</summary>
    public List<RemediationStep> RemediationSteps { get; set; } = new();
    
    /// <summary>Whether the issue is likely transient and may resolve on retry</summary>
    public bool IsTransient { get; set; }
    
    /// <summary>Suggested automatic action: Retry, Reinstall, ManualIntervention, None</summary>
    public string SuggestedAction { get; set; } = "ManualIntervention";
}

/// <summary>
/// A single identified error extracted from raw log text.
/// </summary>
public class IdentifiedError
{
    /// <summary>Error category: Network, FileSystem, Permission, Service, Package, Configuration, Resource, Unknown</summary>
    public string Category { get; set; } = "Unknown";
    
    /// <summary>The error message extracted from logs</summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>Source file or log section where the error was found</summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>Severity: Low, Medium, High, Critical</summary>
    public string Severity { get; set; } = "Medium";
}

/// <summary>
/// A recommended step to resolve an identified error.
/// </summary>
public class RemediationStep
{
    /// <summary>Step number (1-based)</summary>
    public int Order { get; set; }
    
    /// <summary>Short title for the step</summary>
    public string Title { get; set; } = string.Empty;
    
    /// <summary>Detailed description of what to do</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>Whether this step can be performed automatically</summary>
    public bool IsAutomatable { get; set; }
    
    /// <summary>API action to invoke if automatable (e.g., "force-update", "force-reinstall")</summary>
    public string? AutomationAction { get; set; }
}
