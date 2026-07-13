using System.ComponentModel.DataAnnotations;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

// ============================================================================
// Lookup Table Entities (3NF)
// These replace hardcoded enums with database-driven lookup values.
// IDs are manually assigned (not IDENTITY) to match application constants.
// ============================================================================

/// <summary>
/// Lookup table for computer types (e.g., Desktop, Laptop, Server).
/// Maps to COMPUTER_TYPES table.
/// </summary>
public class ComputerTypeLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for computer statuses (e.g., Available, Assigned, Retired).
/// Maps to COMPUTER_STATUSES table.
/// </summary>
public class ComputerStatusLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for agent update statuses (e.g., None, Pending, Downloading).
/// Maps to UPDATE_STATUSES table.
/// </summary>
public class UpdateStatusLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for maintenance record types (e.g., Repair, Upgrade).
/// Maps to MAINTENANCE_TYPES table.
/// </summary>
public class MaintenanceTypeLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for maintenance record statuses (e.g., Scheduled, Completed).
/// Maps to MAINTENANCE_STATUSES table.
/// </summary>
public class MaintenanceStatusLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for restore point types (e.g., Reboot, Scheduled, Manual).
/// Maps to RESTORE_POINT_TYPES table.
/// </summary>
public class RestorePointTypeLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for version types (e.g., Agent, Web, API).
/// Maps to VERSION_TYPES table.
/// </summary>
public class VersionTypeLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Lookup table for user roles (e.g., Admin, Technician, Viewer).
/// Maps to USER_ROLES table.
/// </summary>
public class UserRoleLookup : ILookupEntity
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
