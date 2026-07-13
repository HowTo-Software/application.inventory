using System.ComponentModel.DataAnnotations;
using ComputerInventory.Contracts;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Tracks update requests for computer agents.
/// Extracted from Computer entity in 3NF normalization (separate entity/concern).
/// Maps to COMPUTER_UPDATE_REQUESTS table.
/// </summary>
public class ComputerUpdateRequest
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    
    /// <summary>
    /// FK to UPDATE_STATUSES lookup table.
    /// </summary>
    public int UpdateStatusId { get; set; }
    
    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    [Range(0, 100)]
    public int UpdateProgress { get; set; }
    
    /// <summary>
    /// Current step description.
    /// </summary>
    [StringLength(500)]
    public string? UpdateStatusMessage { get; set; }
    
    /// <summary>
    /// When the update process started.
    /// </summary>
    public DateTime? UpdateStartedAt { get; set; }
    
    /// <summary>
    /// Error logs from failed updates.
    /// </summary>
    public string? UpdateErrorLogs { get; set; }
    
    /// <summary>
    /// When the update was requested.
    /// </summary>
    public DateTime? UpdateRequestedAt { get; set; }
    
    /// <summary>
    /// Who requested the update.
    /// </summary>
    [StringLength(100)]
    public string? UpdateRequestedBy { get; set; }
    
    /// <summary>
    /// Agent version before the update.
    /// </summary>
    [StringLength(50)]
    public string? PreviousAgentVersion { get; set; }
    
    /// <summary>
    /// When this update request was last updated.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }
    
    /// <summary>
    /// When this update request was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Computer Computer { get; set; } = null!;
    public UpdateStatusLookup UpdateStatusLookup { get; set; } = null!;
    
    // Convenience properties
    public bool HasErrorLogs => !string.IsNullOrEmpty(UpdateErrorLogs);
}
