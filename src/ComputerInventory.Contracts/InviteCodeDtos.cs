using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Contracts;

public class InviteCodeListItem
{
    public int Id { get; set; }
    public int MaxUses { get; set; }
    public int UseCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsValid { get; set; }
}

public class InviteCodeResponse
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int MaxUses { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? Note { get; set; }
}

public class CreateInviteCodeRequest
{
    [Range(1, 1000)]
    public int MaxUses { get; set; } = 1;
    [Range(1, 8760)]
    public int ExpirationHours { get; set; } = 72;
    [StringLength(500)]
    public string? Note { get; set; }
}
