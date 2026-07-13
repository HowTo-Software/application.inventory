using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

/// <summary>
/// Service for IP geolocation lookups
/// </summary>
public interface IGeoLocationService
{
    /// <summary>
    /// Look up geolocation data for a given IP address
    /// </summary>
    Task<GeoLocationResult> LookupIpAsync(string ipAddress);
    
    /// <summary>
    /// Get all computer locations with geolocation data
    /// </summary>
    Task<IEnumerable<ComputerLocationDto>> GetAllComputerLocationsAsync();
    
    /// <summary>
    /// Refresh geolocation for all computers with public IPs
    /// </summary>
    Task<GeoLocationRefreshResult> RefreshAllLocationsAsync();
    
    /// <summary>
    /// Refresh geolocation for a specific computer
    /// </summary>
    Task<GeoLocationResult> RefreshComputerLocationAsync(int computerId);
    
    /// <summary>
    /// Get the last refresh time for geolocation data
    /// </summary>
    Task<DateTime?> GetLastRefreshTimeAsync();
    
    /// <summary>
    /// Check if a refresh is needed (more than 6 hours since last refresh)
    /// </summary>
    Task<bool> IsRefreshNeededAsync();
}

/// <summary>
/// Result of a geolocation lookup
/// </summary>
public class GeoLocationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? Isp { get; set; }
    public string? Timezone { get; set; }
}

/// <summary>
/// Result of refreshing all geolocation data
/// </summary>
public class GeoLocationRefreshResult
{
    public int TotalComputers { get; set; }
    public int SuccessfulLookups { get; set; }
    public int FailedLookups { get; set; }
    public int SkippedNoPublicIp { get; set; }
    public DateTime RefreshTime { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// DTO for computer location data used by the map
/// </summary>
public class ComputerLocationDto
{
    public int ComputerId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string? PublicIpAddress { get; set; }
    
    // Active/display coordinates (based on PreferredLocationSource)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    // GPS coordinates (exact location from device)
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public DateTime? GpsLastUpdated { get; set; }
    
    // IP-based coordinates (approximate location)
    public double? IpLatitude { get; set; }
    public double? IpLongitude { get; set; }
    public DateTime? IpLastUpdated { get; set; }
    
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public DateTime? LocationUpdated { get; set; }
    public string? CurrentUser { get; set; }
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Current source of active location data: "GPS" or "IP"
    /// </summary>
    public string LocationSource { get; set; } = "IP";
    
    /// <summary>
    /// Admin's preferred location source: "GPS", "IP", or "Auto"
    /// </summary>
    public string PreferredLocationSource { get; set; } = "Auto";
    
    /// <summary>
    /// Whether GPS location is available for this computer
    /// </summary>
    public bool HasGpsLocation => GpsLatitude.HasValue && GpsLongitude.HasValue;
    
    /// <summary>
    /// Whether IP-based location is available for this computer
    /// </summary>
    public bool HasIpLocation => IpLatitude.HasValue && IpLongitude.HasValue;    
    /// <summary>
    /// Indicates if this is an exact GPS location (more accurate than IP-based)
    /// </summary>
    public bool IsExactGpsLocation => LocationSource == "GPS";
}