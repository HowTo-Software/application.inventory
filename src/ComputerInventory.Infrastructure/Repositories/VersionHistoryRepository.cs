using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

namespace ComputerInventory.Infrastructure.Repositories;

public class VersionHistoryRepository : IVersionHistoryRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<VersionHistoryRepository> _logger;

    public VersionHistoryRepository(ApplicationDbContext context, ILogger<VersionHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<VersionHistory> Query => _context.VersionHistories.AsNoTracking();

    public async Task<List<VersionHistory>> GetByTypeAsync(VersionType type, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<VersionHistory>(
            "sp_get_version_history_by_type_unpaged", cancellationToken,
            SpParam.Input("@type_id", (int)type));
    }

    public async Task<(List<VersionHistory> Items, int TotalCount)> GetByTypePagedAsync(VersionType type, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var spName = type == VersionType.Server
            ? "sp_get_server_history"
            : "sp_get_thin_client_history";

        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = $"EXEC [{spName}] @page, @page_size";
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Result set 1: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Result set 2: paged rows
            var items = new List<VersionHistory>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(ReadVersionHistory(reader));
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<(List<VersionHistory> ServerItems, int ServerTotal, List<VersionHistory> ThinClientItems, int ThinClientTotal)> GetAllVersionHistoryPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_version_history] @page, @page_size";
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Result set 1: server count
            int serverTotal = 0;
            if (await reader.ReadAsync(cancellationToken))
                serverTotal = reader.GetInt32(0);

            // Result set 2: server items
            var serverItems = new List<VersionHistory>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    serverItems.Add(ReadVersionHistory(reader));
            }

            // Result set 3: thin client count
            int thinClientTotal = 0;
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                thinClientTotal = reader.GetInt32(0);

            // Result set 4: thin client items
            var thinClientItems = new List<VersionHistory>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    thinClientItems.Add(ReadVersionHistory(reader));
            }

            return (serverItems, serverTotal, thinClientItems, thinClientTotal);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<VersionHistory?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.VersionHistories.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<bool> ExistsAsync(VersionType type, string version, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_version_exists", cancellationToken,
            SpParam.Input("@type_id", (int)type),
            SpParam.Input("@version", version));
    }

    public async Task<VersionHistory?> FindByTypeAndVersionAsync(VersionType type, string version, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpSingleAsync<VersionHistory>(
            "sp_find_version_by_type_and_version", cancellationToken,
            SpParam.Input("@type_id", (int)type),
            SpParam.Input("@version", version));
    }

    public async Task<List<VersionHistory>> GetCurrentVersionsByTypeAsync(VersionType type, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<VersionHistory>(
            "sp_get_current_versions_by_type", cancellationToken,
            SpParam.Input("@type_id", (int)type));
    }

    public async Task AddAsync(VersionHistory versionHistory, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.VersionHistories.Add(versionHistory);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add version history {Version} (type {TypeId})", versionHistory.Version, versionHistory.TypeId);
            throw;
        }
    }

    public async Task DeleteAsync(VersionHistory versionHistory, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.VersionHistories.Remove(versionHistory);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete version history {VersionId}", versionHistory.Id);
            throw;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save version history changes");
            throw;
        }
    }

    private static VersionHistory ReadVersionHistory(System.Data.Common.DbDataReader reader)
    {
        return new VersionHistory
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Version = reader.GetString(reader.GetOrdinal("version")),
            TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
            ChangeLog = reader.GetString(reader.GetOrdinal("change_log")),
            ReleasedAt = reader.GetDateTime(reader.GetOrdinal("released_at")),
            ReleasedBy = reader.IsDBNull(reader.GetOrdinal("released_by")) ? null : reader.GetString(reader.GetOrdinal("released_by")),
            IsCurrentVersion = reader.GetBoolean(reader.GetOrdinal("is_current_version")),
            Checksum = reader.IsDBNull(reader.GetOrdinal("checksum")) ? null : reader.GetString(reader.GetOrdinal("checksum")),
            FileSize = reader.IsDBNull(reader.GetOrdinal("file_size")) ? null : reader.GetInt64(reader.GetOrdinal("file_size")),
        };
    }
}
