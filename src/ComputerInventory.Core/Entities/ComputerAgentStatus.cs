using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Tracks agent connectivity, update state, and runtime metadata for a computer.
/// Extracted from Computer entity to separate hardware/inventory data from agent state.
/// 1:1 relationship with Computer — stored in COMPUTER_AGENT_STATUSES table.
/// </summary>
public class ComputerAgentStatus
{
    public int Id { get; set; }
    public int ComputerId { get; set; }

    // Agent connectivity
    public DateTime? LastSeen { get; set; }
    public bool IsOnline { get; set; }
    public string? CurrentUser { get; set; }
    public bool HasAgent { get; set; }
    public string? PublicIpAddress { get; set; }

    [StringLength(50)]
    public string? AgentVersion { get; set; }

    [StringLength(64)]
    public string? AgentSessionId { get; set; }

    // Heartbeat configuration
    public int HeartbeatIntervalMinutes { get; set; } = 5;

    // Agent command flags
    public bool ForceUpdate { get; set; }
    public bool ForceReinstall { get; set; }
    public bool ReinstallAttempted { get; set; }
    public bool RequestImmediateCheckin { get; set; }

    /// <summary>
    /// Admin's preferred location source for map display: "GPS", "IP", or "Auto" (default)
    /// </summary>
    public string PreferredLocationSource { get; set; } = "Auto";

    // Update tracking — canonical state lives on ComputerUpdateRequest.
    // This FK points to the currently-active request (null = no update in progress).
    public int? ActiveUpdateRequestId { get; set; }

    // Navigation properties
    [InverseProperty(nameof(Entities.Computer.AgentStatus))]
    public Computer Computer { get; set; } = null!;
    public ComputerUpdateRequest? ActiveUpdateRequest { get; set; }

    /// <summary>
    /// Returns the active update request, creating one if it doesn't exist.
    /// Sets ComputerId and CreatedAt on the new request.
    /// </summary>
    public ComputerUpdateRequest EnsureActiveUpdateRequest()
    {
        if (ActiveUpdateRequest == null)
        {
            ActiveUpdateRequest = new ComputerUpdateRequest
            {
                ComputerId = ComputerId,
                CreatedAt = DateTime.UtcNow
            };
        }
        return ActiveUpdateRequest;
    }

    /// <summary>
    /// Clears the active update request link (does not delete the record).
    /// </summary>
    public void ClearActiveUpdateRequest()
    {
        ActiveUpdateRequestId = null;
        ActiveUpdateRequest = null;
    }
}
