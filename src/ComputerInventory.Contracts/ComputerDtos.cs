#nullable enable

using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Contracts;

/// <summary>
/// Agent connectivity and identity status fields.
/// </summary>
public class ComputerAgentStatusDto
{
    public DateTime? LastSeen { get; set; }
    public bool IsOnline { get; set; }
    public string? CurrentUser { get; set; }
    public bool HasAgent { get; set; }
    public string? AgentVersion { get; set; }
    public string? AgentSessionId { get; set; }
    public int HeartbeatIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Update management and progress tracking fields.
/// </summary>
public class ComputerUpdateStatusDto
{
    public bool ForceUpdate { get; set; }
    public DateTime? UpdateRequestedAt { get; set; }
    public string? UpdateRequestedBy { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? PreviousAgentVersion { get; set; }
    public int UpdateStatus { get; set; }
    public int UpdateProgress { get; set; }
    public string? UpdateStatusMessage { get; set; }
    public DateTime? UpdateStartedAt { get; set; }
    public string? UpdateErrorLogs { get; set; }
    public bool HasUpdateErrorLogs { get; set; }
    public bool ReinstallAttempted { get; set; }
}

/// <summary>
/// Slim DTO for computer list endpoints — excludes heavy/detail-only fields
/// like Notes, hardware specs, purchase info, and full error logs.
/// </summary>
public class ComputerListDto
{
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ComputerAgentStatusDto AgentStatus { get; set; } = new();
    public ComputerUpdateStatusDto UpdateInfo { get; set; } = new();
}

/// <summary>
/// Full detail DTO for single-computer endpoints (get by id, create).
/// Includes hardware specs, purchase info, and notes not present in the list DTO.
/// </summary>
public class ComputerDto
{
    // Identity & Hardware
    public int Id { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? OperatingSystem { get; set; }
    public string? Processor { get; set; }
    public int? RamGB { get; set; }
    public int? StorageGB { get; set; }

    // Classification
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Department { get; set; }
    public string? Notes { get; set; }

    // Purchase Info
    public DateTime PurchaseDate { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    public decimal? PurchasePrice { get; set; }

    // Assignment
    public string? AssignedTo { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ComputerAgentStatusDto AgentStatus { get; set; } = new();
    public ComputerUpdateStatusDto UpdateInfo { get; set; } = new();
}

/// <summary>
/// Request model for creating a new computer.
/// </summary>
public class CreateComputerRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Hostname { get; set; } = string.Empty;
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [Required, StringLength(200)]
    public string Manufacturer { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Model { get; set; } = string.Empty;
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    [Required, StringLength(50)]
    public string Type { get; set; } = "Desktop";
    [Required, StringLength(50)]
    public string Status { get; set; } = ComputerStatusNames.Available;
    [StringLength(200)]
    public string? Location { get; set; }
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.Today;
    public DateTime? WarrantyExpiration { get; set; }
    [Range(0, 1_000_000)]
    public decimal? PurchasePrice { get; set; }
    [StringLength(200)]
    public string? AssignedTo { get; set; }
    [Range(1, 1440)]
    public int HeartbeatIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Inventory statistics summary.
/// </summary>
public class InventoryStats
{
    public int TotalComputers { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
}

/// <summary>
/// Response model for force update and similar operations.
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

/// <summary>
/// Request model for batch status update.
/// </summary>
public class BulkStatusUpdateRequest
{
    [Required]
    public List<int> ComputerIds { get; set; } = new();

    [Required, StringLength(50)]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Response model for bulk operations.
/// </summary>
public class BulkOperationResult
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// DTO for CSV export/import of computers.
/// </summary>
public class ComputerCsvRow
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Hostname { get; set; } = string.Empty;
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [Required, StringLength(200)]
    public string Manufacturer { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Model { get; set; } = string.Empty;
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    [Required, StringLength(50)]
    public string Type { get; set; } = "Desktop";
    [Required, StringLength(50)]
    public string Status { get; set; } = ComputerStatusNames.Available;
    [StringLength(200)]
    public string? Location { get; set; }
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    [StringLength(200)]
    public string? AssignedTo { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    [Range(0, 1000000)]
    public decimal? PurchasePrice { get; set; }
}
