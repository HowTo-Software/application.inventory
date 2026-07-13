using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface ILookupCrudService<TDto> where TDto : class, ILookupItemDto
{
    Task<ApiResult<List<TDto>>> GetAllAsync();
    Task<ApiResult<TDto>> CreateAsync(string name);
    Task<ApiResult> UpdateAsync(int id, string name);
    Task<ApiResult> DeleteAsync(int id);
}

public class LookupCrudService<TDto> : ILookupCrudService<TDto> where TDto : class, ILookupItemDto
{
    private readonly IApiClient _apiClient;
    private readonly string _baseEndpoint;
    private readonly Func<int, string> _byIdEndpoint;

    public LookupCrudService(IApiClient apiClient, string baseEndpoint, Func<int, string> byIdEndpoint)
    {
        _apiClient = apiClient;
        _baseEndpoint = baseEndpoint;
        _byIdEndpoint = byIdEndpoint;
    }

    public async Task<ApiResult<List<TDto>>> GetAllAsync()
    {
        var result = await _apiClient.GetAsync<List<TDto>>(_baseEndpoint);
        if (result.Success && result.Data == null)
            return ApiResult<List<TDto>>.Ok(new List<TDto>());
        return result;
    }

    public async Task<ApiResult<TDto>> CreateAsync(string name)
    {
        return await _apiClient.PostAsync<TDto>(_baseEndpoint, new { Name = name });
    }

    public async Task<ApiResult> UpdateAsync(int id, string name)
    {
        return await _apiClient.PutAsync(_byIdEndpoint(id), new { Name = name });
    }

    public async Task<ApiResult> DeleteAsync(int id)
    {
        return await _apiClient.DeleteAsync(_byIdEndpoint(id));
    }
}
