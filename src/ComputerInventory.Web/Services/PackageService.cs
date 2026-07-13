using System.Net.Http.Headers;
using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IPackageService
{
    Task<ApiResult<List<PackageVersionDto>>> GetAllPackagesAsync();
    Task<ApiResult<PackageVersionDetailDto>> GetPackageByIdAsync(int id);
    Task<ApiResult> UploadPackageAsync(Stream fileStream, string fileName, string version,
        string? releaseNotes, bool isRequired, bool isBreakingChange,
        string? minimumAgentVersion, string channel);
    Task<ApiResult> UpdatePackageAsync(int id, UpdatePackageRequest request);
    Task<ApiResult> DeletePackageAsync(int id);
    Task<ApiResult> PublishPackageAsync(int id);
    Task<ApiResult> ActivatePackageAsync(int id);
    Task<ApiResult> DeprecatePackageAsync(int id);
    Task<ApiResult> RollbackToVersionAsync(int id);
    Task<ApiResult> AddDistributionRuleAsync(int packageId, AddDistributionRuleRequest request);
    Task<ApiResult> RemoveDistributionRuleAsync(int packageId, int ruleId);
    Task<ApiResult<PackageDistributionSummaryDto>> GetDistributionSummaryAsync();
}

public class PackageService : IPackageService
{
    private readonly IApiClient _apiClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PackageService> _logger;

    public PackageService(IApiClient apiClient, HttpClient httpClient, ILogger<PackageService> logger)
    {
        _apiClient = apiClient;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<PackageVersionDto>>> GetAllPackagesAsync()
    {
        var result = await _apiClient.GetAsync<List<PackageVersionDto>>(ApiEndpoints.Packages.Base);
        if (result.Success && result.Data == null)
            return ApiResult<List<PackageVersionDto>>.Ok(new List<PackageVersionDto>());
        return result;
    }

    public async Task<ApiResult<PackageVersionDetailDto>> GetPackageByIdAsync(int id)
    {
        return await _apiClient.GetAsync<PackageVersionDetailDto>(ApiEndpoints.Packages.ById(id));
    }

    public async Task<ApiResult> UploadPackageAsync(Stream fileStream, string fileName, string version,
        string? releaseNotes, bool isRequired, bool isBreakingChange,
        string? minimumAgentVersion, string channel)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            content.Add(streamContent, "package", fileName);
            content.Add(new StringContent(version), "version");
            content.Add(new StringContent(isRequired.ToString()), "isRequired");
            content.Add(new StringContent(isBreakingChange.ToString()), "isBreakingChange");
            content.Add(new StringContent(channel), "channel");
            if (!string.IsNullOrEmpty(releaseNotes))
                content.Add(new StringContent(releaseNotes), "releaseNotes");
            if (!string.IsNullOrEmpty(minimumAgentVersion))
                content.Add(new StringContent(minimumAgentVersion), "minimumAgentVersion");

            var response = await _httpClient.PostAsync(ApiEndpoints.Packages.Upload, content);
            if (response.IsSuccessStatusCode)
                return ApiResult.Ok();

            var errorBody = await response.Content.ReadAsStringAsync();
            return ApiResult.Fail(
                string.IsNullOrWhiteSpace(errorBody) ? $"Upload failed (HTTP {(int)response.StatusCode})" : errorBody,
                (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error uploading package");
            return ApiResult.NetworkError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload package");
            return ApiResult.ServerError(details: ex.Message);
        }
    }

    public async Task<ApiResult> UpdatePackageAsync(int id, UpdatePackageRequest request)
    {
        return await _apiClient.PutAsync(ApiEndpoints.Packages.ById(id), request);
    }

    public async Task<ApiResult> DeletePackageAsync(int id)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Packages.ById(id));
    }

    public async Task<ApiResult> PublishPackageAsync(int id)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Packages.Publish(id));
    }

    public async Task<ApiResult> ActivatePackageAsync(int id)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Packages.Activate(id));
    }

    public async Task<ApiResult> DeprecatePackageAsync(int id)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Packages.Deprecate(id));
    }

    public async Task<ApiResult> RollbackToVersionAsync(int id)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Packages.Rollback(id));
    }

    public async Task<ApiResult> AddDistributionRuleAsync(int packageId, AddDistributionRuleRequest request)
    {
        return await _apiClient.PostAsync(ApiEndpoints.Packages.Rules(packageId), request);
    }

    public async Task<ApiResult> RemoveDistributionRuleAsync(int packageId, int ruleId)
    {
        return await _apiClient.DeleteAsync(ApiEndpoints.Packages.RemoveRule(packageId, ruleId));
    }

    public async Task<ApiResult<PackageDistributionSummaryDto>> GetDistributionSummaryAsync()
    {
        return await _apiClient.GetAsync<PackageDistributionSummaryDto>(ApiEndpoints.Packages.DistributionSummary);
    }
}
