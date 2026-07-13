namespace ComputerInventory.Contracts;

public class AuditLogDto
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public string ComputerName { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class AuditLogPagedResult
{
    public List<AuditLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<string> AvailableActions { get; set; } = new();
}
