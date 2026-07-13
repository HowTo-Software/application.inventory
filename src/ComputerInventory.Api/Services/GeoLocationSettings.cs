namespace ComputerInventory.Api.Services;

/// <summary>
/// Configuration for IP geolocation provider. Bound from "GeoLocation" section in appsettings.json.
/// </summary>
public class GeoLocationSettings
{
    public const string SectionName = "GeoLocation";

    /// <summary>
    /// Base URL for the geolocation API (e.g. "http://ip-api.com").
    /// </summary>
    public string BaseUrl { get; set; } = "http://ip-api.com";

    /// <summary>
    /// URL template for IP lookup. Use {ip} as placeholder for the IP address.
    /// </summary>
    public string LookupPathTemplate { get; set; } = "/json/{ip}?fields=status,message,country,countryCode,region,regionName,city,lat,lon,timezone,isp";
}
