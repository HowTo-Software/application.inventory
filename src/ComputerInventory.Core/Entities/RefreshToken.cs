using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Core.Entities;

public class RefreshToken
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 hash of the token value. The raw token is never stored.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Null until the token is used. Rotation produces a new token and marks this one consumed.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// Null until explicitly revoked (e.g. logout).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Identifies the token family for reuse detection.
    /// All tokens in a rotation chain share the same family.
    /// </summary>
    [Required]
    [StringLength(64)]
    public string Family { get; set; } = string.Empty;

    // Navigation
    public ApplicationUser User { get; set; } = null!;

    public bool IsActive => RevokedAt == null && ConsumedAt == null && ExpiresAt > DateTime.UtcNow;
}
