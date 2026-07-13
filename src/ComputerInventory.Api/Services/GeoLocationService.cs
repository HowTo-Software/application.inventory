using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Service for IP geolocation lookups using ip-api.com (free tier: 45 requests/minute)
/// </summary>
public class GeoLocationService : IGeoLocationService
{
    private readonly IGeoLocationRepository _geoLocationRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeoLocationService> _logger;
    private readonly GeoLocationSettings _settings;
    
    // Rate limiting for ip-api.com free tier (45 req/min)
    // TokenBucketRateLimiter allows concurrent waiters without holding a lock during delays,
    // unlike the previous SemaphoreSlim(1,1) which serialized all lookups globally.
    private static readonly TokenBucketRateLimiter _rateLimiter = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 3,                                        // small burst capacity
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 500,                                      // enough for full-fleet refresh
        ReplenishmentPeriod = TimeSpan.FromMilliseconds(1400), // ~43 requests per minute max
        TokensPerPeriod = 1,
        AutoReplenishment = true
    });
    
    // Cache duration - 6 hours
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public GeoLocationService(
        IGeoLocationRepository geoLocationRepository,
        IComputerRepository computerRepository,
        HttpClient httpClient,
        ILogger<GeoLocationService> logger,
        IOptions<GeoLocationSettings> settings)
    {
        _geoLocationRepository = geoLocationRepository;
        _computerRepository = computerRepository;
        _httpClient = httpClient;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<GeoLocationResult> LookupIpAsync(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return new GeoLocationResult 
            { 
                Success = false, 
                ErrorMessage = "IP address is required" 
            };
        }

        // Skip private/local IPs
        if (IsPrivateIp(ipAddress))
        {
            return new GeoLocationResult 
            { 
                Success = false, 
                ErrorMessage = "Cannot geolocate private IP addresses" 
            };
        }

        try
        {
            // Rate limiting — acquire a token (waits without holding a lock)
            using var lease = await _rateLimiter.AcquireAsync(1);
            if (!lease.IsAcquired)
            {
                return new GeoLocationResult
                {
                    Success = false,
                    ErrorMessage = "Rate limit queue full — try again later"
                };
            }

            // Call geolocation API (provider configurable via GeoLocation settings)
            var lookupUrl = $"{_settings.BaseUrl.TrimEnd('/')}{_settings.LookupPathTemplate.Replace("{ip}", ipAddress)}";
            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>(lookupUrl);

            if (response == null)
            {
                return new GeoLocationResult 
                { 
                    Success = false, 
                    ErrorMessage = "No response from geolocation service" 
                };
            }

            if (response.Status != "success")
            {
                return new GeoLocationResult 
                { 
                    Success = false, 
                    ErrorMessage = response.Message ?? "Geolocation lookup failed" 
                };
            }

            return new GeoLocationResult
            {
                Success = true,
                Latitude = response.Lat,
                Longitude = response.Lon,
                City = response.City,
                Region = response.RegionName,
                Country = response.Country,
                CountryCode = response.CountryCode,
                Isp = response.Isp,
                Timezone = response.Timezone
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up geolocation for IP {IpAddress}", ipAddress);
            return new GeoLocationResult 
            { 
                Success = false, 
                ErrorMessage = ex.Message 
            };
        }
    }

    public async Task<IEnumerable<ComputerLocationDto>> GetAllComputerLocationsAsync()
    {
        return await _geoLocationRepository.GetAllComputerLocationsSpAsync();
    }

    public async Task<GeoLocationRefreshResult> RefreshAllLocationsAsync()
    {
        var result = new GeoLocationRefreshResult
        {
            RefreshTime = DateTime.UtcNow
        };

        // Use explicit join to avoid INNER JOIN from non-nullable AgentStatus navigation.
        // Computers without an AgentStatus row would be silently excluded by Include.
        var computers = await _computerRepository.GetAllWithGeoLocationAsync();

        // Load AgentStatus separately via dictionary for efficiency
        var agentStatuses = await _computerRepository.GetAgentStatusDictionaryAsync();

        result.TotalComputers = computers.Count;

        foreach (var computer in computers)
        {
            if (!agentStatuses.TryGetValue(computer.Id, out var agentStatus) ||
                string.IsNullOrWhiteSpace(agentStatus.PublicIpAddress))
            {
                result.SkippedNoPublicIp++;
                continue;
            }

            var lookupResult = await LookupIpAsync(agentStatus.PublicIpAddress);

            if (lookupResult.Success)
            {
                var geo = computer.GeoLocation ?? new ComputerGeoLocation { ComputerId = computer.Id };

                geo.PublicIpAddress = agentStatus.PublicIpAddress;
                geo.IpLatitude = lookupResult.Latitude;
                geo.IpLongitude = lookupResult.Longitude;
                geo.IpLastUpdated = DateTime.UtcNow;
                // Also update legacy columns for backward compatibility
                geo.Latitude = lookupResult.Latitude;
                geo.Longitude = lookupResult.Longitude;
                geo.City = lookupResult.City;
                geo.Region = lookupResult.Region;
                geo.CountryId = await _geoLocationRepository.GetOrCreateCountryIdAsync(lookupResult.Country, lookupResult.CountryCode);
                geo.Isp = lookupResult.Isp;
                geo.Timezone = lookupResult.Timezone;
                geo.LastUpdated = DateTime.UtcNow;
                geo.LookupSuccessful = true;
                geo.LookupError = null;
                geo.LocationSource = "IP";

                await _geoLocationRepository.UpsertAsync(geo);

                result.SuccessfulLookups++;
            }
            else
            {
                if (computer.GeoLocation != null)
                {
                    computer.GeoLocation.LookupSuccessful = false;
                    computer.GeoLocation.LookupError = lookupResult.ErrorMessage;
                    computer.GeoLocation.LastUpdated = DateTime.UtcNow;

                    await _geoLocationRepository.UpsertAsync(computer.GeoLocation);
                }
                
                result.FailedLookups++;
                result.Errors.Add($"{computer.Hostname}: {lookupResult.ErrorMessage}");
            }
        }
        
        _logger.LogInformation(
            "Geolocation refresh completed: {Total} computers, {Success} successful, {Failed} failed, {Skipped} skipped",
            result.TotalComputers, result.SuccessfulLookups, result.FailedLookups, result.SkippedNoPublicIp);

        return result;
    }

    public async Task<GeoLocationResult> RefreshComputerLocationAsync(int computerId)
    {
        var computer = await _computerRepository.GetByIdWithGeoLocationAsync(computerId);

        if (computer == null)
        {
            return new GeoLocationResult 
            { 
                Success = false, 
                ErrorMessage = "Computer not found" 
            };
        }

        // Load AgentStatus separately to avoid INNER JOIN from non-nullable navigation
        var agentStatus = await _computerRepository.GetAgentStatusAsync(computerId);

        if (agentStatus == null || string.IsNullOrWhiteSpace(agentStatus.PublicIpAddress))
        {
            return new GeoLocationResult 
            { 
                Success = false, 
                ErrorMessage = "Computer does not have a public IP address" 
            };
        }

        var lookupResult = await LookupIpAsync(agentStatus.PublicIpAddress);

        if (lookupResult.Success)
        {
            var geo = computer.GeoLocation ?? new ComputerGeoLocation { ComputerId = computer.Id };

            geo.PublicIpAddress = agentStatus.PublicIpAddress;
            geo.Latitude = lookupResult.Latitude;
            geo.Longitude = lookupResult.Longitude;
            geo.City = lookupResult.City;
            geo.Region = lookupResult.Region;
            geo.CountryId = await _geoLocationRepository.GetOrCreateCountryIdAsync(lookupResult.Country, lookupResult.CountryCode);
            geo.Isp = lookupResult.Isp;
            geo.Timezone = lookupResult.Timezone;
            geo.LastUpdated = DateTime.UtcNow;
            geo.LookupSuccessful = true;
            geo.LookupError = null;

            await _geoLocationRepository.UpsertAsync(geo);
        }

        return lookupResult;
    }

    public async Task<DateTime?> GetLastRefreshTimeAsync()
    {
        return await _geoLocationRepository.GetLastSuccessfulLookupTimeAsync();
    }

    public async Task<bool> IsRefreshNeededAsync()
    {
        var lastRefresh = await GetLastRefreshTimeAsync();
        
        if (lastRefresh == null)
            return true;

        return DateTime.UtcNow - lastRefresh.Value > CacheDuration;
    }

    private static bool IsPrivateIp(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return true;

        // Handle localhost
        if (ipAddress == "127.0.0.1" || ipAddress == "::1" || ipAddress.ToLower() == "localhost")
            return true;

        // Try parsing as IPv4
        var parts = ipAddress.Split('.');
        if (parts.Length == 4 && parts.All(p => byte.TryParse(p, out _)))
        {
            var bytes = parts.Select(byte.Parse).ToArray();
            
            // 10.x.x.x
            if (bytes[0] == 10)
                return true;
            
            // 172.16.x.x - 172.31.x.x
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            
            // 192.168.x.x
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            
            // 169.254.x.x (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
        }

        return false;
    }
}

/// <summary>
/// Response model from ip-api.com
/// </summary>
internal class IpApiResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    
    [JsonPropertyName("country")]
    public string? Country { get; set; }
    
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; set; }
    
    [JsonPropertyName("region")]
    public string? Region { get; set; }
    
    [JsonPropertyName("regionName")]
    public string? RegionName { get; set; }
    
    [JsonPropertyName("city")]
    public string? City { get; set; }
    
    [JsonPropertyName("lat")]
    public double? Lat { get; set; }
    
    [JsonPropertyName("lon")]
    public double? Lon { get; set; }
    
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
    
    [JsonPropertyName("isp")]
    public string? Isp { get; set; }
}
