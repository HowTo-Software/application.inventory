using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Per-computer access control: tracks which users have been granted access to specific computers.
/// Admins always bypass this check. Non-admins need either department match OR explicit ownership.
/// </summary>
public class ComputerOwnership
{
    public int Id { get; set; }

    // FK to Computers
    public int ComputerId { get; set; }

    // FK to ApplicationUsers
    public int UserId { get; set; }

    public int AccessLevel { get; set; }

    public DateTime GrantedAt { get; set; }

    [StringLength(100)]
    public string? GrantedBy { get; set; }

    // Navigation properties
    public Computer Computer { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}

public enum ComputerAccessLevel
{
    Viewer = 0,
    Manager = 1,
    Owner = 2
}
