using System.Data;
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

public class MaintenanceRepository : IMaintenanceRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceRepository> _logger;

    // Compiled queries removed — GetByIdAsync now uses sp_get_maintenance_by_id

    public MaintenanceRepository(ApplicationDbContext context, ILogger<MaintenanceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<MaintenanceRecord> Query => _context.MaintenanceRecords.AsNoTracking();

    public async Task<(int Open, int Scheduled, int CompletedThisMonth)> GetMaintenanceSummaryCountsAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_maintenance_summary_counts]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return (0, 0, 0);

            return (
                reader["open_count"] as int? ?? 0,
                reader["scheduled_count"] as int? ?? 0,
                reader["completed_this_month"] as int? ?? 0
            );
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = string.IsNullOrWhiteSpace(sortDirection) || sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC" : "ASC";

        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_maintenance_records] @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy?.ToLowerInvariant() ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = dir });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged maintenance records
            var items = new List<MaintenanceRecord>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new MaintenanceRecord
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ComputerId = reader.GetInt32(reader.GetOrdinal("computer_id")),
                        TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                        Description = reader.GetString(reader.GetOrdinal("description")),
                        ScheduledDate = reader.GetDateTime(reader.GetOrdinal("scheduled_date")),
                        CompletedDate = reader.IsDBNull(reader.GetOrdinal("completed_date")) ? null : reader.GetDateTime(reader.GetOrdinal("completed_date")),
                        Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? null : reader.GetDecimal(reader.GetOrdinal("cost")),
                        PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetString(reader.GetOrdinal("performed_by")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                        StatusId = reader.GetInt32(reader.GetOrdinal("status_id")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
                    };

                    // Hydrate navigation properties from denormalized SP columns
                    record.Computer = new Computer
                    {
                        Id = record.ComputerId,
                        Hostname = reader.GetString(reader.GetOrdinal("_computer_hostname")),
                        SerialNumber = reader.GetString(reader.GetOrdinal("_computer_serial_number")),
                    };
                    record.TypeLookup = new MaintenanceTypeLookup
                    {
                        Id = record.TypeId,
                        Name = reader.GetString(reader.GetOrdinal("_type_name")),
                    };
                    record.StatusLookup = new MaintenanceStatusLookup
                    {
                        Id = record.StatusId,
                        Name = reader.GetString(reader.GetOrdinal("_status_name")),
                    };

                    items.Add(record);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<MaintenanceRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_maintenance_by_id] @id";
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var record = new MaintenanceRecord
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                ComputerId = reader.GetInt32(reader.GetOrdinal("computer_id")),
                TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                Description = reader.GetString(reader.GetOrdinal("description")),
                ScheduledDate = reader.GetDateTime(reader.GetOrdinal("scheduled_date")),
                CompletedDate = reader.IsDBNull(reader.GetOrdinal("completed_date")) ? null : reader.GetDateTime(reader.GetOrdinal("completed_date")),
                Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? null : reader.GetDecimal(reader.GetOrdinal("cost")),
                PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetString(reader.GetOrdinal("performed_by")),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                StatusId = reader.GetInt32(reader.GetOrdinal("status_id")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
                RowVersion = reader.IsDBNull(reader.GetOrdinal("row_version")) ? null : (byte[])reader["row_version"],
            };

            record.Computer = new Computer
            {
                Id = record.ComputerId,
                Hostname = reader.GetString(reader.GetOrdinal("_computer_hostname")),
                SerialNumber = reader.GetString(reader.GetOrdinal("_computer_serial_number")),
            };
            record.TypeLookup = new MaintenanceTypeLookup
            {
                Id = record.TypeId,
                Name = reader.GetString(reader.GetOrdinal("_type_name")),
            };
            record.StatusLookup = new MaintenanceStatusLookup
            {
                Id = record.StatusId,
                Name = reader.GetString(reader.GetOrdinal("_status_name")),
            };

            return record;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        // Fetch all records (no pagination) via the SP with a large page size
        var (items, _) = await ExecuteGetMaintenanceByComputerIdSpAsync(computerId, 1, int.MaxValue, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetByComputerIdPagedAsync(int computerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await ExecuteGetMaintenanceByComputerIdSpAsync(computerId, page, pageSize, cancellationToken);
    }

    private async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> ExecuteGetMaintenanceByComputerIdSpAsync(
        int computerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_maintenance_by_computer_id] @computer_id, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged maintenance records
            var items = new List<MaintenanceRecord>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new MaintenanceRecord
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ComputerId = reader.GetInt32(reader.GetOrdinal("computer_id")),
                        TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                        Description = reader.GetString(reader.GetOrdinal("description")),
                        ScheduledDate = reader.GetDateTime(reader.GetOrdinal("scheduled_date")),
                        CompletedDate = reader.IsDBNull(reader.GetOrdinal("completed_date")) ? null : reader.GetDateTime(reader.GetOrdinal("completed_date")),
                        Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? null : reader.GetDecimal(reader.GetOrdinal("cost")),
                        PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetString(reader.GetOrdinal("performed_by")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                        StatusId = reader.GetInt32(reader.GetOrdinal("status_id")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
                    };

                    record.Computer = new Computer
                    {
                        Id = record.ComputerId,
                        Hostname = reader.GetString(reader.GetOrdinal("_computer_hostname")),
                        SerialNumber = reader.GetString(reader.GetOrdinal("_computer_serial_number")),
                    };
                    record.TypeLookup = new MaintenanceTypeLookup
                    {
                        Id = record.TypeId,
                        Name = reader.GetString(reader.GetOrdinal("_type_name")),
                    };
                    record.StatusLookup = new MaintenanceStatusLookup
                    {
                        Id = record.StatusId,
                        Name = reader.GetString(reader.GetOrdinal("_status_name")),
                    };

                    items.Add(record);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetByStatusAsync(MaintenanceStatus status, CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetMaintenanceByStatusSpAsync((int)status, 1, int.MaxValue, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetByStatusPagedAsync(MaintenanceStatus status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await ExecuteGetMaintenanceByStatusSpAsync((int)status, page, pageSize, cancellationToken);
    }

    private async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> ExecuteGetMaintenanceByStatusSpAsync(
        int statusId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_maintenance_by_status] @status_id, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@status_id", SqlDbType.Int) { Value = statusId });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged maintenance records
            var items = new List<MaintenanceRecord>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new MaintenanceRecord
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ComputerId = reader.GetInt32(reader.GetOrdinal("computer_id")),
                        TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                        Description = reader.GetString(reader.GetOrdinal("description")),
                        ScheduledDate = reader.GetDateTime(reader.GetOrdinal("scheduled_date")),
                        CompletedDate = reader.IsDBNull(reader.GetOrdinal("completed_date")) ? null : reader.GetDateTime(reader.GetOrdinal("completed_date")),
                        Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? null : reader.GetDecimal(reader.GetOrdinal("cost")),
                        PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetString(reader.GetOrdinal("performed_by")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                        StatusId = reader.GetInt32(reader.GetOrdinal("status_id")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
                    };

                    record.Computer = new Computer
                    {
                        Id = record.ComputerId,
                        Hostname = reader.GetString(reader.GetOrdinal("_computer_hostname")),
                        SerialNumber = reader.GetString(reader.GetOrdinal("_computer_serial_number")),
                    };
                    record.TypeLookup = new MaintenanceTypeLookup
                    {
                        Id = record.TypeId,
                        Name = reader.GetString(reader.GetOrdinal("_type_name")),
                    };
                    record.StatusLookup = new MaintenanceStatusLookup
                    {
                        Id = record.StatusId,
                        Name = reader.GetString(reader.GetOrdinal("_status_name")),
                    };

                    items.Add(record);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetScheduledBetweenDatesAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetScheduledMaintenanceSpAsync(start, end, 1, int.MaxValue, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> GetScheduledBetweenDatesPagedAsync(DateTime start, DateTime end, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await ExecuteGetScheduledMaintenanceSpAsync(start, end, page, pageSize, cancellationToken);
    }

    private async Task<(IEnumerable<MaintenanceRecord> Items, int TotalCount)> ExecuteGetScheduledMaintenanceSpAsync(
        DateTime startDate, DateTime endDate, int page, int pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_scheduled_maintenance] @start_date, @end_date, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@start_date", SqlDbType.DateTime2) { Value = startDate });
            command.Parameters.Add(new SqlParameter("@end_date", SqlDbType.DateTime2) { Value = endDate });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged maintenance records
            var items = new List<MaintenanceRecord>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var record = new MaintenanceRecord
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("id")),
                        ComputerId = reader.GetInt32(reader.GetOrdinal("computer_id")),
                        TypeId = reader.GetInt32(reader.GetOrdinal("type_id")),
                        Description = reader.GetString(reader.GetOrdinal("description")),
                        ScheduledDate = reader.GetDateTime(reader.GetOrdinal("scheduled_date")),
                        CompletedDate = reader.IsDBNull(reader.GetOrdinal("completed_date")) ? null : reader.GetDateTime(reader.GetOrdinal("completed_date")),
                        Cost = reader.IsDBNull(reader.GetOrdinal("cost")) ? null : reader.GetDecimal(reader.GetOrdinal("cost")),
                        PerformedBy = reader.IsDBNull(reader.GetOrdinal("performed_by")) ? null : reader.GetString(reader.GetOrdinal("performed_by")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                        StatusId = reader.GetInt32(reader.GetOrdinal("status_id")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                        CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? null : reader.GetString(reader.GetOrdinal("created_by")),
                    };

                    record.Computer = new Computer
                    {
                        Id = record.ComputerId,
                        Hostname = reader.GetString(reader.GetOrdinal("_computer_hostname")),
                        SerialNumber = reader.GetString(reader.GetOrdinal("_computer_serial_number")),
                    };
                    record.TypeLookup = new MaintenanceTypeLookup
                    {
                        Id = record.TypeId,
                        Name = reader.GetString(reader.GetOrdinal("_type_name")),
                    };
                    record.StatusLookup = new MaintenanceStatusLookup
                    {
                        Id = record.StatusId,
                        Name = reader.GetString(reader.GetOrdinal("_status_name")),
                    };

                    items.Add(record);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<MaintenanceRecord> AddAsync(MaintenanceRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.MaintenanceRecords.Add(record);
            await _context.SaveChangesAsync(cancellationToken);
            return record;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add maintenance record for computer {ComputerId}", record.ComputerId);
            throw;
        }
    }

    public async Task UpdateAsync(MaintenanceRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Entry(record).State = EntityState.Modified;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update maintenance record {RecordId}", record.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var record = await _context.MaintenanceRecords.FindAsync(new object[] { id }, cancellationToken);
            if (record != null)
            {
                _context.MaintenanceRecords.Remove(record);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete maintenance record {RecordId}", id);
            throw;
        }
    }
}
