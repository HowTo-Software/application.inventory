using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

/// <summary>
/// Enhanced computer service interface with proper error handling via ApiResult.
/// This provides detailed error feedback to the UI layer.
/// </summary>
public interface IComputerService
{
    Task<ApiResult<List<ComputerListDto>>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<PagedResult<ComputerListDto>>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null, string? status = null, string? type = null, string? location = null, string? department = null, string? connection = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default);
    Task<ApiResult<ComputerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<List<ComputerListDto>>> SearchAsync(string term, CancellationToken cancellationToken = default);
    Task<ApiResult<InventoryStats>> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<ComputerDto>> CreateAsync(CreateComputerRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> UpdateAsync(int id, CreateComputerRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default);
    Task<ApiResult> AssignUserAsync(int id, string? assignedTo, CancellationToken cancellationToken = default);
    Task<ApiResult<ForceUpdateResponse>> ForceUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<ForceUpdateResponse>> CancelUpdateAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<ForceUpdateResponse>> RequestImmediateCheckinAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult> ClearErrorLogsAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<ForceUpdateResponse>> OverrideUpdateAsync(int id, string? reason = null, CancellationToken cancellationToken = default);
    Task<ApiResult<ErrorAnalysisResult>> GetErrorAnalysisAsync(int id, CancellationToken cancellationToken = default);
    Task<ApiResult<byte[]>> DownloadErrorLogsAsync(int computerId, CancellationToken cancellationToken = default);
    Task<ApiResult<byte[]>> DownloadExportAsync(CancellationToken cancellationToken = default);
    Task<ApiResult<BulkOperationResult>> ImportCsvAsync(string csvContent, CancellationToken cancellationToken = default);
    Task<ApiResult<BulkOperationResult>> BulkUpdateStatusAsync(List<int> computerIds, string status, CancellationToken cancellationToken = default);
    Task<ApiResult<BulkOperationResult>> BulkDeleteAsync(List<int> computerIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Enhanced computer service with comprehensive error handling.
/// Uses the ApiClient to provide consistent error responses to the UI.
/// </summary>
public class ComputerService : IComputerService
{
    private readonly IApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComputerService> _logger;

    public ComputerService(IApiClient apiClient, HttpClient httpClient, ILogger<ComputerService> logger)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<ComputerListDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<List<ComputerListDto>>(ApiEndpoints.Computers.Base, cancellationToken);
        
        // Return empty list instead of null on success for easier UI handling
        if (result.Success && result.Data == null)
        {
            return ApiResult<List<ComputerListDto>>.Ok(new List<ComputerListDto>());
        }
        
        return result;
    }

    public async Task<ApiResult<PagedResult<ComputerListDto>>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null, string? status = null, string? type = null, string? location = null, string? department = null, string? connection = null, string? sortBy = null, string? sortDirection = null, CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(status)) queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(type)) queryParams.Add($"type={Uri.EscapeDataString(type)}");
        if (!string.IsNullOrWhiteSpace(location)) queryParams.Add($"location={Uri.EscapeDataString(location)}");
        if (!string.IsNullOrWhiteSpace(department)) queryParams.Add($"department={Uri.EscapeDataString(department)}");
        if (!string.IsNullOrWhiteSpace(connection)) queryParams.Add($"connection={Uri.EscapeDataString(connection)}");
        if (!string.IsNullOrWhiteSpace(sortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
        if (!string.IsNullOrWhiteSpace(sortDirection)) queryParams.Add($"sortDirection={Uri.EscapeDataString(sortDirection)}");

        var url = $"{ApiEndpoints.Computers.Paged}?{string.Join("&", queryParams)}";
        var result = await _apiClient.GetAsync<PagedResult<ComputerListDto>>(url, cancellationToken);

        if (result.Success && result.Data == null)
        {
            return ApiResult<PagedResult<ComputerListDto>>.Ok(new PagedResult<ComputerListDto>());
        }

        return result;
    }

    public async Task<ApiResult<ComputerDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<ComputerDto>(ApiEndpoints.Computers.ById(id), cancellationToken);
    }

    public async Task<ApiResult<List<ComputerListDto>>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return ApiResult<List<ComputerListDto>>.Ok(new List<ComputerListDto>());
        }

        var result = await _apiClient.GetAsync<List<ComputerListDto>>(
            ApiEndpoints.Computers.Search(term), 
            cancellationToken);

        if (result.Success && result.Data == null)
        {
            return ApiResult<List<ComputerListDto>>.Ok(new List<ComputerListDto>());
        }

        return result;
    }

    public async Task<ApiResult<InventoryStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetAsync<InventoryStats>(ApiEndpoints.Computers.Stats, cancellationToken);
        
        if (result.Success && result.Data == null)
        {
            return ApiResult<InventoryStats>.Ok(new InventoryStats());
        }

        return result;
    }

    public async Task<ApiResult<ComputerDto>> CreateAsync(CreateComputerRequest request, CancellationToken cancellationToken = default)
    {
        // Validate request before sending
        if (string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return ApiResult<ComputerDto>.Fail("Serial number is required.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Hostname))
        {
            return ApiResult<ComputerDto>.Fail("Hostname is required.", 400);
        }

        return await _apiClient.PostAsync<ComputerDto>(ApiEndpoints.Computers.Base, request, cancellationToken);
    }

    public async Task<ApiResult> UpdateAsync(int id, CreateComputerRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating computer {Id} with Status={Status}, Type={Type}", 
            id, request.Status, request.Type);
        
        return await _apiClient.PutAsync(ApiEndpoints.Computers.ById(id), request, cancellationToken);
    }

    public async Task<ApiResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Computers.ById(id), cancellationToken);
    }

    public async Task<ApiResult> UpdateStatusAsync(int id, string status, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PatchAsync(ApiEndpoints.Computers.Status(id), new { Status = status }, cancellationToken);
    }

    public async Task<ApiResult> AssignUserAsync(int id, string? assignedTo, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PatchAsync(ApiEndpoints.Computers.Assign(id), new { AssignedTo = assignedTo }, cancellationToken);
    }

    public async Task<ApiResult<ForceUpdateResponse>> ForceUpdateAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<ForceUpdateResponse>(ApiEndpoints.Computers.ForceUpdate(id), null, cancellationToken);
    }

    public async Task<ApiResult<ForceUpdateResponse>> CancelUpdateAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<ForceUpdateResponse>(ApiEndpoints.Computers.CancelUpdate(id), null, cancellationToken);
    }

    public async Task<ApiResult<ForceUpdateResponse>> RequestImmediateCheckinAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<ForceUpdateResponse>(ApiEndpoints.Computers.RequestCheckin(id), null, cancellationToken);
    }

    public async Task<ApiResult> ClearErrorLogsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Computers.ErrorLogs(id), cancellationToken);
    }

    public async Task<ApiResult<ForceUpdateResponse>> OverrideUpdateAsync(int id, string? reason = null, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<ForceUpdateResponse>(
            ApiEndpoints.Computers.OverrideUpdate(id), new { Reason = reason }, cancellationToken);
    }

    public async Task<ApiResult<ErrorAnalysisResult>> GetErrorAnalysisAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _apiClient.GetAsync<ErrorAnalysisResult>(ApiEndpoints.Computers.ErrorAnalysis(id), cancellationToken);
    }

    public async Task<ApiResult<byte[]>> DownloadErrorLogsAsync(int computerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(ApiEndpoints.Computers.ErrorLogs(computerId), cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiResult<byte[]>.Fail($"Failed to download error logs (HTTP {(int)response.StatusCode})", (int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ApiResult<byte[]>.Ok(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download error logs for computer {Id}", computerId);
            return ApiResult<byte[]>.Fail("Failed to download error logs");
        }
    }

    public async Task<ApiResult<byte[]>> DownloadExportAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(ApiEndpoints.Computers.Export, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return ApiResult<byte[]>.Fail($"Failed to download export (HTTP {(int)response.StatusCode})", (int)response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return ApiResult<byte[]>.Ok(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download CSV export");
            return ApiResult<byte[]>.Fail("Failed to download export");
        }
    }

    public async Task<ApiResult<BulkOperationResult>> ImportCsvAsync(string csvContent, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<BulkOperationResult>(ApiEndpoints.Computers.Import, csvContent, cancellationToken);
    }

    public async Task<ApiResult<BulkOperationResult>> BulkUpdateStatusAsync(List<int> computerIds, string status, CancellationToken cancellationToken = default)
    {
        var request = new BulkStatusUpdateRequest { ComputerIds = computerIds, Status = status };
        return await _apiClient.PostAsync<BulkOperationResult>(ApiEndpoints.Computers.BulkStatus, request, cancellationToken);
    }

    public async Task<ApiResult<BulkOperationResult>> BulkDeleteAsync(List<int> computerIds, CancellationToken cancellationToken = default)
    {
        return await _apiClient.PostAsync<BulkOperationResult>(ApiEndpoints.Computers.BulkDelete, computerIds, cancellationToken);
    }
}

/// <summary>
/// Response model for force update and similar operations.
/// Matches the API response structure from ComputersController.
/// </summary>
public class ForceUpdateResponse
{
    public string? Message { get; set; }
    public bool AlreadyPending { get; set; }
    public double? PreviousDuration { get; set; }
    public string? CurrentVersion { get; set; }
    public string? NextHeartbeatEstimate { get; set; }
    public double? WasPendingMinutes { get; set; }
    public string? Warning { get; set; }
}
