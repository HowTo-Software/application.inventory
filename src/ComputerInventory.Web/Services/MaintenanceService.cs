using ComputerInventory.Contracts;
using ComputerInventory.Web.Models;

namespace ComputerInventory.Web.Services;

public interface IMaintenanceService
{
    Task<ApiResult<PagedResult<MaintenanceRecordDto>>> GetPagedAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<ApiResult<PagedResult<MaintenanceRecordDto>>> GetByComputerIdAsync(int computerId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<ApiResult<MaintenanceRecordDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<MaintenanceRecordDto>> CreateAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> UpdateAsync(int id, UpdateMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> CompleteAsync(int id, decimal? cost, string? notes, bool setComputerAvailable, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IApiClient _apiClient;

    public MaintenanceService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<PagedResult<MaintenanceRecordDto>>> GetPagedAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiEndpoints.Maintenance.Paged}?page={page}&pageSize={pageSize}";
        return await _apiClient.GetAsync<PagedResult<MaintenanceRecordDto>>(url, cancellationToken);
    }

    public async Task<ApiResult<PagedResult<MaintenanceRecordDto>>> GetByComputerIdAsync(int computerId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiEndpoints.Maintenance.ByComputerId(computerId)}?page={page}&pageSize={pageSize}";
        return await _apiClient.GetAsync<PagedResult<MaintenanceRecordDto>>(url, cancellationToken);
    }

    public async Task<ApiResult<MaintenanceRecordDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<MaintenanceRecordDto>(ApiEndpoints.Maintenance.ById(id), cancellationToken);
    }

    public async Task<ApiResult<MaintenanceRecordDto>> CreateAsync(CreateMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<MaintenanceRecordDto>(ApiEndpoints.Maintenance.Base, request, cancellationToken);
    }

    public async Task<ApiResult> UpdateAsync(int id, UpdateMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PutAsync(ApiEndpoints.Maintenance.ById(id), request, cancellationToken);
    }

    public async Task<ApiResult> CompleteAsync(int id, decimal? cost, string? notes, bool setComputerAvailable, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PatchAsync(ApiEndpoints.Maintenance.Complete(id), new
        {
            Cost = cost,
            Notes = notes,
            SetComputerAvailable = setComputerAvailable
        }, cancellationToken);
    }

    public async Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Maintenance.ById(id), cancellationToken);
    }
}
