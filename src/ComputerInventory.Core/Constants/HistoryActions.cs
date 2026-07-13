namespace ComputerInventory.Core.Constants;

/// <summary>
/// Constants for ComputerHistory.Action values and actor identifiers.
/// </summary>
public static class HistoryActions
{
    public const string Heartbeat = "Heartbeat";
    public const string AgentUpdated = "AgentUpdated";
    public const string UpdateCompleted = "UpdateCompleted";
    public const string UpdateFailed = "UpdateFailed";
    public const string ManualOverride = "ManualOverride";
    public const string StatusChange = "StatusChange";
    public const string RestorePointCreated = "RestorePointCreated";
    public const string RestorePointAccessed = "RestorePointAccessed";
    public const string RestorePointDeleted = "RestorePointDeleted";
    public const string RestorePointCleanup = "RestorePointCleanup";
    public const string AgentSelfHealed = "AgentSelfHealed";
    public const string ComputerEdited = "ComputerEdited";
    public const string AssignmentChanged = "AssignmentChanged";
}

/// <summary>
/// Constants for CreatedBy/ChangedBy actor names.
/// </summary>
public static class ActorNames
{
    public const string Agent = "Agent";
}
