using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IVersionService
{
    Task<ApiResult<VersionHistoryResponse>> GetVersionHistoryAsync(int page = 1, int pageSize = 50);
    Task<ApiResult<VersionInfo>> GetCurrentServerVersionAsync();
    Task<ApiResult<VersionInfo>> GetCurrentThinClientVersionAsync();
    Task<ApiResult> AddVersionHistoryAsync(CreateVersionHistoryRequest request);
    Task<ApiResult> UpdateVersionHistoryAsync(int id, UpdateVersionHistoryRequest request);
    Task<ApiResult> DeleteVersionHistoryAsync(int id);
}

public class VersionService : IVersionService
{
    private readonly IApiClient _apiClient;

    public VersionService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<VersionHistoryResponse>> GetVersionHistoryAsync(int page = 1, int pageSize = 50)
    {
        return await _apiClient.GetAsync<VersionHistoryResponse>($"{ApiEndpoints.Version.History}?page={page}&pageSize={pageSize}");
    }

    public async Task<ApiResult<VersionInfo>> GetCurrentServerVersionAsync()
    {
        return await _apiClient.GetAsync<VersionInfo>(ApiEndpoints.Version.ServerCurrent);
    }

    public async Task<ApiResult<VersionInfo>> GetCurrentThinClientVersionAsync()
    {
        return await _apiClient.GetAsync<VersionInfo>(ApiEndpoints.Version.ThinClientCurrent);
    }

    public async Task<ApiResult> AddVersionHistoryAsync(CreateVersionHistoryRequest request)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Version.History, request);
    }

    public async Task<ApiResult> UpdateVersionHistoryAsync(int id, UpdateVersionHistoryRequest request)
    {
        return await _apiClient.PutAsync(ApiEndpoints.Version.HistoryById(id), request);
    }

    public async Task<ApiResult> DeleteVersionHistoryAsync(int id)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Version.HistoryById(id));
    }
}
