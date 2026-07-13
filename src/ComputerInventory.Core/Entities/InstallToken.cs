using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Core.Entities;

public class InstallToken
{
    public int Id { get; set; }

    /// <summary>
    /// SHA-256 hash of the token value. The raw token is never stored.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Null until the token is consumed by downloading the install script.
    /// </summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>
    /// Username of the admin/technician who generated this token.
    /// </summary>
    [Required]
    [StringLength(256)]
    public string CreatedBy { get; set; } = string.Empty;

    public bool IsValid => ConsumedAt == null && ExpiresAt > DateTime.UtcNow;
}
