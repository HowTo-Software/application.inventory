using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class ReferenceDataRepository : IReferenceDataRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReferenceDataRepository> _logger;

    public ReferenceDataRepository(ApplicationDbContext context, ILogger<ReferenceDataRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int?> FindOrCreateManufacturerAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_find_or_create_manufacturer", cancellationToken,
            SpParam.Input("@name", name));
    }

    public async Task<int?> FindOrCreateComputerModelAsync(int manufacturerId, string modelName, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_find_or_create_computer_model", cancellationToken,
            SpParam.Input("@manufacturer_id", manufacturerId),
            SpParam.Input("@model_name", modelName));
    }

    public async Task<int?> FindOrCreateOperatingSystemAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_find_or_create_operating_system", cancellationToken,
            SpParam.Input("@name", name));
    }

    public async Task<int?> FindOrCreateDepartmentAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_find_or_create_department", cancellationToken,
            SpParam.Input("@name", name));
    }

    public async Task<int?> FindOrCreateLocationAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_find_or_create_location", cancellationToken,
            SpParam.Input("@name", name));
    }
}
