using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Core.Entities;

public class InviteCode
{
    public int Id { get; set; }

    /// <summary>
    /// SHA-256 hash of the invite code value. The raw code is never stored.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Maximum number of times this invite code can be used. Null means single-use.
    /// </summary>
    public int MaxUses { get; set; } = 1;

    /// <summary>
    /// Number of times this invite code has been used.
    /// </summary>
    public int UseCount { get; set; }

    /// <summary>
    /// Username of the admin who generated this invite code.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional note for tracking purpose (e.g. "For new IT team members").
    /// </summary>
    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>
    /// Whether this code has been manually revoked by an admin.
    /// </summary>
    public bool IsRevoked { get; set; }

    public bool IsValid => !IsRevoked && UseCount < MaxUses && ExpiresAt > DateTime.UtcNow;
}
