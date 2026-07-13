using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Represents a versioned agent update package tracked in the database.
/// Replaces the single-file-on-disk model with a centralized registry
/// supporting multiple versions, distribution channels, and rollback.
/// </summary>
public class PackageVersion : ISoftDeletable, IHasCreationAudit
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string PackageType { get; set; } = "Agent"; // Agent, Server, etc.
    
    // Package file metadata
    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Checksum { get; set; } = string.Empty;

    public long FileSize { get; set; }
    
    // Release information
    public string? ReleaseNotes { get; set; }
    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }

    [StringLength(50)]
    public string? MinimumAgentVersion { get; set; }
    
    // Lifecycle
    public int Status { get; set; } = (int)PackageStatus.Draft;
    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeprecatedAt { get; set; }
    
    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Distribution
    public int DownloadCount { get; set; }
    
    // Channel targeting
    [Required]
    [StringLength(20)]
    public string Channel { get; set; } = PackageChannel.Stable;
    
    // Navigation
    [InverseProperty(nameof(PackageDistributionRule.PackageVersion))]
    public ICollection<PackageDistributionRule> DistributionRules { get; set; } = new List<PackageDistributionRule>();
}

/// <summary>
/// Defines which computers should receive a specific package version.
/// When no rules exist for a package, it distributes to all computers.
/// </summary>
public class PackageDistributionRule
{
    public int Id { get; set; }
    public int PackageVersionId { get; set; }
    
    public int RuleType { get; set; }
    
    /// <summary>
    /// The target value: serial number, department name, or location name depending on RuleType.
    /// </summary>
    [Required]
    [StringLength(200)]
    public string TargetValue { get; set; } = string.Empty;
    
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    [InverseProperty(nameof(Entities.PackageVersion.DistributionRules))]
    public PackageVersion PackageVersion { get; set; } = null!;
}

public enum PackageStatus
{
    Draft = 0,
    Published = 1,
    Active = 2,
    Deprecated = 3,
    Archived = 4
}

public enum DistributionRuleType
{
    AllComputers = 0,
    BySerialNumber = 1,
    ByDepartment = 2,
    ByLocation = 3
}

/// <summary>
/// Well-known distribution channel names.
/// </summary>
public static class PackageChannel
{
    public const string Stable = "stable";
    public const string Beta = "beta";
    public const string Canary = "canary";
}
