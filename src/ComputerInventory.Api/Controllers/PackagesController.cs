using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

/// <summary>
/// Centralized package version management and distribution.
/// Provides CRUD for package versions, lifecycle transitions, distribution rules,
/// and the agent-facing endpoint to resolve which version a computer should receive.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PackagesController : ControllerBase
{
    private readonly IPackageRepository _packageRepository;
    private readonly IPackageService _packageService;
    private readonly ILogger<PackagesController> _logger;
    private readonly IConfiguration _configuration;

    public PackagesController(
        IPackageRepository packageRepository,
        IPackageService packageService,
        ILogger<PackagesController> logger,
        IConfiguration configuration)
    {
        _packageRepository = packageRepository;
        _packageService = packageService;
        _logger = logger;
        _configuration = configuration;
    }

    // ───────────────────────────────────────────────
    // Admin endpoints: package CRUD & lifecycle
    // ───────────────────────────────────────────────

    /// <summary>
    /// List all package versions.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(List<PackageVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PackageVersionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var packages = await _packageRepository.GetAllAsync(cancellationToken);
        return Ok(packages.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Get a package version by ID with distribution rules.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PackageVersionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageVersionDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdWithRulesAsync(id, cancellationToken);
        if (package == null) return NotFound();

        return Ok(MapToDetailDto(package));
    }

    /// <summary>
    /// Upload a new package version (zip file + metadata).
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(500_000_000)] // 500 MB max
    [ProducesResponseType(typeof(PackageVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PackageVersionDto>> Upload(
        [FromForm] IFormFile package,
        [FromForm][Required][StringLength(50)] string version,
        [FromForm][StringLength(5000)] string? releaseNotes,
        [FromForm] bool isRequired = false,
        [FromForm] bool isBreakingChange = false,
        [FromForm][StringLength(50)] string? minimumAgentVersion = null,
        [FromForm][StringLength(20)] string channel = "stable",
        CancellationToken cancellationToken = default)
    {
        if (package == null || package.Length == 0)
        {
            return BadRequest(new { message = "No package file provided." });
        }

        try
        {
            using var stream = package.OpenReadStream();
            var result = await _packageService.UploadPackageAsync(
                stream, package.FileName, version,
                releaseNotes, isRequired, isBreakingChange,
                minimumAgentVersion, channel,
                User.Identity?.Name ?? "Admin",
                cancellationToken);

            _logger.LogInformation("Package uploaded by {User}: Version={Version}", User.Identity?.Name, version);

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, MapToDto(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update package metadata (not the file).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdatePackageRequest request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(id, cancellationToken);
        if (package == null) return NotFound();

        if (request.ReleaseNotes != null) package.ReleaseNotes = request.ReleaseNotes;
        if (request.IsRequired.HasValue) package.IsRequired = request.IsRequired.Value;
        if (request.IsBreakingChange.HasValue) package.IsBreakingChange = request.IsBreakingChange.Value;
        if (request.MinimumAgentVersion != null) package.MinimumAgentVersion = request.MinimumAgentVersion;
        if (request.Channel != null) package.Channel = request.Channel;

        await _packageRepository.UpdateAsync(package, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Delete a package version. Only Draft or Archived packages can be deleted.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _packageService.DeletePackageAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ───────────────────────────────────────────────
    // Lifecycle transitions
    // ───────────────────────────────────────────────

    /// <summary>
    /// Publish a Draft package (makes it eligible for activation).
    /// </summary>
    [HttpPost("{id}/publish")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Publish(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _packageService.PublishPackageAsync(id, cancellationToken);
            _logger.LogInformation("Package {Id} published by {User}", id, User.Identity?.Name);
            return Ok(new { message = "Package published successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Activate a Published package — this becomes the version distributed to agents.
    /// Only one version can be active at a time per package type.
    /// </summary>
    [HttpPost("{id}/activate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _packageService.ActivatePackageAsync(id, cancellationToken);
            _logger.LogInformation("Package {Id} activated by {User}", id, User.Identity?.Name);
            return Ok(new { message = "Package activated and is now being distributed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deprecate a package so it's no longer distributed.
    /// </summary>
    [HttpPost("{id}/deprecate")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Deprecate(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _packageService.DeprecatePackageAsync(id, cancellationToken);
            _logger.LogInformation("Package {Id} deprecated by {User}", id, User.Identity?.Name);
            return Ok(new { message = "Package deprecated." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Rollback to a previously published version.
    /// </summary>
    [HttpPost("{id}/rollback")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Rollback(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _packageService.RollbackToVersionAsync(id, cancellationToken);
            _logger.LogInformation("Rolled back to package {Id} by {User}", id, User.Identity?.Name);
            return Ok(new { message = "Rollback successful. Previous version is now active." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ───────────────────────────────────────────────
    // Distribution rules
    // ───────────────────────────────────────────────

    /// <summary>
    /// Add a distribution rule to a package (target specific computers, departments, or locations).
    /// </summary>
    [HttpPost("{id}/rules")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PackageDistributionRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PackageDistributionRuleDto>> AddRule(
        int id, [FromBody] AddDistributionRuleRequest request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(id, cancellationToken);
        if (package == null) return NotFound();

        if (!Enum.TryParse<DistributionRuleType>(request.RuleType, true, out var ruleType))
        {
            return BadRequest(new { message = $"Invalid rule type: {request.RuleType}. Valid values: AllComputers, BySerialNumber, ByDepartment, ByLocation" });
        }

        var rule = new PackageDistributionRule
        {
            PackageVersionId = id,
            RuleType = (int)ruleType,
            TargetValue = request.TargetValue,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        await _packageRepository.AddDistributionRuleAsync(rule, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, MapRuleToDto(rule));
    }

    /// <summary>
    /// Remove a distribution rule.
    /// </summary>
    [HttpDelete("{id}/rules/{ruleId}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveRule(int id, int ruleId, CancellationToken cancellationToken)
    {
        await _packageRepository.RemoveDistributionRuleAsync(ruleId, cancellationToken);
        return NoContent();
    }

    // ───────────────────────────────────────────────
    // Agent-facing endpoints
    // ───────────────────────────────────────────────

    /// <summary>
    /// Resolve which package version a computer should download.
    /// Called by agents during update check.
    /// </summary>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(ActivePackageInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivePackageInfoDto>> ResolveForAgent(
        [FromQuery] string serialNumber,
        [FromQuery] string? currentVersion,
        CancellationToken cancellationToken)
    {
        // Validate API key
        var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();
        var configuredKey = _configuration["AgentSettings:ApiKey"];
        if (!string.IsNullOrEmpty(configuredKey) && !string.Equals(apiKey, configuredKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var package = await _packageService.ResolvePackageForComputerAsync(serialNumber, currentVersion, cancellationToken);
        if (package == null)
        {
            return NotFound(new { message = "No update available" });
        }

        var serverUrl = GetConfiguredServerUrl();
        return Ok(new ActivePackageInfoDto
        {
            Version = package.Version,
            DownloadUrl = $"{serverUrl}/api/packages/{package.Id}/download",
            Checksum = package.Checksum,
            FileSize = package.FileSize,
            IsRequired = package.IsRequired,
            IsBreakingChange = package.IsBreakingChange,
            ReleaseNotes = package.ReleaseNotes ?? "",
            ReleasedAt = package.PublishedAt ?? package.CreatedAt
        });
    }

    /// <summary>
    /// Download a specific package version's zip file. Tracks download count.
    /// Supports HTTP Range requests for download resume.
    /// </summary>
    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(id, cancellationToken);
        if (package == null) return NotFound();

        var filePath = _packageService.GetPackageFilePath(package);
        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Package file not found: {Path} for version {Version}", filePath, package.Version);
            return NotFound(new { message = "Package file not found on server." });
        }

        // Increment download count (fire-and-forget, don't block the download)
        _ = _packageRepository.IncrementDownloadCountAsync(id, CancellationToken.None);

        return PhysicalFile(filePath, "application/zip", "agent-update.zip", enableRangeProcessing: true);
    }

    // ───────────────────────────────────────────────
    // Distribution summary
    // ───────────────────────────────────────────────

    /// <summary>
    /// Get a summary of package distribution across the fleet.
    /// </summary>
    [HttpGet("distribution-summary")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(PackageDistributionSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PackageDistributionSummaryDto>> GetDistributionSummary(CancellationToken cancellationToken)
    {
        var summary = await _packageService.GetDistributionSummaryAsync(cancellationToken);
        return Ok(new PackageDistributionSummaryDto
        {
            ActiveVersion = summary.ActiveVersion ?? "None",
            TotalPackages = summary.TotalPackages,
            ComputersAtLatest = summary.ComputersAtLatest,
            ComputersPendingUpdate = summary.ComputersPendingUpdate,
            ComputersUpdating = summary.ComputersUpdating,
            VersionDistribution = summary.VersionDistribution
        });
    }

    // ───────────────────────────────────────────────
    // Helpers
    // ───────────────────────────────────────────────

    private string GetConfiguredServerUrl()
    {
        var configuredUrl = _configuration["AgentSettings:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl.TrimEnd('/');
        }
        return $"{Request.Scheme}://{Request.Host}";
    }

    private static PackageVersionDto MapToDto(PackageVersion p) => new()
    {
        Id = p.Id,
        Version = p.Version,
        PackageType = p.PackageType,
        FileName = p.FileName,
        Checksum = p.Checksum,
        FileSize = p.FileSize,
        ReleaseNotes = p.ReleaseNotes,
        IsRequired = p.IsRequired,
        IsBreakingChange = p.IsBreakingChange,
        MinimumAgentVersion = p.MinimumAgentVersion,
        Status = ((PackageStatus)p.Status).ToString(),
        CreatedAt = p.CreatedAt,
        CreatedBy = p.CreatedBy,
        PublishedAt = p.PublishedAt,
        DeprecatedAt = p.DeprecatedAt,
        DownloadCount = p.DownloadCount,
        Channel = p.Channel,
        DistributionRuleCount = p.DistributionRules?.Count ?? 0
    };

    private static PackageVersionDetailDto MapToDetailDto(PackageVersion p) => new()
    {
        Id = p.Id,
        Version = p.Version,
        PackageType = p.PackageType,
        FileName = p.FileName,
        Checksum = p.Checksum,
        FileSize = p.FileSize,
        ReleaseNotes = p.ReleaseNotes,
        IsRequired = p.IsRequired,
        IsBreakingChange = p.IsBreakingChange,
        MinimumAgentVersion = p.MinimumAgentVersion,
        Status = ((PackageStatus)p.Status).ToString(),
        CreatedAt = p.CreatedAt,
        CreatedBy = p.CreatedBy,
        PublishedAt = p.PublishedAt,
        DeprecatedAt = p.DeprecatedAt,
        DownloadCount = p.DownloadCount,
        Channel = p.Channel,
        DistributionRuleCount = p.DistributionRules?.Count ?? 0,
        DistributionRules = p.DistributionRules?.Select(MapRuleToDto).ToList() ?? new()
    };

    private static PackageDistributionRuleDto MapRuleToDto(PackageDistributionRule r) => new()
    {
        Id = r.Id,
        PackageVersionId = r.PackageVersionId,
        RuleType = ((DistributionRuleType)r.RuleType).ToString(),
        TargetValue = r.TargetValue,
        IsEnabled = r.IsEnabled,
        CreatedAt = r.CreatedAt
    };
}
