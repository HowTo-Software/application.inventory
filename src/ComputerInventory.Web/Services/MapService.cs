using ComputerInventory.Contracts;
using ComputerInventory.Web.Models;

namespace ComputerInventory.Web.Services;

public interface IMapService
{
    /// <summary>
    /// Get all computer locations with geolocation data
    /// </summary>
    Task<ApiResult<List<ComputerLocationDto>>> GetLocationsAsync();
    
    /// <summary>
    /// Get the last refresh time and status
    /// </summary>
    Task<ApiResult<MapRefreshInfo>> GetRefreshInfoAsync();
    
    /// <summary>
    /// Refresh geolocation data for all computers
    /// </summary>
    Task<ApiResult<GeoLocationRefreshResult>> RefreshAllLocationsAsync();
    
    /// <summary>
    /// Refresh geolocation for a specific computer
    /// </summary>
    Task<ApiResult<GeoLocationResult>> RefreshComputerLocationAsync(int computerId);
    
    /// <summary>
    /// Set the preferred location source for a computer
    /// </summary>
    Task<ApiResult> SetPreferredLocationSourceAsync(int computerId, string locationSource);
}

public class MapService : IMapService
{
    private readonly IApiClient _apiClient;

    public MapService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<ApiResult<List<ComputerLocationDto>>> GetLocationsAsync()
    {
        var result = await _apiClient.GetAsync<List<ComputerLocationDto>>(ApiEndpoints.Map.Locations);
        if (result.Success && result.Data == null)
            return ApiResult<List<ComputerLocationDto>>.Ok(new List<ComputerLocationDto>());
        return result;
    }

    public async Task<ApiResult<MapRefreshInfo>> GetRefreshInfoAsync()
    {
        return await _apiClient.GetAsync<MapRefreshInfo>(ApiEndpoints.Map.LastRefresh);
    }

    public async Task<ApiResult<GeoLocationRefreshResult>> RefreshAllLocationsAsync()
    {
        return await _apiClient.PostAsync<GeoLocationRefreshResult>(ApiEndpoints.Map.Refresh);
    }

    public async Task<ApiResult<GeoLocationResult>> RefreshComputerLocationAsync(int computerId)
    {
        return await _apiClient.PostAsync<GeoLocationResult>(ApiEndpoints.Map.RefreshComputer(computerId));
    }

    public async Task<ApiResult> SetPreferredLocationSourceAsync(int computerId, string locationSource)
    {
        return await _apiClient.PutAsync(ApiEndpoints.Map.LocationSource(computerId), new { LocationSource = locationSource });
    }
}
