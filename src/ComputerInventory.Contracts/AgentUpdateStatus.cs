namespace ComputerInventory.Contracts;

/// <summary>
/// Represents the update status of an agent during the update process.
/// </summary>
public enum AgentUpdateStatus
{
    /// <summary>No update in progress</summary>
    None = 0,
    
    /// <summary>Update pending, waiting for agent to start</summary>
    Pending = 1,
    
    /// <summary>Checking for available updates</summary>
    CheckingForUpdate = 2,
    
    /// <summary>Downloading update package</summary>
    Downloading = 3,
    
    /// <summary>Verifying package checksum</summary>
    Verifying = 4,
    
    /// <summary>Extracting update files</summary>
    Extracting = 5,
    
    /// <summary>Preparing the updater process</summary>
    PreparingUpdater = 6,
    
    /// <summary>Stopping the agent service</summary>
    StoppingService = 7,
    
    /// <summary>Backing up current installation</summary>
    BackingUp = 8,
    
    /// <summary>Copying new files</summary>
    CopyingFiles = 9,
    
    /// <summary>Starting the agent service</summary>
    StartingService = 10,
    
    /// <summary>Update completed successfully</summary>
    Completed = 11,
    
    /// <summary>Update failed</summary>
    Failed = 99
}
