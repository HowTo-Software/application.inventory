using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IInviteCodeService
{
    Task<ApiResult<List<InviteCodeListItem>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<InviteCodeResponse>> CreateAsync(CreateInviteCodeRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> RevokeAsync(int id, CancellationToken cancellationToken = default);
}

public class InviteCodeService : IInviteCodeService
{
    private readonly IApiClient _apiClient;

    public InviteCodeService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<List<InviteCodeListItem>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<List<InviteCodeListItem>>(ApiEndpoints.Auth.InviteCodes, cancellationToken);
        if (result.Success && result.Data == null)
            return ApiResult<List<InviteCodeListItem>>.Ok(new List<InviteCodeListItem>());
        return result;
    }

    public async Task<ApiResult<InviteCodeResponse>> CreateAsync(CreateInviteCodeRequest request, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<InviteCodeResponse>(ApiEndpoints.Auth.InviteCodes, request, cancellationToken);
    }

    public async Task<ApiResult> RevokeAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Auth.RevokeInviteCode(id), cancellationToken);
    }
}
