using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ComputerInventory.Api.Controllers;

/// <summary>
/// Manage per-computer ownership assignments. Admins can grant/revoke access for any user.
/// Technicians can view ownership for computers in their department.
/// </summary>
[ApiController]
[Route("api/computers/{computerId}/ownership")]
[Authorize]
public class ComputerOwnershipController : ControllerBase
{
    private readonly IComputerOwnershipRepository _ownershipRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly IUserRepository _userRepository;

    public ComputerOwnershipController(
        IComputerOwnershipRepository ownershipRepository,
        IComputerRepository computerRepository,
        IUserRepository userRepository)
    {
        _ownershipRepository = ownershipRepository;
        _computerRepository = computerRepository;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Get all users with access to a computer
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "TechnicianOrAdmin")]
    public async Task<IActionResult> GetOwners(int computerId)
    {
        var computer = await _computerRepository.GetByIdAsync(computerId);
        if (computer == null)
            return NotFound(new { error = "Computer not found" });

        var ownerships = await _ownershipRepository.GetByComputerIdAsync(computerId);
        var result = ownerships.Select(o => new
        {
            o.UserId,
            Username = o.User?.Username,
            FullName = o.User?.FullName,
            AccessLevel = o.AccessLevel,
            o.GrantedAt,
            o.GrantedBy
        });

        return Ok(result);
    }

    /// <summary>
    /// Grant a user access to a computer
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GrantAccess(int computerId, [FromBody] GrantAccessRequest request, CancellationToken cancellationToken)
    {
        var computer = await _computerRepository.GetByIdAsync(computerId);
        if (computer == null)
            return NotFound(new { error = "Computer not found" });

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            return NotFound(new { error = "User not found" });

        var existing = await _ownershipRepository.GetAsync(computerId, request.UserId);
        if (existing != null)
            return Conflict(new { error = "User already has access to this computer" });

        var grantedBy = User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        var ownership = new ComputerOwnership
        {
            ComputerId = computerId,
            UserId = request.UserId,
            AccessLevel = (int)request.AccessLevel,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = grantedBy
        };

        await _ownershipRepository.AddAsync(ownership);

        return Ok(new { message = $"Access granted to {user.Username} for computer {computer.Hostname}" });
    }

    /// <summary>
    /// Revoke a user's access to a computer
    /// </summary>
    [HttpDelete("{userId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RevokeAccess(int computerId, int userId)
    {
        var existing = await _ownershipRepository.GetAsync(computerId, userId);
        if (existing == null)
            return NotFound(new { error = "Ownership record not found" });

        await _ownershipRepository.RemoveAsync(computerId, userId);

        return Ok(new { message = "Access revoked" });
    }
}

public class GrantAccessRequest
{
    public int UserId { get; set; }
    public ComputerAccessLevel AccessLevel { get; set; } = ComputerAccessLevel.Viewer;
}
