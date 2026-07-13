using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Stores geolocation data for computers.
/// Supports both IP-based geolocation (from server lookups) and exact GPS coordinates (from Windows Location Services on agents).
/// Admin can choose which location source to display via PreferredLocationSource on the Computer entity.
/// </summary>
public class ComputerGeoLocation
{
    public int Id { get; set; }
    
    /// <summary>
    /// Foreign key to the Computer entity
    /// </summary>
    public int ComputerId { get; set; }
    
    /// <summary>
    /// The public IP address used to determine location (for IP-based geolocation)
    /// </summary>
    [StringLength(45)]
    public string? PublicIpAddress { get; set; }
    
    // ===== GPS Location (from Windows Location Services on agent) =====
    
    /// <summary>
    /// GPS Latitude coordinate from device (exact location)
    /// </summary>
    public double? GpsLatitude { get; set; }
    
    /// <summary>
    /// GPS Longitude coordinate from device (exact location)
    /// </summary>
    public double? GpsLongitude { get; set; }
    
    /// <summary>
    /// When GPS coordinates were last updated
    /// </summary>
    public DateTime? GpsLastUpdated { get; set; }
    
    // ===== IP-based Location (from ip-api.com lookups) =====
    
    /// <summary>
    /// IP-based Latitude coordinate (approximate location)
    /// </summary>
    public double? IpLatitude { get; set; }
    
    /// <summary>
    /// IP-based Longitude coordinate (approximate location)
    /// </summary>
    public double? IpLongitude { get; set; }
    
    /// <summary>
    /// When IP location was last updated
    /// </summary>
    public DateTime? IpLastUpdated { get; set; }
    
    // ===== Display coordinates (based on admin preference) =====
    
    /// <summary>
    /// Active Latitude coordinate (GPS or IP based on preference)
    /// </summary>
    public double? Latitude { get; set; }
    
    /// <summary>
    /// Active Longitude coordinate (GPS or IP based on preference)
    /// </summary>
    public double? Longitude { get; set; }
    
    /// <summary>
    /// City name from geolocation lookup
    /// </summary>
    [StringLength(100)]
    public string? City { get; set; }
    
    /// <summary>
    /// Region/State from geolocation lookup
    /// </summary>
    [StringLength(100)]
    public string? Region { get; set; }
    
    // FK to COUNTRIES
    public int? CountryId { get; set; }
    
    // Navigation property
    public Country? CountryEntity { get; set; }
    
    /// <summary>
    /// Internet Service Provider
    /// </summary>
    [StringLength(200)]
    public string? Isp { get; set; }
    
    /// <summary>
    /// Timezone of the location
    /// </summary>
    [StringLength(50)]
    public string? Timezone { get; set; }
    
    /// <summary>
    /// When the geolocation was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
    
    /// <summary>
    /// Whether the last geolocation lookup was successful
    /// </summary>
    public bool LookupSuccessful { get; set; }
    
    /// <summary>
    /// Error message if lookup failed
    /// </summary>
    [StringLength(500)]
    public string? LookupError { get; set; }
    
    /// <summary>
    /// Source of the location data: "GPS" for exact device location, "IP" for IP-based geolocation
    /// </summary>
    public string LocationSource { get; set; } = "IP";
    
    /// <summary>
    /// Accuracy of GPS location in meters (only applicable when LocationSource is "GPS")
    /// </summary>
    public double? GpsAccuracyMeters { get; set; }
    
    /// <summary>
    /// Whether GPS location is available for this computer
    /// </summary>
    public bool HasGpsLocation => GpsLatitude.HasValue && GpsLongitude.HasValue;
    
    /// <summary>
    /// Whether IP-based location is available for this computer
    /// </summary>
    public bool HasIpLocation => IpLatitude.HasValue && IpLongitude.HasValue;
    
    /// <summary>
    /// Indicates if this is an exact GPS location from the device (more accurate than IP-based)
    /// </summary>
    public bool IsExactGpsLocation => LocationSource == "GPS";
    
    // Navigation property
    [InverseProperty(nameof(Entities.Computer.GeoLocation))]
    public Computer Computer { get; set; } = null!;
}
