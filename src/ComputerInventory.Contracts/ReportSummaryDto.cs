namespace ComputerInventory.Contracts;

public class ReportSummaryDto
{
    public int Id { get; set; }
    public int? ComputerId { get; set; }
    public string? ComputerName { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CollectedAt { get; set; }
}
