using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

/// <summary>
/// Service for fetching lookup/reference data from the API.
/// Provides cached dropdown options for all lookup tables.
/// </summary>
public interface ILookupService
{
    Task<List<LookupItemDto>> GetComputerTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetComputerStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetMaintenanceStatusesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetUserRolesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetVersionTypesAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetLocationsAsync(CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetManufacturersAsync(CancellationToken cancellationToken = default);
    Task<List<ModelLookupItemDto>> GetModelsAsync(int? manufacturerId = null, CancellationToken cancellationToken = default);
    Task<List<LookupItemDto>> GetOperatingSystemsAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
}

public class LookupService : ILookupService
{
    private readonly IApiClient _apiClient;
    private readonly Dictionary<string, (DateTime expiry, object data)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public LookupService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<List<LookupItemDto>> GetComputerTypesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("computer-types", cancellationToken);
    public Task<List<LookupItemDto>> GetComputerStatusesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("computer-statuses", cancellationToken);
    public Task<List<LookupItemDto>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("maintenance-types", cancellationToken);
    public Task<List<LookupItemDto>> GetMaintenanceStatusesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("maintenance-statuses", cancellationToken);
    public Task<List<LookupItemDto>> GetUserRolesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("user-roles", cancellationToken);
    public Task<List<LookupItemDto>> GetVersionTypesAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("version-types", cancellationToken);
    public Task<List<LookupItemDto>> GetDepartmentsAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("departments", cancellationToken);
    public Task<List<LookupItemDto>> GetLocationsAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("locations", cancellationToken);
    public Task<List<LookupItemDto>> GetManufacturersAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("manufacturers", cancellationToken);
    public Task<List<LookupItemDto>> GetOperatingSystemsAsync(CancellationToken cancellationToken = default) => GetCachedLookupAsync("operating-systems", cancellationToken);

    public async Task<List<ModelLookupItemDto>> GetModelsAsync(int? manufacturerId = null, CancellationToken cancellationToken = default)
    {
        var key = manufacturerId.HasValue ? $"models-{manufacturerId}" : "models";
        var url = manufacturerId.HasValue ? ApiEndpoints.Lookups.ModelsByManufacturer(manufacturerId.Value) : ApiEndpoints.Lookups.Models;

        if (_cache.TryGetValue(key, out var cached) && cached.expiry > DateTime.UtcNow)
            return (List<ModelLookupItemDto>)cached.data;

        var result = await _apiClient.GetAsync<PagedResult<ModelLookupItemDto>>(url, cancellationToken);
        var data = result.Data?.Items ?? new List<ModelLookupItemDto>();
        _cache[key] = (DateTime.UtcNow.Add(CacheDuration), data);
        return data;
    }

    public void InvalidateCache() => _cache.Clear();

    private async Task<List<LookupItemDto>> GetCachedLookupAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(endpoint, out var cached) && cached.expiry > DateTime.UtcNow)
            return (List<LookupItemDto>)cached.data;

        var result = await _apiClient.GetAsync<PagedResult<LookupItemDto>>(ApiEndpoints.Lookups.ByEndpoint(endpoint), cancellationToken);
        var data = result.Data?.Items ?? new List<LookupItemDto>();
        _cache[endpoint] = (DateTime.UtcNow.Add(CacheDuration), data);
        return data;
    }
}

public class LookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ModelLookupItemDto : LookupItemDto
{
    public int ManufacturerId { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
}
