using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Core.Entities;

public class AppSetting
{
    [Required]
    [StringLength(128)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(1024)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
