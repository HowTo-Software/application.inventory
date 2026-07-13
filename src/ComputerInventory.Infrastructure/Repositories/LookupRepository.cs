using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LookupRepository> _logger;

    public LookupRepository(ApplicationDbContext context, ILogger<LookupRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<ComputerTypeLookup>> GetComputerTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<ComputerTypeLookup>(
            "sp_get_computer_types", cancellationToken);
    }

    public async Task<List<ComputerStatusLookup>> GetComputerStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<ComputerStatusLookup>(
            "sp_get_computer_statuses", cancellationToken);
    }

    public async Task<List<MaintenanceTypeLookup>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<MaintenanceTypeLookup>(
            "sp_get_maintenance_types", cancellationToken);
    }

    public async Task<List<MaintenanceStatusLookup>> GetMaintenanceStatusesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<MaintenanceStatusLookup>(
            "sp_get_maintenance_statuses", cancellationToken);
    }

    public async Task<List<UserRoleLookup>> GetUserRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<UserRoleLookup>(
            "sp_get_user_roles", cancellationToken);
    }

    public async Task<List<VersionTypeLookup>> GetVersionTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<VersionTypeLookup>(
            "sp_get_version_types", cancellationToken);
    }

    public async Task<List<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Department>(
            "sp_get_departments", cancellationToken);
    }

    public async Task<Department?> GetDepartmentByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Department> AddDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Departments.Add(department);
            await _context.SaveChangesAsync(cancellationToken);
            return department;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add department {DepartmentName}", department.Name);
            throw;
        }
    }

    public async Task UpdateDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Departments.Update(department);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update department {DepartmentId}", department.Id);
            throw;
        }
    }

    public async Task DeleteDepartmentAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _context.Departments.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                _context.Departments.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete department {DepartmentId}", id);
            throw;
        }
    }

    public async Task<bool> DepartmentNameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_department_name_exists", cancellationToken,
            SpParam.Input("@name", name),
            SpParam.Input("@exclude_id", (object?)excludeId ?? DBNull.Value));
    }

    public async Task<int> GetDepartmentUsageCountAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int>(
            "sp_get_department_usage_count", cancellationToken,
            SpParam.Input("@id", id));
    }

    public async Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Location>(
            "sp_get_locations", cancellationToken);
    }

    public async Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Locations.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Location> AddLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Locations.Add(location);
            await _context.SaveChangesAsync(cancellationToken);
            return location;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add location {LocationName}", location.Name);
            throw;
        }
    }

    public async Task UpdateLocationAsync(Location location, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Locations.Update(location);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update location {LocationId}", location.Id);
            throw;
        }
    }

    public async Task DeleteLocationAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _context.Locations.FindAsync(new object[] { id }, cancellationToken);
            if (entity != null)
            {
                _context.Locations.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete location {LocationId}", id);
            throw;
        }
    }

    public async Task<bool> LocationNameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<bool>(
            "sp_location_name_exists", cancellationToken,
            SpParam.Input("@name", name),
            SpParam.Input("@exclude_id", (object?)excludeId ?? DBNull.Value));
    }

    public async Task<int> GetLocationUsageCountAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int>(
            "sp_get_location_usage_count", cancellationToken,
            SpParam.Input("@id", id));
    }

    public async Task<List<Manufacturer>> GetManufacturersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Manufacturer>(
            "sp_get_manufacturers", cancellationToken);
    }

    public async Task<List<ComputerModelLookupResult>> GetModelsAsync(int? manufacturerId = null, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<ComputerModelLookupResult>(
            "sp_get_computer_models", cancellationToken,
            SpParam.Input("@manufacturer_id", (object?)manufacturerId ?? DBNull.Value));
    }

    public async Task<List<OperatingSystemEntity>> GetOperatingSystemsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<OperatingSystemEntity>(
            "sp_get_operating_systems", cancellationToken);
    }

    public async Task<List<Location>> GetLocationTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Location>(
            "sp_get_location_types", cancellationToken);
    }

    public async Task<List<Department>> GetDepartmentTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Department>(
            "sp_get_department_types", cancellationToken);
    }

    public async Task<List<Manufacturer>> GetManufacturerTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpListAsync<Manufacturer>(
            "sp_get_manufacturer_types", cancellationToken);
    }
}
