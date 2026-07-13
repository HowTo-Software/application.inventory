using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Contracts;

/// <summary>
/// Summary DTO for package versions in list views.
/// </summary>
public class PackageVersionDto
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string PackageType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }
    public string? MinimumAgentVersion { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeprecatedAt { get; set; }
    public int DownloadCount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public int DistributionRuleCount { get; set; }
}

/// <summary>
/// Detailed DTO including distribution rules.
/// </summary>
public class PackageVersionDetailDto : PackageVersionDto
{
    public List<PackageDistributionRuleDto> DistributionRules { get; set; } = new();
}

public class PackageDistributionRuleDto
{
    public int Id { get; set; }
    public int PackageVersionId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to upload a new package version.
/// </summary>
public class UploadPackageRequest
{
    [Required, StringLength(50)]
    public string Version { get; set; } = string.Empty;

    [StringLength(20)]
    public string PackageType { get; set; } = "Agent";

    [StringLength(5000)]
    public string? ReleaseNotes { get; set; }

    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }

    [StringLength(50)]
    public string? MinimumAgentVersion { get; set; }

    [StringLength(20)]
    public string Channel { get; set; } = "stable";
}

/// <summary>
/// Request to update package metadata (not the file itself).
/// </summary>
public class UpdatePackageRequest
{
    [StringLength(5000)]
    public string? ReleaseNotes { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsBreakingChange { get; set; }

    [StringLength(50)]
    public string? MinimumAgentVersion { get; set; }

    [StringLength(20)]
    public string? Channel { get; set; }
}

/// <summary>
/// Request to add a distribution rule to a package.
/// </summary>
public class AddDistributionRuleRequest
{
    [Required]
    public string RuleType { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string TargetValue { get; set; } = string.Empty;
}

/// <summary>
/// Response for the active version endpoint consumed by agents.
/// </summary>
public class ActivePackageInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public bool ReinstallRequired { get; set; }
    public string? InstallScriptUrl { get; set; }
}

/// <summary>
/// Summary of package distribution status.
/// </summary>
public class PackageDistributionSummaryDto
{
    public string ActiveVersion { get; set; } = string.Empty;
    public int TotalPackages { get; set; }
    public int ComputersAtLatest { get; set; }
    public int ComputersPendingUpdate { get; set; }
    public int ComputersUpdating { get; set; }
    public Dictionary<string, int> VersionDistribution { get; set; } = new();
}
