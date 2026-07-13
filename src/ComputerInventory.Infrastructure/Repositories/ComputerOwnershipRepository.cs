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

public class ComputerOwnershipRepository : IComputerOwnershipRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComputerOwnershipRepository> _logger;

    public ComputerOwnershipRepository(ApplicationDbContext context, ILogger<ComputerOwnershipRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ComputerOwnership?> GetAsync(int computerId, int userId)
    {
        return await _context.ExecuteSpSingleAsync<ComputerOwnership>(
            "sp_get_ownership", default,
            SpParam.Input("@computer_id", computerId),
            SpParam.Input("@user_id", userId));
    }

    public async Task<IEnumerable<ComputerOwnership>> GetByComputerIdAsync(int computerId)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_ownerships_by_computer_id] @computer_id";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync();
            var items = new List<ComputerOwnership>();
            while (await reader.ReadAsync())
            {
                var o = MapOwnershipFromReader(reader);
                o.User = new ApplicationUser
                {
                    Id = o.UserId,
                    Username = reader["_user_username"] as string ?? string.Empty,
                    FirstName = reader["_user_first_name"] as string ?? string.Empty,
                    LastName = reader["_user_last_name"] as string ?? string.Empty,
                    Email = reader["_user_email"] as string ?? string.Empty,
                };
                items.Add(o);
            }
            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<ComputerOwnership>> GetByUserIdAsync(int userId)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_ownerships_by_user_id] @user_id";
            command.Parameters.Add(new SqlParameter("@user_id", SqlDbType.Int) { Value = userId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync();
            var items = new List<ComputerOwnership>();
            while (await reader.ReadAsync())
            {
                var o = MapOwnershipFromReader(reader);
                o.Computer = new Computer
                {
                    Id = o.ComputerId,
                    Hostname = reader["_computer_hostname"] as string ?? string.Empty,
                    SerialNumber = reader["_computer_serial_number"] as string ?? string.Empty,
                };
                items.Add(o);
            }
            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<bool> HasAccessAsync(int computerId, int userId, ComputerAccessLevel minimumLevel = ComputerAccessLevel.Viewer)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_has_ownership_access", default,
            SpParam.Input("@computer_id", computerId),
            SpParam.Input("@user_id", userId),
            SpParam.Input("@minimum_level", (int)minimumLevel));
    }

    public async Task AddAsync(ComputerOwnership ownership)
    {
        try
        {
            _context.ComputerOwnerships.Add(ownership);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add ownership for computer {ComputerId}, user {UserId}", ownership.ComputerId, ownership.UserId);
            throw;
        }
    }

    public async Task RemoveAsync(int computerId, int userId)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_remove_ownership", default,
                SpParam.Input("@computer_id", computerId),
                SpParam.Input("@user_id", userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove ownership for computer {ComputerId}, user {UserId}", computerId, userId);
            throw;
        }
    }

    public async Task RemoveAllForComputerAsync(int computerId)
    {
        try
        {
            await _context.ExecuteSpNonQueryAsync(
                "sp_remove_all_ownerships_for_computer", default,
                SpParam.Input("@computer_id", computerId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove all ownerships for computer {ComputerId}", computerId);
            throw;
        }
    }

    // ── SP result-set mapper ──────────────────────────────────────────────────────

    private static ComputerOwnership MapOwnershipFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        ComputerId = (int)r["computer_id"],
        UserId = (int)r["user_id"],
        AccessLevel = (int)r["access_level"],
        GrantedAt = (DateTime)r["granted_at"],
        GrantedBy = r["granted_by"] as string,
    };
}
