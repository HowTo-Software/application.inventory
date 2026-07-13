using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ComputerInventory.Api.Services;

public interface IPackageService
{
    /// <summary>
    /// Stores the uploaded package file and creates a database record.
    /// </summary>
    Task<PackageVersion> UploadPackageAsync(Stream fileStream, string fileName, string version,
        string? releaseNotes, bool isRequired, bool isBreakingChange,
        string? minimumAgentVersion, string channel, string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a package to Published status.
    /// </summary>
    Task PublishPackageAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a package as the Active version (only one active per type/channel).
    /// The previously active package is moved to Published status.
    /// </summary>
    Task ActivatePackageAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a package as Deprecated.
    /// </summary>
    Task DeprecatePackageAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to a previous version by activating it.
    /// </summary>
    Task RollbackToVersionAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves which package version a specific computer should receive.
    /// Considers distribution rules, channels, and version compatibility.
    /// </summary>
    Task<PackageVersion?> ResolvePackageForComputerAsync(string serialNumber, string? currentVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the physical file path for a package version.
    /// </summary>
    string GetPackageFilePath(PackageVersion package);

    /// <summary>
    /// Deletes a package and its file from storage. Only Draft/Archived packages can be deleted.
    /// </summary>
    Task DeletePackageAsync(int packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets distribution summary: how many computers are at each version.
    /// </summary>
    Task<PackageDistributionSummary> GetDistributionSummaryAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes the active package info to update.json for backward compatibility.
    /// </summary>
    Task SyncUpdateJsonAsync(CancellationToken cancellationToken = default);
}

public class PackageDistributionSummary
{
    public string? ActiveVersion { get; set; }
    public int TotalPackages { get; set; }
    public int ComputersAtLatest { get; set; }
    public int ComputersPendingUpdate { get; set; }
    public int ComputersUpdating { get; set; }
    public Dictionary<string, int> VersionDistribution { get; set; } = new();
}

public class PackageService : IPackageService
{
    private readonly IPackageRepository _packageRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly IUpdateInfoCacheService _updateInfoCache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PackageService> _logger;
    private readonly AgentFileSettings _fileSettings;
    private readonly string _packageStorageRoot;

    public PackageService(
        IPackageRepository packageRepository,
        IComputerRepository computerRepository,
        IUpdateInfoCacheService updateInfoCache,
        IConfiguration configuration,
        ILogger<PackageService> logger,
        AgentFileSettings fileSettings)
    {
        _packageRepository = packageRepository;
        _computerRepository = computerRepository;
        _updateInfoCache = updateInfoCache;
        _configuration = configuration;
        _logger = logger;
        _fileSettings = fileSettings;

        _packageStorageRoot = _fileSettings.GetPackagesPath();
        Directory.CreateDirectory(_packageStorageRoot);
    }

    public async Task<PackageVersion> UploadPackageAsync(Stream fileStream, string fileName, string version,
        string? releaseNotes, bool isRequired, bool isBreakingChange,
        string? minimumAgentVersion, string channel, string createdBy,
        CancellationToken cancellationToken = default)
    {
        // Validate version doesn't already exist
        if (await _packageRepository.VersionExistsAsync(version, "Agent", cancellationToken))
        {
            throw new InvalidOperationException($"Package version {version} already exists.");
        }

        // Create version-specific directory
        var versionDir = Path.Combine(_packageStorageRoot, SanitizeVersionPath(version));
        Directory.CreateDirectory(versionDir);

        var storagePath = Path.Combine(versionDir, "agent-update.zip");

        // Save the file
        using (var fileOut = File.Create(storagePath))
        {
            await fileStream.CopyToAsync(fileOut, cancellationToken);
        }

        // Calculate checksum
        string checksum;
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(storagePath))
        {
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        var fileSize = new FileInfo(storagePath).Length;

        var package = new PackageVersion
        {
            Version = version,
            PackageType = "Agent",
            FileName = fileName,
            StoragePath = storagePath,
            Checksum = checksum,
            FileSize = fileSize,
            ReleaseNotes = releaseNotes,
            IsRequired = isRequired,
            IsBreakingChange = isBreakingChange,
            MinimumAgentVersion = minimumAgentVersion,
            Channel = channel,
            Status = (int)PackageStatus.Draft,
            CreatedBy = createdBy
        };

        await _packageRepository.AddAsync(package, cancellationToken);

        _logger.LogInformation("Package uploaded: Version={Version}, Size={Size}, Checksum={Checksum}",
            version, fileSize, checksum);

        return package;
    }

    public async Task PublishPackageAsync(int packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package {packageId} not found.");

        if (package.Status != (int)PackageStatus.Draft)
        {
            throw new InvalidOperationException($"Only Draft packages can be published. Current status: {package.Status}");
        }

        // Verify file still exists
        if (!File.Exists(package.StoragePath))
        {
            throw new InvalidOperationException("Package file not found on disk.");
        }

        package.Status = (int)PackageStatus.Published;
        package.PublishedAt = DateTime.UtcNow;
        await _packageRepository.UpdateAsync(package, cancellationToken);

        _logger.LogInformation("Package published: Version={Version}", package.Version);
    }

    public async Task ActivatePackageAsync(int packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package {packageId} not found.");

        if (package.Status != (int)PackageStatus.Published && package.Status != (int)PackageStatus.Active)
        {
            throw new InvalidOperationException($"Only Published packages can be activated. Current status: {package.Status}");
        }

        // Deactivate the current active version (if any)
        var currentActive = await _packageRepository.GetActiveVersionAsync(package.PackageType, cancellationToken);
        if (currentActive != null && currentActive.Id != packageId)
        {
            currentActive.Status = (int)PackageStatus.Published;
            await _packageRepository.UpdateAsync(currentActive, cancellationToken);
            _logger.LogInformation("Deactivated previous active package: Version={Version}", currentActive.Version);
        }

        package.Status = (int)PackageStatus.Active;
        if (!package.PublishedAt.HasValue)
        {
            package.PublishedAt = DateTime.UtcNow;
        }
        await _packageRepository.UpdateAsync(package, cancellationToken);

        // Sync to update.json for backward compatibility with existing agents
        await SyncUpdateJsonAsync(cancellationToken);

        // Invalidate the update info cache so heartbeats pick up the new version
        _updateInfoCache.Invalidate();

        _logger.LogInformation("Package activated: Version={Version}", package.Version);
    }

    public async Task DeprecatePackageAsync(int packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package {packageId} not found.");

        if (package.Status == (int)PackageStatus.Active)
        {
            throw new InvalidOperationException("Cannot deprecate the active package. Activate a different version first.");
        }

        package.Status = (int)PackageStatus.Deprecated;
        package.DeprecatedAt = DateTime.UtcNow;
        await _packageRepository.UpdateAsync(package, cancellationToken);

        _logger.LogInformation("Package deprecated: Version={Version}", package.Version);
    }

    public async Task RollbackToVersionAsync(int packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package {packageId} not found.");

        if (package.Status == (int)PackageStatus.Draft || package.Status == (int)PackageStatus.Archived)
        {
            throw new InvalidOperationException($"Cannot rollback to a {package.Status} package.");
        }

        // Verify file still exists
        if (!File.Exists(package.StoragePath))
        {
            throw new InvalidOperationException("Package file not found on disk. Cannot rollback.");
        }

        // Activate this version (deactivates the current one)
        await ActivatePackageAsync(packageId, cancellationToken);

        _logger.LogInformation("Rolled back to package: Version={Version}", package.Version);
    }

    public async Task<PackageVersion?> ResolvePackageForComputerAsync(string serialNumber, string? currentVersion,
        CancellationToken cancellationToken = default)
    {
        // Get the active package
        var activePackage = await _packageRepository.GetActiveVersionAsync("Agent", cancellationToken);
        if (activePackage == null)
        {
            return null;
        }

        // Check version - if already at or beyond active, no update needed
        if (!string.IsNullOrEmpty(currentVersion) && !string.IsNullOrEmpty(activePackage.Version))
        {
            try
            {
                var current = new Version(currentVersion);
                var available = new Version(activePackage.Version);
                if (current >= available)
                {
                    return null;
                }
            }
            catch
            {
                // If versions can't be parsed, still offer the update
            }
        }

        // Check minimum agent version compatibility
        if (!string.IsNullOrEmpty(activePackage.MinimumAgentVersion) && !string.IsNullOrEmpty(currentVersion))
        {
            try
            {
                var current = new Version(currentVersion);
                var minimum = new Version(activePackage.MinimumAgentVersion);
                if (current < minimum)
                {
                    _logger.LogWarning(
                        "Computer {Serial} at version {Current} is below minimum {Minimum} for package {Version}. Reinstall may be required.",
                        serialNumber, currentVersion, activePackage.MinimumAgentVersion, activePackage.Version);
                }
            }
            catch { /* Ignore parse errors */ }
        }

        // Check distribution rules (if any exist)
        var rules = activePackage.DistributionRules?.Where(r => r.IsEnabled).ToList();
        if (rules != null && rules.Count > 0)
        {
            // If rules exist, the computer must match at least one
            var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);
            if (computer == null)
            {
                return activePackage; // Unknown computer, allow update
            }

            var matchesRule = false;
            foreach (var rule in rules)
            {
                switch ((DistributionRuleType)rule.RuleType)
                {
                    case DistributionRuleType.AllComputers:
                        matchesRule = true;
                        break;
                    case DistributionRuleType.BySerialNumber:
                        matchesRule = string.Equals(computer.SerialNumber, rule.TargetValue, StringComparison.OrdinalIgnoreCase);
                        break;
                    case DistributionRuleType.ByDepartment:
                        matchesRule = computer.DepartmentId.HasValue &&
                                     string.Equals(computer.DepartmentEntity?.Name, rule.TargetValue, StringComparison.OrdinalIgnoreCase);
                        break;
                    case DistributionRuleType.ByLocation:
                        matchesRule = computer.LocationId.HasValue &&
                                     string.Equals(computer.LocationEntity?.Name, rule.TargetValue, StringComparison.OrdinalIgnoreCase);
                        break;
                }
                if (matchesRule) break;
            }

            if (!matchesRule)
            {
                _logger.LogDebug("Computer {Serial} does not match distribution rules for package {Version}",
                    serialNumber, activePackage.Version);
                return null;
            }
        }

        return activePackage;
    }

    public string GetPackageFilePath(PackageVersion package)
    {
        return package.StoragePath;
    }

    public async Task DeletePackageAsync(int packageId, CancellationToken cancellationToken = default)
    {
        var package = await _packageRepository.GetByIdAsync(packageId, cancellationToken)
            ?? throw new InvalidOperationException($"Package {packageId} not found.");

        if (package.Status == (int)PackageStatus.Active)
        {
            throw new InvalidOperationException("Cannot delete the active package.");
        }

        // Delete file from disk
        if (File.Exists(package.StoragePath))
        {
            var dir = Path.GetDirectoryName(package.StoragePath);
            File.Delete(package.StoragePath);

            // Clean up version directory if empty
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }

        await _packageRepository.DeleteAsync(package, cancellationToken);
        _logger.LogInformation("Package deleted: Version={Version}", package.Version);
    }

    public async Task<PackageDistributionSummary> GetDistributionSummaryAsync(CancellationToken cancellationToken = default)
    {
        var activePackage = await _packageRepository.GetActiveVersionAsync("Agent", cancellationToken);
        var allPackages = await _packageRepository.GetAllAsync(cancellationToken);

        // Page through all computers in batches to avoid loading entire table at once
        var allComputers = new List<Computer>();
        int page = 1;
        const int pageSize = 1000;
        while (true)
        {
            var (items, totalCount) = await _computerRepository.GetAllAsync(
                page, pageSize, cancellationToken: cancellationToken);
            allComputers.AddRange(items);
            if (allComputers.Count >= totalCount) break;
            page++;
        }

        var summary = new PackageDistributionSummary
        {
            ActiveVersion = activePackage?.Version,
            TotalPackages = allPackages.Count,
        };

        foreach (var computer in allComputers)
        {
            var version = computer.AgentStatus.AgentVersion ?? "Unknown";
            summary.VersionDistribution.TryGetValue(version, out var count);
            summary.VersionDistribution[version] = count + 1;

            if (activePackage != null && version == activePackage.Version)
            {
                summary.ComputersAtLatest++;
            }
            else if (computer.AgentStatus.ForceUpdate || (computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusId ?? (int)AgentUpdateStatus.None) == (int)AgentUpdateStatus.Pending)
            {
                summary.ComputersPendingUpdate++;
            }
            else
            {
                var status = computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusId ?? (int)AgentUpdateStatus.None;
                if (status != (int)AgentUpdateStatus.None && 
                    status != (int)AgentUpdateStatus.Completed &&
                    status != (int)AgentUpdateStatus.Failed)
                {
                    summary.ComputersUpdating++;
                }
            }
        }

        return summary;
    }

    public async Task SyncUpdateJsonAsync(CancellationToken cancellationToken = default)
    {
        var activePackage = await _packageRepository.GetActiveVersionAsync("Agent", cancellationToken);
        if (activePackage == null)
        {
            return;
        }

        // Write to the legacy update.json location for backward compatibility
        var updatePath = _fileSettings.GetUpdatePath();
        Directory.CreateDirectory(updatePath);

        var updateInfo = new AgentUpdateInfo
        {
            Version = activePackage.Version,
            Checksum = activePackage.Checksum,
            ReleaseNotes = activePackage.ReleaseNotes ?? "",
            IsRequired = activePackage.IsRequired,
            IsBreakingChange = activePackage.IsBreakingChange,
            ReleasedAt = activePackage.PublishedAt ?? DateTime.UtcNow,
            FileSize = activePackage.FileSize
        };

        var updateInfoPath = _fileSettings.GetUpdateJsonPath();
        await UpdateJsonHelper.WriteUpdateJsonAsync(updateInfoPath, updateInfo, cancellationToken);

        // Also copy/symlink the package file so the legacy download endpoint works
        var legacyPackagePath = _fileSettings.GetUpdatePackagePath();
        if (File.Exists(activePackage.StoragePath) && activePackage.StoragePath != legacyPackagePath)
        {
            File.Copy(activePackage.StoragePath, legacyPackagePath, overwrite: true);
        }

        _logger.LogInformation("Synced update.json for backward compatibility: Version={Version}", activePackage.Version);
    }

    private static string SanitizeVersionPath(string version)
    {
        // Replace characters that are invalid in directory names
        return version.Replace(' ', '_')
                      .Replace('/', '-')
                      .Replace('\\', '-');
    }
}
