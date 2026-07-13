using ComputerInventory.Contracts;
using ComputerInventory.Web.Services;

namespace ComputerInventory.Web.Services;

public interface IAuditLogService
{
    Task<ApiResult<AuditLogPagedResult>> GetAuditLogAsync(int page = 1, int pageSize = 50, string? action = null, string? search = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<ApiResult> PurgeAsync(int days = 90, CancellationToken cancellationToken = default);
}

public class AuditLogService : IAuditLogService
{
    private readonly IApiClient _apiClient;

    public AuditLogService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<AuditLogPagedResult>> GetAuditLogAsync(
        int page = 1, int pageSize = 50, string? action = null, string? search = null,
        DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrEmpty(action)) queryParams.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrEmpty(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (from.HasValue) queryParams.Add($"from={from.Value:yyyy-MM-ddTHH:mm:ss}");
        if (to.HasValue) queryParams.Add($"to={to.Value:yyyy-MM-ddTHH:mm:ss}");

        var url = $"{ApiEndpoints.AuditLog.Base}?{string.Join("&", queryParams)}";
        return await _apiClient.GetAsync<AuditLogPagedResult>(url, cancellationToken);
    }

    public async Task<ApiResult> PurgeAsync(int days = 90, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiEndpoints.AuditLog.Purge}?days={days}";
        return await _apiClient.DeleteAsync(url, cancellationToken);
    }
}
