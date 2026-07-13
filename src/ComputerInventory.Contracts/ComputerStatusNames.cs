namespace ComputerInventory.Contracts;

/// <summary>
/// String constants for computer status values. Use these instead of magic strings
/// to ensure consistency across the codebase.
/// </summary>
public static class ComputerStatusNames
{
    public const string Available = "Unassigned";
    public const string Assigned = "Assigned";
    public const string InMaintenance = "InMaintenance";
    public const string Retired = "Retired";
    public const string Lost = "Lost";
    public const string Stolen = "Stolen";
    public const string Disposed = "Disposed";
}
