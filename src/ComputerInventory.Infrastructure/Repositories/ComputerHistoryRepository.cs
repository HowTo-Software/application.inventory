using System.Data;
using System.Data.Common;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using ComputerInventory.Infrastructure.Sorting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class ComputerHistoryRepository : IComputerHistoryRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComputerHistoryRepository> _logger;

    public ComputerHistoryRepository(ApplicationDbContext context, ILogger<ComputerHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<ComputerHistory> Query => _context.ComputerHistories.AsNoTracking();

    public async Task<IEnumerable<ComputerHistory>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<ComputerHistory>(
            "sp_get_history_by_computer_id", cancellationToken,
            SpParam.Input("@computer_id", computerId));
    }

    public async Task<(IEnumerable<ComputerHistory> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? action = null, string? search = null,
        DateTime? from = null, DateTime? to = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = string.IsNullOrWhiteSpace(sortDirection) ? "DESC"
            : sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_history_paged] @page, @page_size, @action, @search, @from_date, @to_date, @sort_by, @sort_direction";
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });
            command.Parameters.Add(new SqlParameter("@action", SqlDbType.NVarChar, 100) { Value = (object?)action ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200) { Value = (object?)search ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@from_date", SqlDbType.DateTime2) { Value = (object?)from ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@to_date", SqlDbType.DateTime2) { Value = (object?)to ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy?.ToLowerInvariant() ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = dir });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = (int)reader["total_count"];

            // RS2: paged history rows with computer
            var items = new List<ComputerHistory>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var h = MapHistoryFromReader(reader);
                    h.Computer = new Computer
                    {
                        Id = h.ComputerId,
                        Hostname = reader["_computer_hostname"] as string ?? string.Empty,
                        SerialNumber = reader["_computer_serial_number"] as string ?? string.Empty,
                    };
                    items.Add(h);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<List<string>> GetDistinctActionsAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_distinct_history_actions]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var actions = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
                actions.Add(reader.GetString(0));
            return actions;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task AddAsync(ComputerHistory history, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.ComputerHistories.Add(history);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add history entry for computer {ComputerId}, action {Action}", history.ComputerId, history.Action);
            throw;
        }
    }

    public async Task AddHistorySpAsync(int computerId, string action, string? changedBy = null, string? oldValue = null, string? newValue = null, string? notes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_add_computer_history", cancellationToken,
                SpParam.Input("@computer_id", computerId),
                SpParam.Input("@action", action),
                SpParam.Input("@changed_by", (object?)changedBy ?? DBNull.Value),
                SpParam.Input("@old_value", (object?)oldValue ?? DBNull.Value),
                SpParam.Input("@new_value", (object?)newValue ?? DBNull.Value),
                SpParam.Input("@notes", (object?)notes ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add history via SP for computer {ComputerId}, action {Action}", computerId, action);
            throw;
        }
    }

    public void Track(ComputerHistory history)
    {
        _context.ComputerHistories.Add(history);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExecuteSpScalarAsync<int>(
                "sp_soft_delete_old_history", cancellationToken,
                SpParam.Input("@cutoff", cutoff));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to soft-delete history records older than {Cutoff}", cutoff);
            throw;
        }
    }

    // ── SP result-set mapper ──────────────────────────────────────────────────────

    private static ComputerHistory MapHistoryFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        ComputerId = (int)r["computer_id"],
        Action = (string)r["action"],
        OldValue = r["old_value"] as string,
        NewValue = r["new_value"] as string,
        ChangedBy = r["changed_by"] as string,
        ChangedAt = (DateTime)r["changed_at"],
        Notes = r["notes"] as string,
    };
}
