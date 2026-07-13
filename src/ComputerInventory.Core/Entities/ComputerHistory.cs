using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

public class ComputerHistory : ISoftDeletable
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    [InverseProperty(nameof(Entities.Computer.History))]
    public Computer Computer { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Action { get; set; } = string.Empty;

    [StringLength(500)]
    public string? OldValue { get; set; }

    [StringLength(500)]
    public string? NewValue { get; set; }

    [StringLength(100)]
    public string? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Concurrency token
    public byte[]? RowVersion { get; set; }

    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}
