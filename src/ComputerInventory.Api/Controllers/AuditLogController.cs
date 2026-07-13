using ComputerInventory.Contracts;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class AuditLogController : ControllerBase
{
    private readonly IComputerHistoryRepository _historyRepository;

    public AuditLogController(IComputerHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AuditLogPagedResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogPagedResult>> GetAuditLog(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _historyRepository.GetPagedAsync(
            page, pageSize, action, search, from, to, sortBy, sortDirection, cancellationToken);

        var actions = await _historyRepository.GetDistinctActionsAsync(cancellationToken);

        var result = new AuditLogPagedResult
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            AvailableActions = actions,
            Items = items.Select(h => new AuditLogDto
            {
                Id = h.Id,
                ComputerId = h.ComputerId,
                ComputerName = h.Computer?.Hostname ?? "Unknown",
                SerialNumber = h.Computer?.SerialNumber,
                Action = h.Action,
                OldValue = h.OldValue,
                NewValue = h.NewValue,
                ChangedBy = h.ChangedBy,
                ChangedAt = h.ChangedAt,
                Notes = h.Notes
            }).ToList()
        };

        return Ok(result);
    }

    [HttpDelete("purge")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> PurgeOldEntries(
        [FromQuery] int days = 90,
        CancellationToken cancellationToken = default)
    {
        if (days < 1) days = 1;

        var cutoff = DateTime.UtcNow.AddDays(-days);
        var deleted = await _historyRepository.DeleteOlderThanAsync(cutoff, cancellationToken);

        return Ok(new { deleted, cutoffDate = cutoff });
    }
}
