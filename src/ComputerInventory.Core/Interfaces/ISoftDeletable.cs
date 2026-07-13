namespace ComputerInventory.Core.Interfaces;

/// <summary>
/// Marker interface for entities that support soft-delete.
/// Entities implementing this will have their deletes intercepted:
/// instead of being removed from the database, IsDeleted is set to true
/// and DeletedAt records the timestamp. A global query filter automatically
/// excludes soft-deleted rows from all queries.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
