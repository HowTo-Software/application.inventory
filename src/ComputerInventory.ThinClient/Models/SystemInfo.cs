namespace ComputerInventory.ThinClient.Models;

public class SystemInfo
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? PublicIpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? OperatingSystem { get; set; }
    public string? OsVersion { get; set; }
    public string? Processor { get; set; }
    public int? RamGB { get; set; }
    public int? StorageGB { get; set; }
    public string? CurrentUser { get; set; }
    public DateTime LastBootTime { get; set; }
    public bool IsOnline { get; set; } = true;
    public string ComputerType { get; set; } = "Desktop";  // Desktop, Laptop, Tablet, Server, etc.
    
    // GPS Location (from Windows Location Services)
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public bool HasGpsLocation => GpsLatitude.HasValue && GpsLongitude.HasValue;
}

public class HeartbeatRequest
{
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CurrentUser { get; set; }
    public bool IsOnline { get; set; } = true;
    public string? IpAddress { get; set; }
    public string? PublicIpAddress { get; set; }
    public string? Hostname { get; set; }
    public string? MacAddress { get; set; }
    public string? OperatingSystem { get; set; }
    public string? AgentVersion { get; set; }  // Thin client version
    // Extended fields for auto-registration
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Processor { get; set; }
    public int? RamGB { get; set; }
    public int? StorageGB { get; set; }
    public string? Type { get; set; }  // Desktop, Laptop, Tablet, Server, etc.
    
    // GPS Location (exact coordinates from Windows Location Services)
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
}

public class CheckInRequest
{
    public string SerialNumber { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    
    /// <summary>
    /// The type of check-in operation. Use CheckInTypeExtensions.ToApiString() when serializing.
    /// </summary>
    public CheckInType Type { get; set; }
    
    /// <summary>
    /// String representation of Type for JSON serialization (API expects strings, not enum values).
    /// </summary>
    public string TypeString => Type.ToApiString();
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
}

public enum CheckInType
{
    CheckIn,
    CheckOut,
    AutoCheckOut
}

/// <summary>
/// Extension methods for CheckInType enum to ensure proper API serialization.
/// </summary>
public static class CheckInTypeExtensions
{
    /// <summary>
    /// Converts CheckInType to the string value expected by the API.
    /// </summary>
    public static string ToApiString(this CheckInType type) => type switch
    {
        CheckInType.CheckIn => "CheckIn",
        CheckInType.CheckOut => "CheckOut",
        CheckInType.AutoCheckOut => "AutoCheckOut",
        _ => type.ToString()
    };
}

public class AgentRegistrationResponse
{
    public bool Success { get; set; }
    public int? ComputerId { get; set; }
    public string? Message { get; set; }
    public AgentConfiguration? Configuration { get; set; }
}

public class AgentConfiguration
{
    public int HeartbeatIntervalMinutes { get; set; } = 5;
    public int CheckInIntervalMinutes { get; set; } = 15;
    public int IdleTimeoutMinutes { get; set; } = 30;
    public bool EnableAutoCheckOut { get; set; } = true;
}
