using ComputerInventory.Contracts;
using ComputerInventory.Web.Models;

namespace ComputerInventory.Web.Services;

public interface IUserService
{
    Task<ApiResult<PagedResult<UserListDto>>> GetPagedAsync(int page = 1, int pageSize = 25, string? search = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<ApiResult<UserDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<UserListDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult> ActivateAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}

public class UserService : IUserService
{
    private readonly IApiClient _apiClient;

    public UserService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<PagedResult<UserListDto>>> GetPagedAsync(int page = 1, int pageSize = 25, string? search = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiEndpoints.Users.Base}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrWhiteSpace(sortBy)) url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
        if (!string.IsNullOrWhiteSpace(sortDirection)) url += $"&sortDirection={Uri.EscapeDataString(sortDirection)}";
        var result = await _apiClient.GetAsync<PagedResult<UserListDto>>(url, cancellationToken);
        if (result.Success && result.Data == null)
            return ApiResult<PagedResult<UserListDto>>.Ok(new PagedResult<UserListDto>());
        return result;
    }

    public async Task<ApiResult<UserDetailDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<UserDetailDto>(ApiEndpoints.Users.ById(id), cancellationToken);
    }

    public async Task<ApiResult<UserListDto>> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<UserListDto>(ApiEndpoints.Users.Base, request, cancellationToken);
    }

    public async Task<ApiResult> UpdateAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PutAsync(ApiEndpoints.Users.ById(id), request, cancellationToken);
    }

    public async Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Users.ById(id), cancellationToken);
    }

    public async Task<ApiResult> ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PatchAsync(ApiEndpoints.Users.Activate(id), cancellationToken: cancellationToken);
    }

    public async Task<ApiResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PatchAsync(ApiEndpoints.Users.Deactivate(id), cancellationToken: cancellationToken);
    }
}
