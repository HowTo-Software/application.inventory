using System.Data;
using System.Data.Common;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class PackageRepository : IPackageRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PackageRepository> _logger;

    public PackageRepository(ApplicationDbContext context, ILogger<PackageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<PackageVersion> Query => _context.PackageVersions.AsNoTracking();

    public async Task<List<PackageVersion>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_packages]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: Packages
            var items = new List<PackageVersion>();
            while (await reader.ReadAsync(cancellationToken))
                items.Add(MapPackageFromReader(reader));

            // RS2: Distribution rules — correlate by package_version_id
            if (await reader.NextResultAsync(cancellationToken))
                await AttachRulesFromReader(reader, items, cancellationToken);

            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<PackageVersion?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.PackageVersions
            .FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<PackageVersion?> GetByIdWithRulesAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_package_by_id_with_rules] @id";
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: Package
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            var pkg = MapPackageFromReader(reader);

            // RS2: Distribution rules
            if (await reader.NextResultAsync(cancellationToken))
                await AttachRulesFromReader(reader, new List<PackageVersion> { pkg }, cancellationToken);

            return pkg;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<PackageVersion?> GetByVersionAsync(string version, string packageType = "Agent", CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_package_by_version] @version, @package_type";
            command.Parameters.Add(new SqlParameter("@version", SqlDbType.NVarChar, 50) { Value = version });
            command.Parameters.Add(new SqlParameter("@package_type", SqlDbType.NVarChar, 50) { Value = packageType });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;
            var pkg = MapPackageFromReader(reader);

            if (await reader.NextResultAsync(cancellationToken))
                await AttachRulesFromReader(reader, new List<PackageVersion> { pkg }, cancellationToken);

            return pkg;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<PackageVersion?> GetActiveVersionAsync(string packageType = "Agent", CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_active_package_version] @package_type";
            command.Parameters.Add(new SqlParameter("@package_type", SqlDbType.NVarChar, 50) { Value = packageType });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;
            var pkg = MapPackageFromReader(reader);

            if (await reader.NextResultAsync(cancellationToken))
                await AttachRulesFromReader(reader, new List<PackageVersion> { pkg }, cancellationToken);

            return pkg;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<List<PackageVersion>> GetByStatusAsync(PackageStatus status, string packageType = "Agent", CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<PackageVersion>(
            "sp_get_packages_by_status", cancellationToken,
            SpParam.Input("@status", (int)status),
            SpParam.Input("@package_type", packageType));
    }

    public async Task<List<PackageVersion>> GetByChannelAsync(string channel, string packageType = "Agent", CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<PackageVersion>(
            "sp_get_packages_by_channel", cancellationToken,
            SpParam.Input("@channel", channel),
            SpParam.Input("@package_type", packageType));
    }

    public async Task AddAsync(PackageVersion package, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PackageVersions.Add(package);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add package {Version} ({PackageType})", package.Version, package.PackageType);
            throw;
        }
    }

    public async Task UpdateAsync(PackageVersion package, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PackageVersions.Update(package);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update package {PackageId}", package.Id);
            throw;
        }
    }

    public async Task DeleteAsync(PackageVersion package, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PackageVersions.Remove(package);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete package {PackageId}", package.Id);
            throw;
        }
    }

    public async Task<bool> VersionExistsAsync(string version, string packageType = "Agent", CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_package_version_exists", cancellationToken,
            SpParam.Input("@version", version),
            SpParam.Input("@package_type", packageType));
    }

    public async Task IncrementDownloadCountAsync(int packageId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_increment_download_count", cancellationToken,
                SpParam.Input("@package_id", packageId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment download count for package {PackageId}", packageId);
            throw;
        }
    }

    // Distribution rules
    public async Task AddDistributionRuleAsync(PackageDistributionRule rule, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PackageDistributionRules.Add(rule);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add distribution rule for package {PackageId}", rule.PackageVersionId);
            throw;
        }
    }

    public async Task RemoveDistributionRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await _context.PackageDistributionRules.FindAsync(new object[] { ruleId }, cancellationToken);
            if (rule != null)
            {
                _context.PackageDistributionRules.Remove(rule);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove distribution rule {RuleId}", ruleId);
            throw;
        }
    }

    public async Task<List<PackageDistributionRule>> GetDistributionRulesAsync(int packageId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<PackageDistributionRule>(
            "sp_get_distribution_rules", cancellationToken,
            SpParam.Input("@package_id", packageId));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save package changes");
            throw;
        }
    }

    private static PackageVersion MapPackageFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        Version = (string)r["version"],
        PackageType = (string)r["package_type"],
        FileName = (string)r["file_name"],
        StoragePath = (string)r["storage_path"],
        Checksum = (string)r["checksum"],
        FileSize = (long)r["file_size"],
        ReleaseNotes = r["release_notes"] as string,
        IsRequired = (bool)r["is_required"],
        IsBreakingChange = (bool)r["is_breaking_change"],
        MinimumAgentVersion = r["minimum_agent_version"] as string,
        Status = (int)r["status"],
        CreatedAt = (DateTime)r["created_at"],
        CreatedBy = r["created_by"] as string,
        PublishedAt = r["published_at"] is DBNull ? null : (DateTime)r["published_at"],
        DeprecatedAt = r["deprecated_at"] is DBNull ? null : (DateTime)r["deprecated_at"],
        IsDeleted = (bool)r["is_deleted"],
        DeletedAt = r["deleted_at"] is DBNull ? null : (DateTime)r["deleted_at"],
        DownloadCount = (int)r["download_count"],
        Channel = (string)r["channel"],
    };

    private static PackageDistributionRule MapRuleFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        PackageVersionId = (int)r["package_version_id"],
        RuleType = (int)r["rule_type"],
        TargetValue = (string)r["target_value"],
        IsEnabled = (bool)r["is_enabled"],
        CreatedAt = (DateTime)r["created_at"],
    };

    private static async Task AttachRulesFromReader(DbDataReader reader, List<PackageVersion> packages, CancellationToken cancellationToken)
    {
        var rulesMap = new Dictionary<int, List<PackageDistributionRule>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var rule = MapRuleFromReader(reader);
            if (!rulesMap.TryGetValue(rule.PackageVersionId, out var list))
            {
                list = new List<PackageDistributionRule>();
                rulesMap[rule.PackageVersionId] = list;
            }
            list.Add(rule);
        }
        foreach (var pkg in packages)
        {
            if (rulesMap.TryGetValue(pkg.Id, out var rules))
                pkg.DistributionRules = rules;
        }
    }
}
