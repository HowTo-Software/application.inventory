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

using AgentUpdateStatus = ComputerInventory.Contracts.AgentUpdateStatus;

namespace ComputerInventory.Infrastructure.Repositories;

public class ComputerRepository : IComputerRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ComputerRepository> _logger;

    public ComputerRepository(ApplicationDbContext context, ILogger<ComputerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<Computer> Query => _context.Computers.AsNoTracking();

    public async Task<ComputerDashboardCounts> GetDashboardCountsAsync(string? latestAgentVersion, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_dashboard_counts] @latest_agent_version";
            command.Parameters.Add(new SqlParameter("@latest_agent_version", SqlDbType.NVarChar, 50) { Value = (object?)latestAgentVersion ?? DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return new ComputerDashboardCounts();

            return new ComputerDashboardCounts
            {
                TotalComputers = reader["total_computers"] as int? ?? 0,
                OnlineComputers = reader["online_computers"] as int? ?? 0,
                OfflineComputers = reader["offline_computers"] as int? ?? 0,
                AgentsInstalled = reader["agents_installed"] as int? ?? 0,
                AgentsUpdating = reader["agents_updating"] as int? ?? 0,
                AgentsFailedUpdate = reader["agents_failed_update"] as int? ?? 0,
                AgentsPendingUpdate = reader["agents_pending_update"] as int? ?? 0,
                AgentsOnLatestVersion = reader["agents_on_latest_version"] as int? ?? 0,
                AgentsOnOlderVersion = reader["agents_on_older_version"] as int? ?? 0,
                StaleAgents = reader["stale_agents"] as int? ?? 0,
                ExpiredWarranties = reader["expired_warranties"] as int? ?? 0
            };
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<(IEnumerable<Computer> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? searchTerm = null, string? status = null, string? type = null, string? location = null, string? department = null, string? connection = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        // Resolve search term to IDs via the dedicated search SP (reused by other methods).
        string? searchIds = null;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var matchingIds = await SearchMatchingIdsAsync(searchTerm, cancellationToken);
            if (matchingIds.Count == 0)
                return ([], 0);
            searchIds = string.Join(',', matchingIds);
        }

        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_computers] @search_ids, @status, @type, @location, @department, @connection, @cutoff_minutes, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@search_ids", SqlDbType.NVarChar, -1) { Value = (object?)searchIds ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 100) { Value = (object?)status ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@type", SqlDbType.NVarChar, 100) { Value = (object?)type ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@location", SqlDbType.NVarChar, 100) { Value = (object?)location ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@department", SqlDbType.NVarChar, 100) { Value = (object?)department ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@connection", SqlDbType.NVarChar, 20) { Value = (object?)connection ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@cutoff_minutes", SqlDbType.Int) { Value = 10 });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy?.ToLowerInvariant() ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = ParseDirection(sortDirection, SortDirection.Asc) == SortDirection.Desc ? "DESC" : "ASC" });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = page });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = pageSize });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: paged computer rows
            var items = new List<Computer>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var computer = MapComputerFromReader(reader);

                    if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
                    {
                        computer.AgentStatus = MapAgentStatusFromReader(reader);

                        if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                            computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
                    }
                    else
                    {
                        computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                    }

                    // Populate navigation property names from denormalized SP columns
                    computer.ComputerModel = new ComputerModel
                    {
                        Id = computer.ModelId,
                        ModelName = reader["_model_name"] as string ?? string.Empty,
                        ManufacturerId = (int)reader["_manufacturer_id"],
                        Manufacturer = new Manufacturer
                        {
                            Id = (int)reader["_manufacturer_id"],
                            Name = reader["_manufacturer_name"] as string ?? string.Empty
                        }
                    };
                    computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
                    computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
                    computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
                    computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

                    items.Add(computer);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<Computer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computer_by_id] @id";
            command.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Result set 1: Computer row with includes
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var computer = MapComputerFromReader(reader);

            if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
            {
                computer.AgentStatus = MapAgentStatusFromReader(reader);

                if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                    computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
            }
            else
            {
                computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
            }

            // Populate navigation properties from denormalized SP columns
            computer.ComputerModel = new ComputerModel
            {
                Id = computer.ModelId,
                ModelName = reader["_model_name"] as string ?? string.Empty,
                ManufacturerId = (int)reader["_manufacturer_id"],
                Manufacturer = new Manufacturer
                {
                    Id = (int)reader["_manufacturer_id"],
                    Name = reader["_manufacturer_name"] as string ?? string.Empty
                }
            };
            computer.OperatingSystemEntity = computer.OperatingSystemId.HasValue
                ? new OperatingSystemEntity { Id = computer.OperatingSystemId.Value, Name = reader["_os_name"] as string ?? string.Empty }
                : null;
            computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
            computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
            computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
            computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

            // Result set 2: Maintenance records
            if (await reader.NextResultAsync(cancellationToken))
            {
                var records = new List<MaintenanceRecord>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    records.Add(new MaintenanceRecord
                    {
                        Id = (int)reader["id"],
                        ComputerId = (int)reader["computer_id"],
                        TypeId = (int)reader["type_id"],
                        Description = (string)reader["description"],
                        ScheduledDate = (DateTime)reader["scheduled_date"],
                        CompletedDate = reader["completed_date"] as DateTime?,
                        Cost = reader["cost"] as decimal?,
                        PerformedBy = reader["performed_by"] as string,
                        Notes = reader["notes"] as string,
                        StatusId = (int)reader["status_id"],
                        CreatedAt = (DateTime)reader["created_at"],
                        CreatedBy = reader["created_by"] as string,
                        TypeLookup = new MaintenanceTypeLookup { Id = (int)reader["type_id"], Name = reader["type_name"] as string ?? string.Empty },
                        StatusLookup = new MaintenanceStatusLookup { Id = (int)reader["status_id"], Name = reader["status_name"] as string ?? string.Empty }
                    });
                }
                computer.MaintenanceRecords = records;
            }

            // Result set 3: Recent history (top 50)
            if (await reader.NextResultAsync(cancellationToken))
            {
                var history = new List<ComputerHistory>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    history.Add(new ComputerHistory
                    {
                        Id = (int)reader["id"],
                        ComputerId = (int)reader["computer_id"],
                        Action = (string)reader["action"],
                        OldValue = reader["old_value"] as string,
                        NewValue = reader["new_value"] as string,
                        ChangedBy = reader["changed_by"] as string,
                        ChangedAt = (DateTime)reader["changed_at"],
                        Notes = reader["notes"] as string
                    });
                }
                computer.History = history;
            }

            return computer;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<Computer?> GetBySerialNumberAsync(string serialNumber, CancellationToken cancellationToken = default)
    {
        // Single round-trip via stored procedure with LEFT JOINs on AgentStatus + UpdateRequest.
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computer_by_serial_number] @serial_number";
            command.Parameters.Add(new SqlParameter("@serial_number", SqlDbType.NVarChar, 100) { Value = serialNumber });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            // Build full object graph from the single SP result
            var computer = MapComputerFromReader(reader);

            if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
            {
                computer.AgentStatus = MapAgentStatusFromReader(reader);

                if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                    computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);

                // Attach full graph — all reachable entities tracked as Unchanged
                _context.Computers.Attach(computer);
            }
            else
            {
                // Suppress default = new() initializer before attach to avoid phantom entity
                computer.AgentStatus = null!;
                _context.Computers.Attach(computer);

                // No AgentStatus row in DB — create one so callers can modify it
                computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                _context.ComputerAgentStatuses.Add(computer.AgentStatus);
            }

            return computer;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<Computer?> GetBySerialNumberBasicAsync(string serialNumber, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        var computer = await _context.ExecuteSpSingleAsync<Computer>(
            "sp_get_computer_by_serial_number_basic", cancellationToken,
            SpParam.Input("@serial_number", serialNumber),
            SpParam.Input("@include_deleted", includeDeleted ? 1 : 0));

        if (computer != null)
        {
            // Re-attach for change tracking since ExecuteSpSingleAsync uses AsNoTracking
            _context.Computers.Attach(computer);

            // Load AgentStatus directly from its DbSet to bypass the Computer soft-delete
            // query filter — LoadAsync on a reference navigation won't find the AgentStatus
            // when the owning Computer is soft-deleted.
            var agentStatus = await _context.ComputerAgentStatuses
                .FirstOrDefaultAsync(s => s.ComputerId == computer.Id, cancellationToken);

            if (agentStatus != null)
            {
                computer.AgentStatus = agentStatus;
            }
        }

        return computer;
    }

    public async Task<IEnumerable<Computer>> GetByStatusAsync(ComputerStatus status, CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetComputersByStatusSpAsync((int)status, sortBy: null, sortDirection: "ASC", page: null, pageSize: null, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<Computer> Items, int TotalCount)> GetByStatusAsync(ComputerStatus status, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = ParseDirection(sortDirection, SortDirection.Asc) == SortDirection.Desc ? "DESC" : "ASC";
        return await ExecuteGetComputersByStatusSpAsync((int)status, sortBy?.ToLowerInvariant(), dir, page, pageSize, cancellationToken);
    }

    private async Task<(List<Computer> Items, int TotalCount)> ExecuteGetComputersByStatusSpAsync(
        int statusId, string? sortBy, string sortDirection, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computers_by_status] @status_id, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@status_id", SqlDbType.Int) { Value = statusId });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = sortDirection });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = (object?)page ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = (object?)pageSize ?? DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: computer rows
            var items = new List<Computer>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var computer = MapComputerFromReader(reader);

                    if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
                    {
                        computer.AgentStatus = MapAgentStatusFromReader(reader);

                        if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                            computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
                    }
                    else
                    {
                        computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                    }

                    computer.ComputerModel = new ComputerModel
                    {
                        Id = computer.ModelId,
                        ModelName = reader["_model_name"] as string ?? string.Empty,
                        ManufacturerId = (int)reader["_manufacturer_id"],
                        Manufacturer = new Manufacturer
                        {
                            Id = (int)reader["_manufacturer_id"],
                            Name = reader["_manufacturer_name"] as string ?? string.Empty
                        }
                    };
                    computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
                    computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
                    computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
                    computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

                    items.Add(computer);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<Computer>> GetByLocationAsync(int locationId, CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetComputersByLocationSpAsync(locationId, sortBy: null, sortDirection: "ASC", page: null, pageSize: null, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<Computer> Items, int TotalCount)> GetByLocationAsync(int locationId, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = ParseDirection(sortDirection, SortDirection.Asc) == SortDirection.Desc ? "DESC" : "ASC";
        return await ExecuteGetComputersByLocationSpAsync(locationId, sortBy?.ToLowerInvariant(), dir, page, pageSize, cancellationToken);
    }

    private async Task<(List<Computer> Items, int TotalCount)> ExecuteGetComputersByLocationSpAsync(
        int locationId, string? sortBy, string sortDirection, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computers_by_location] @location_id, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@location_id", SqlDbType.Int) { Value = locationId });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = sortDirection });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = (object?)page ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = (object?)pageSize ?? DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: computer rows
            var items = new List<Computer>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var computer = MapComputerFromReader(reader);

                    if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
                    {
                        computer.AgentStatus = MapAgentStatusFromReader(reader);

                        if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                            computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
                    }
                    else
                    {
                        computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                    }

                    computer.ComputerModel = new ComputerModel
                    {
                        Id = computer.ModelId,
                        ModelName = reader["_model_name"] as string ?? string.Empty,
                        ManufacturerId = (int)reader["_manufacturer_id"],
                        Manufacturer = new Manufacturer
                        {
                            Id = (int)reader["_manufacturer_id"],
                            Name = reader["_manufacturer_name"] as string ?? string.Empty
                        }
                    };
                    computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
                    computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
                    computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
                    computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

                    items.Add(computer);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<Computer>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetComputersByDepartmentSpAsync(departmentId, sortBy: null, sortDirection: "ASC", page: null, pageSize: null, cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<Computer> Items, int TotalCount)> GetByDepartmentAsync(int departmentId, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var dir = ParseDirection(sortDirection, SortDirection.Asc) == SortDirection.Desc ? "DESC" : "ASC";
        return await ExecuteGetComputersByDepartmentSpAsync(departmentId, sortBy?.ToLowerInvariant(), dir, page, pageSize, cancellationToken);
    }

    private async Task<(List<Computer> Items, int TotalCount)> ExecuteGetComputersByDepartmentSpAsync(
        int departmentId, string? sortBy, string sortDirection, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_computers_by_department] @department_id, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@department_id", SqlDbType.Int) { Value = departmentId });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = (object?)sortBy ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = sortDirection });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = (object?)page ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = (object?)pageSize ?? DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Second result set: computer rows
            var items = new List<Computer>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var computer = MapComputerFromReader(reader);

                    if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
                    {
                        computer.AgentStatus = MapAgentStatusFromReader(reader);

                        if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                            computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
                    }
                    else
                    {
                        computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                    }

                    computer.ComputerModel = new ComputerModel
                    {
                        Id = computer.ModelId,
                        ModelName = reader["_model_name"] as string ?? string.Empty,
                        ManufacturerId = (int)reader["_manufacturer_id"],
                        Manufacturer = new Manufacturer
                        {
                            Id = (int)reader["_manufacturer_id"],
                            Name = reader["_manufacturer_name"] as string ?? string.Empty
                        }
                    };
                    computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
                    computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
                    computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
                    computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

                    items.Add(computer);
                }
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    public async Task<IEnumerable<Computer>> SearchAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        // Delegate to the paged GetAllAsync with search, then unwrap
        var (items, _) = await GetAllAsync(1, int.MaxValue, searchTerm: searchTerm, sortBy: "hostname", sortDirection: "ASC", cancellationToken: cancellationToken);
        return items;
    }

    public async Task<(IEnumerable<Computer> Items, int TotalCount)> SearchAsync(string searchTerm, int page, int pageSize, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        return await GetAllAsync(page, pageSize, searchTerm: searchTerm, sortBy: sortBy, sortDirection: sortDirection, cancellationToken: cancellationToken);
    }

    public void ClearChangeTracker()
    {
        _context.ChangeTracker.Clear();
    }

    public async Task<Computer> AddAsync(Computer computer, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Computers.Add(computer);
            await _context.SaveChangesAsync(cancellationToken);
            return computer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add computer {SerialNumber}", computer.SerialNumber);
            throw;
        }
    }

    public async Task UpdateAsync(Computer computer, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only update properties that have changed instead of marking entire entity as modified
            var entry = _context.Entry(computer);
            if (entry.State == EntityState.Detached)
            {
                _context.Computers.Attach(computer);
                entry.State = EntityState.Modified;
            }
            
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update computer {ComputerId}", computer.Id);
            throw;
        }
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var computer = await _context.Computers.FindAsync(new object[] { id }, cancellationToken);
            if (computer != null)
            {
                _context.Computers.Remove(computer);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete computer {ComputerId}", id);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_computer_exists", cancellationToken,
            SpParam.Input("@id", id));
    }

    public async Task<bool> DeleteDirectAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await _context.ExecuteSpScalarAsync<int?>(
                "sp_delete_computer_direct",
                cancellationToken,
                SpParam.Input("@computer_id", id)) ?? 0;
            return deleted > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to soft-delete computer {ComputerId}", id);
            throw;
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_get_total_computer_count", cancellationToken) ?? 0;
    }

    public async Task<Dictionary<ComputerStatus, int>> GetStatusCountsAsync(CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_status_counts]";

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var result = new Dictionary<ComputerStatus, int>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var statusId = reader.GetInt32(0);
                var count = reader.GetInt32(1);
                result[(ComputerStatus)statusId] = count;
            }

            return result;
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    public async Task UpdateWithHistoryAsync(Computer computer, ComputerHistory history, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use execution strategy to support retry-on-failure with explicit transactions
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var entry = _context.Entry(computer);
                    if (entry.State == EntityState.Detached)
                    {
                        _context.Computers.Attach(computer);
                        entry.State = EntityState.Modified;
                    }
                    
                    _context.ComputerHistories.Add(history);
                    
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update computer {ComputerId} with history entry", computer.Id);
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
            _logger.LogError(ex, "Failed to save computer changes");
            throw;
        }
    }

    public async Task<IEnumerable<Computer>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var searchIds = string.Join(',', ids);
        if (string.IsNullOrEmpty(searchIds)) return [];

        // Reuse sp_get_all_computers with search_ids filter and no pagination
        var (items, _) = await GetAllAsync(1, int.MaxValue,
            cancellationToken: cancellationToken);

        // We need to pass IDs directly — call GetAllAsync's underlying SP
        // by building the search_ids from our input
        var dbConnection = _context.Database.GetDbConnection();
        var wasOpen = dbConnection.State == ConnectionState.Open;
        if (!wasOpen) await dbConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = "EXEC [sp_get_all_computers] @search_ids, @status, @type, @location, @department, @connection, @cutoff_minutes, @sort_by, @sort_direction, @page, @page_size";
            command.Parameters.Add(new SqlParameter("@search_ids", SqlDbType.NVarChar, -1) { Value = searchIds });
            command.Parameters.Add(new SqlParameter("@status", SqlDbType.NVarChar, 100) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@type", SqlDbType.NVarChar, 100) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@location", SqlDbType.NVarChar, 100) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@department", SqlDbType.NVarChar, 100) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@connection", SqlDbType.NVarChar, 20) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@cutoff_minutes", SqlDbType.Int) { Value = 10 });
            command.Parameters.Add(new SqlParameter("@sort_by", SqlDbType.NVarChar, 50) { Value = "hostname" });
            command.Parameters.Add(new SqlParameter("@sort_direction", SqlDbType.NVarChar, 4) { Value = "ASC" });
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = DBNull.Value });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // First result set: total count (skip)
            if (await reader.ReadAsync(cancellationToken)) { }

            // Second result set: computer rows
            var result = new List<Computer>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var computer = MapComputerFromReader(reader);

                    if (!reader.IsDBNull(reader.GetOrdinal("agent_status_id")))
                    {
                        computer.AgentStatus = MapAgentStatusFromReader(reader);
                        if (!reader.IsDBNull(reader.GetOrdinal("ur_id")))
                            computer.AgentStatus.ActiveUpdateRequest = MapUpdateRequestFromReader(reader);
                    }
                    else
                    {
                        computer.AgentStatus = new ComputerAgentStatus { ComputerId = computer.Id };
                    }

                    computer.ComputerModel = new ComputerModel
                    {
                        Id = computer.ModelId,
                        ModelName = reader["_model_name"] as string ?? string.Empty,
                        ManufacturerId = (int)reader["_manufacturer_id"],
                        Manufacturer = new Manufacturer
                        {
                            Id = (int)reader["_manufacturer_id"],
                            Name = reader["_manufacturer_name"] as string ?? string.Empty
                        }
                    };
                    computer.StatusLookup = new ComputerStatusLookup { Id = computer.StatusId, Name = reader["_status_name"] as string ?? string.Empty };
                    computer.TypeLookup = new ComputerTypeLookup { Id = computer.TypeId, Name = reader["_type_name"] as string ?? string.Empty };
                    computer.LocationEntity = computer.LocationId.HasValue ? new Location { Id = computer.LocationId.Value, Name = reader["_location_name"] as string ?? string.Empty } : null;
                    computer.DepartmentEntity = computer.DepartmentId.HasValue ? new Department { Id = computer.DepartmentId.Value, Name = reader["_department_name"] as string ?? string.Empty } : null;

                    result.Add(computer);
                }
            }

            return result;
        }
        finally
        {
            if (!wasOpen) await dbConnection.CloseAsync();
        }
    }

    private static SortDirection ParseDirection(string? direction, SortDirection defaultDirection)
    {
        if (string.IsNullOrWhiteSpace(direction)) return defaultDirection;
        return direction.Equals("desc", StringComparison.OrdinalIgnoreCase) ? SortDirection.Desc : SortDirection.Asc;
    }

    /// <summary>
    /// Calls <c>sp_search_computers</c> to perform multi-column LIKE search on the server
    /// and returns the matching computer IDs.
    /// </summary>
    private async Task<List<int>> SearchMatchingIdsAsync(string searchTerm, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_search_computers] @search_term";
            command.Parameters.Add(new SqlParameter("@search_term", SqlDbType.NVarChar, 200) { Value = searchTerm });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var ids = new List<int>();
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetInt32(0));

            return ids;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<int> BulkUpdateStatusAsync(IEnumerable<int> ids, ComputerStatus status, string? updatedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            var idList = string.Join(',', ids);
            if (string.IsNullOrEmpty(idList)) return 0;

            return await _context.ExecuteSpScalarAsync<int>(
                "sp_bulk_update_computer_status", cancellationToken,
                SpParam.Input("@id_list", idList),
                SpParam.Input("@status_id", (int)status),
                SpParam.Input("@updated_by", (object?)updatedBy ?? DBNull.Value));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk update status to {Status} for computers", status);
            throw;
        }
    }

    public async Task<bool> SetLocationSourcePreferenceAsync(int computerId, string preference, CancellationToken cancellationToken = default)
    {
        var affected = await _context.ExecuteSpNonQueryAsync(
            "sp_set_location_source_preference",
            cancellationToken,
            SpParam.Input("@computer_id", computerId),
            SpParam.Input("@preference", preference));
        return affected > 0;
    }

    // ── SP result-set mappers ───────────────────────────────────────────

    public async Task<List<Computer>> GetAllWithGeoLocationAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Computers
            .Include(c => c.GeoLocation)
            .ToListAsync(cancellationToken);
    }

    public async Task<Computer?> GetByIdWithGeoLocationAsync(int computerId, CancellationToken cancellationToken = default)
    {
        return await _context.Computers
            .Include(c => c.GeoLocation)
            .FirstOrDefaultAsync(c => c.Id == computerId, cancellationToken);
    }

    public async Task<Dictionary<int, ComputerAgentStatus>> GetAgentStatusDictionaryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ComputerAgentStatuses
            .ToDictionaryAsync(s => s.ComputerId, cancellationToken);
    }

    public async Task<ComputerAgentStatus?> GetAgentStatusAsync(int computerId, CancellationToken cancellationToken = default)
    {
        return await _context.ComputerAgentStatuses
            .FirstOrDefaultAsync(s => s.ComputerId == computerId, cancellationToken);
    }

    private static Computer MapComputerFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SerialNumber = (string)r["serial_number"],
        Hostname = (string)r["hostname"],
        IpAddress = r["ip_address"] as string,
        MacAddress = r["mac_address"] as string,
        ModelId = (int)r["model_id"],
        OperatingSystemId = r["operating_system_id"] as int?,
        Processor = r["processor"] as string,
        RamGB = r["ram_gb"] as int?,
        StorageGB = r["storage_gb"] as int?,
        TypeId = (int)r["type_id"],
        StatusId = (int)r["status_id"],
        LocationId = r["location_id"] as int?,
        DepartmentId = r["department_id"] as int?,
        Notes = r["notes"] as string,
        PurchaseDate = (DateTime)r["purchase_date"],
        WarrantyExpiration = r["warranty_expiration"] as DateTime?,
        PurchasePrice = r["purchase_price"] as decimal?,
        AssignedTo = r["assigned_to"] as string,
        CreatedAt = (DateTime)r["created_at"],
        UpdatedAt = r["updated_at"] as DateTime?,
        IsDeleted = (bool)r["is_deleted"],
        DeletedAt = r["deleted_at"] as DateTime?,
        CreatedBy = r["created_by"] as string,
        UpdatedBy = r["updated_by"] as string,
        RowVersion = r["row_version"] as byte[]
    };

    private static ComputerAgentStatus MapAgentStatusFromReader(DbDataReader r) => new()
    {
        Id = (int)r["agent_status_id"],
        ComputerId = (int)r["cas_computer_id"],
        LastSeen = r["last_seen"] as DateTime?,
        IsOnline = (bool)r["is_online"],
        CurrentUser = r["current_user"] as string,
        HasAgent = (bool)r["has_agent"],
        PublicIpAddress = r["cas_public_ip"] as string,
        AgentVersion = r["agent_version"] as string,
        AgentSessionId = r["agent_session_id"] as string,
        HeartbeatIntervalMinutes = (int)r["cas_heartbeat"],
        ForceUpdate = (bool)r["force_update"],
        ForceReinstall = (bool)r["force_reinstall"],
        ReinstallAttempted = (bool)r["reinstall_attempted"],
        RequestImmediateCheckin = (bool)r["request_immediate_checkin"],
        PreferredLocationSource = r["preferred_location_source"] as string ?? "Auto",
        ActiveUpdateRequestId = r["active_update_request_id"] as int?
    };

    private static ComputerUpdateRequest MapUpdateRequestFromReader(DbDataReader r) => new()
    {
        Id = (int)r["ur_id"],
        ComputerId = (int)r["ur_computer_id"],
        UpdateStatusId = (int)r["update_status_id"],
        UpdateProgress = (int)r["update_progress"],
        UpdateStatusMessage = r["update_status_message"] as string,
        UpdateStartedAt = r["update_started_at"] as DateTime?,
        UpdateErrorLogs = r["update_error_logs"] as string,
        UpdateRequestedAt = r["update_requested_at"] as DateTime?,
        UpdateRequestedBy = r["update_requested_by"] as string,
        PreviousAgentVersion = r["previous_agent_version"] as string,
        LastUpdatedAt = r["ur_last_updated"] as DateTime?,
        CreatedAt = (DateTime)r["ur_created_at"]
    };
}
