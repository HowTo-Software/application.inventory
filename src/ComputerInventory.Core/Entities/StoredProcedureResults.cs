namespace ComputerInventory.Core.Entities;

/// <summary>
/// Keyless result type for <c>sp_get_status_counts</c>.
/// Returns computer count grouped by status.
/// </summary>
public class StatusCountResult
{
    public int StatusId { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Keyless result type for <c>sp_get_all_computer_locations</c>.
/// Returns all computers with GPS/IP coordinate data for map display.
/// </summary>
public class ComputerLocationResult
{
    public int Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public int? StatusId { get; set; }
    public string? PublicIpAddress { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastSeen { get; set; }
    public string? CurrentUser { get; set; }
    public string? PreferredLocationSource { get; set; }
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public DateTime? GpsLastUpdated { get; set; }
    public double? IpLatitude { get; set; }
    public double? IpLongitude { get; set; }
    public DateTime? IpLastUpdated { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public DateTime? LocationUpdated { get; set; }
    public double? LegacyLatitude { get; set; }
    public double? LegacyLongitude { get; set; }
    public string? LegacyLocationSource { get; set; }
}
