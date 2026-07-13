using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IDashboardService
{
    Task<ApiResult<DashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default);
}

public class DashboardService : IDashboardService
{
    private readonly IApiClient _apiClient;

    public DashboardService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<DashboardDto>> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<DashboardDto>(ApiEndpoints.Dashboard.Base, cancellationToken);
    }
}
