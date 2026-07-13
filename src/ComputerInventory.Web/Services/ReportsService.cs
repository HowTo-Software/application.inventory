using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IReportsService
{
    Task<ApiResult<PagedResult<SystemDetailsReport>>> GetAllReportsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<ApiResult<List<ReportSummaryDto>>> GetReportSummariesAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<SystemDetailsReport>> GetReportByComputerIdAsync(int computerId, CancellationToken cancellationToken = default);
    Task<ApiResult<List<SystemDetailsReport>>> GetReportHistoryAsync(int computerId, CancellationToken cancellationToken = default);
}

public class ReportsService : IReportsService
{
    private readonly IApiClient _apiClient;

    public ReportsService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<PagedResult<SystemDetailsReport>>> GetAllReportsAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<PagedResult<SystemDetailsReport>>($"{ApiEndpoints.SystemDetails.Report}?page={page}&pageSize={pageSize}", cancellationToken);
        if (result.Success && result.Data == null)
            return ApiResult<PagedResult<SystemDetailsReport>>.Ok(new PagedResult<SystemDetailsReport>());
        return result;
    }

    public async Task<ApiResult<List<ReportSummaryDto>>> GetReportSummariesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<List<ReportSummaryDto>>(ApiEndpoints.SystemDetails.ReportSummaries, cancellationToken);
        if (result.Success && result.Data == null)
            return ApiResult<List<ReportSummaryDto>>.Ok(new List<ReportSummaryDto>());
        return result;
    }

    public async Task<ApiResult<SystemDetailsReport>> GetReportByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<SystemDetailsReport>(ApiEndpoints.SystemDetails.ByComputerId(computerId), cancellationToken);
    }

    public async Task<ApiResult<List<SystemDetailsReport>>> GetReportHistoryAsync(int computerId, CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<List<SystemDetailsReport>>(ApiEndpoints.SystemDetails.History(computerId), cancellationToken);
        if (result.Success && result.Data == null)
            return ApiResult<List<SystemDetailsReport>>.Ok(new List<SystemDetailsReport>());
        return result;
    }
}
