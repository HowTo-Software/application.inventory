namespace ComputerInventory.Core.Interfaces;

/// <summary>
/// Interface for entities that track who created them and when.
/// The DbContext SaveChanges override automatically populates these fields
/// from the current authenticated user — no manual assignment required.
/// If CreatedBy is already set (e.g. by agent registration), it is not overwritten.
/// </summary>
public interface IHasCreationAudit
{
    string? CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
}

/// <summary>
/// Extended audit interface for entities that also track who last modified them.
/// Inherits creation tracking from <see cref="IHasCreationAudit"/>.
/// </summary>
public interface IAuditable : IHasCreationAudit
{
    string? UpdatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
}
