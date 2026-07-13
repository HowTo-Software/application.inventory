using System.Security.Claims;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ComputerInventory.Api.Authorization;

/// <summary>
/// Requirement for per-computer access checks.
/// Admins always pass. Non-admins must share a department with the computer OR have explicit ownership.
/// </summary>
public class SameDepartmentRequirement : IAuthorizationRequirement { }

/// <summary>
/// Handles per-computer authorization by checking:
/// 1. Admin role → always allowed
/// 2. Explicit ownership in ComputerOwnerships table → allowed
/// 3. Department match → allowed (for unassigned computers, all authenticated users pass)
/// </summary>
public class ComputerAuthorizationHandler : AuthorizationHandler<SameDepartmentRequirement, Computer>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ComputerAuthorizationHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SameDepartmentRequirement requirement,
        Computer computer)
    {
        // Admins can access all computers
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Parse user ID from claims
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
        {
            // Check explicit per-computer ownership
            using var scope = _scopeFactory.CreateScope();
            var ownershipRepo = scope.ServiceProvider.GetRequiredService<IComputerOwnershipRepository>();
            if (await ownershipRepo.HasAccessAsync(computer.Id, userId))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // Fall back to department-based check
        var departmentClaim = context.User.FindFirstValue("DepartmentId");

        // User has no department — deny access to any specific computer
        if (string.IsNullOrEmpty(departmentClaim) || !int.TryParse(departmentClaim, out var userDepartmentId))
        {
            return; // requirement not met → Forbid
        }

        // Computer has no department — allow access (unassigned computers are visible to all)
        if (!computer.DepartmentId.HasValue)
        {
            context.Succeed(requirement);
            return;
        }

        // Department match
        if (computer.DepartmentId.Value == userDepartmentId)
        {
            context.Succeed(requirement);
        }
    }
}
