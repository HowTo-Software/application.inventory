using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface ILookupRepository
{
    Task<List<ComputerTypeLookup>> GetComputerTypesAsync(CancellationToken cancellationToken = default);
    Task<List<ComputerStatusLookup>> GetComputerStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<MaintenanceTypeLookup>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default);
    Task<List<MaintenanceStatusLookup>> GetMaintenanceStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<UserRoleLookup>> GetUserRolesAsync(CancellationToken cancellationToken = default);
    Task<List<VersionTypeLookup>> GetVersionTypesAsync(CancellationToken cancellationToken = default);
    Task<List<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<Department?> GetDepartmentByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Department> AddDepartmentAsync(Department department, CancellationToken cancellationToken = default);
    Task UpdateDepartmentAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteDepartmentAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DepartmentNameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<int> GetDepartmentUsageCountAsync(int id, CancellationToken cancellationToken = default);

    Task<List<Location>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<Location?> GetLocationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Location> AddLocationAsync(Location location, CancellationToken cancellationToken = default);
    Task UpdateLocationAsync(Location location, CancellationToken cancellationToken = default);
    Task DeleteLocationAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> LocationNameExistsAsync(string name, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<int> GetLocationUsageCountAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Manufacturer>> GetManufacturersAsync(CancellationToken cancellationToken = default);
    Task<List<ComputerModelLookupResult>> GetModelsAsync(int? manufacturerId = null, CancellationToken cancellationToken = default);
    Task<List<OperatingSystemEntity>> GetOperatingSystemsAsync(CancellationToken cancellationToken = default);
    Task<List<Location>> GetLocationTypesAsync(CancellationToken cancellationToken = default);
    Task<List<Department>> GetDepartmentTypesAsync(CancellationToken cancellationToken = default);
    Task<List<Manufacturer>> GetManufacturerTypesAsync(CancellationToken cancellationToken = default);
}
