using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Contracts;

public class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class VersionHistoryItem
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ChangeLog { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public string? ReleasedBy { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class VersionHistoryResponse
{
    public string CurrentServerVersion { get; set; } = string.Empty;
    public string CurrentThinClientVersion { get; set; } = string.Empty;
    public List<VersionHistoryItem> ServerHistory { get; set; } = new();
    public List<VersionHistoryItem> ThinClientHistory { get; set; } = new();
    public int ServerTotalCount { get; set; }
    public int ThinClientTotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int ServerTotalPages => PageSize > 0 ? (int)Math.Ceiling((double)ServerTotalCount / PageSize) : 0;
    public int ThinClientTotalPages => PageSize > 0 ? (int)Math.Ceiling((double)ThinClientTotalCount / PageSize) : 0;
}

public class CreateVersionHistoryRequest
{
    [Required, StringLength(50)]
    public string Version { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string Type { get; set; } = string.Empty;
    [Required, StringLength(5000)]
    public string ChangeLog { get; set; } = string.Empty;
    public DateTime? ReleasedAt { get; set; }
    public bool IsCurrentVersion { get; set; }
    [StringLength(128)]
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class UpdateVersionHistoryRequest
{
    [Required, StringLength(5000)]
    public string ChangeLog { get; set; } = string.Empty;
    public bool IsCurrentVersion { get; set; }
}
