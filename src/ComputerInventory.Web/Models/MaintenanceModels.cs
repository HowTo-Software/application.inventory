namespace ComputerInventory.Web.Models;

public class MaintenanceRecordDto
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public string? ComputerHostname { get; set; }
    public string? ComputerSerialNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? Cost { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateMaintenanceRequest
{
    public int ComputerId { get; set; }
    public string Type { get; set; } = "Repair";
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; } = DateTime.Today;
    public decimal? Cost { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public bool SetComputerInMaintenance { get; set; }
}

public class UpdateMaintenanceRequest
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? Cost { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
}
