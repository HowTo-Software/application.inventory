namespace ComputerInventory.Core.Interfaces;

public interface IReferenceDataRepository
{
    Task<int?> FindOrCreateManufacturerAsync(string name, CancellationToken cancellationToken = default);
    Task<int?> FindOrCreateComputerModelAsync(int manufacturerId, string modelName, CancellationToken cancellationToken = default);
    Task<int?> FindOrCreateOperatingSystemAsync(string name, CancellationToken cancellationToken = default);
    Task<int?> FindOrCreateDepartmentAsync(string name, CancellationToken cancellationToken = default);
    Task<int?> FindOrCreateLocationAsync(string name, CancellationToken cancellationToken = default);
}
