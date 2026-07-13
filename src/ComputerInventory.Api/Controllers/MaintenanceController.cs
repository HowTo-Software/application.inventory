using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IMaintenanceRepository maintenanceRepository, IComputerRepository computerRepository, ILogger<MaintenanceController> logger)
    {
        _maintenanceRepository = maintenanceRepository;
        _computerRepository = computerRepository;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MaintenanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        return await GetPaged(page, pageSize, sortBy, sortDirection, cancellationToken);
    }

    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<MaintenanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var (items, totalCount) = await _maintenanceRepository.GetPagedAsync(page, pageSize, sortBy, sortDirection, cancellationToken);

            var result = new PagedResult<MaintenanceRecordDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged maintenance records (page={Page}, pageSize={PageSize})", page, pageSize);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving maintenance records.");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(MaintenanceRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MaintenanceRecordDto>> GetById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var record = await _maintenanceRepository.GetByIdAsync(id, cancellationToken);
            if (record == null)
            {
                return NotFound();
            }
            return Ok(MapToDto(record));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance record {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving the maintenance record.");
        }
    }

    [HttpGet("computer/{computerId}")]
    [ProducesResponseType(typeof(PagedResult<MaintenanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetByComputerId(
        int computerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var (items, totalCount) = await _maintenanceRepository.GetByComputerIdPagedAsync(computerId, page, pageSize, cancellationToken);

            var result = new PagedResult<MaintenanceRecordDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records for computer {ComputerId}", computerId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving maintenance records.");
        }
    }

    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(PagedResult<MaintenanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetByStatus(
        MaintenanceStatus status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var (items, totalCount) = await _maintenanceRepository.GetByStatusPagedAsync(status, page, pageSize, cancellationToken);

            var result = new PagedResult<MaintenanceRecordDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records with status {Status}", status);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving maintenance records.");
        }
    }

    [HttpGet("scheduled")]
    [ProducesResponseType(typeof(PagedResult<MaintenanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetScheduled(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (start > end)
                return BadRequest("'start' must be less than or equal to 'end'.");

            const int maxRangeDays = 365;
            if ((end - start).TotalDays > maxRangeDays)
                return BadRequest($"Date range must not exceed {maxRangeDays} days.");

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 200) pageSize = 200;

            var (items, totalCount) = await _maintenanceRepository.GetScheduledBetweenDatesPagedAsync(start, end, page, pageSize, cancellationToken);

            var result = new PagedResult<MaintenanceRecordDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled maintenance records between {Start} and {End}", start, end);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred while retrieving scheduled maintenance records.");
        }
    }

    [HttpPost]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(MaintenanceRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MaintenanceRecordDto>> Create([FromBody] CreateMaintenanceRequest request, CancellationToken cancellationToken)
    {
        var computer = await _computerRepository.GetByIdAsync(request.ComputerId, cancellationToken);
        if (computer == null)
        {
            return BadRequest("Computer not found");
        }

        if (request.SetComputerInMaintenance && computer.StatusId == (int)ComputerStatus.InMaintenance)
        {
            return Conflict("Computer is already in maintenance.");
        }

        var record = new MaintenanceRecord
        {
            ComputerId = request.ComputerId,
            TypeId = (int)request.Type,
            Description = request.Description,
            ScheduledDate = request.ScheduledDate,
            Cost = request.Cost,
            PerformedBy = request.PerformedBy,
            Notes = request.Notes,
            StatusId = (int)MaintenanceStatus.Scheduled
        };

        var created = await _maintenanceRepository.AddAsync(record, cancellationToken);
        
        // Update computer status if maintenance is starting
        if (request.SetComputerInMaintenance)
        {
            computer.StatusId = (int)ComputerStatus.InMaintenance;
            await _computerRepository.UpdateAsync(computer, cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateMaintenanceRequest request, CancellationToken cancellationToken)
    {
        var record = await _maintenanceRepository.GetByIdAsync(id, cancellationToken);
        if (record == null)
        {
            return NotFound();
        }

        record.TypeId = (int)request.Type;
        record.Description = request.Description;
        record.ScheduledDate = request.ScheduledDate;
        record.CompletedDate = request.CompletedDate;
        record.Cost = request.Cost;
        record.PerformedBy = request.PerformedBy;
        record.Notes = request.Notes;
        record.StatusId = (int)request.Status;

        await _maintenanceRepository.UpdateAsync(record, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var record = await _maintenanceRepository.GetByIdAsync(id, cancellationToken);
        if (record == null)
        {
            return NotFound();
        }

        await _maintenanceRepository.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id}/complete")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Complete(int id, [FromBody] CompleteMaintenanceRequest request, CancellationToken cancellationToken)
    {
        var record = await _maintenanceRepository.GetByIdAsync(id, cancellationToken);
        if (record == null)
        {
            return NotFound();
        }

        record.StatusId = (int)MaintenanceStatus.Completed;
        record.CompletedDate = DateTime.UtcNow;
        record.Cost = request.Cost;
        record.Notes = request.Notes;

        await _maintenanceRepository.UpdateAsync(record, cancellationToken);

        // Update computer status back to available if requested
        if (request.SetComputerAvailable)
        {
            var computer = await _computerRepository.GetByIdAsync(record.ComputerId, cancellationToken);
            if (computer != null)
            {
                computer.StatusId = (int)ComputerStatus.Available;
                await _computerRepository.UpdateAsync(computer, cancellationToken);
            }
        }

        return NoContent();
    }

    private static MaintenanceRecordDto MapToDto(MaintenanceRecord record) => new()
    {
        Id = record.Id,
        ComputerId = record.ComputerId,
        ComputerHostname = record.Computer?.Hostname,
        ComputerSerialNumber = record.Computer?.SerialNumber,
        Type = record.TypeId.ToString(),
        Description = record.Description,
        ScheduledDate = record.ScheduledDate,
        CompletedDate = record.CompletedDate,
        Cost = record.Cost,
        PerformedBy = record.PerformedBy,
        Notes = record.Notes,
        Status = record.StatusId.ToString(),
        CreatedAt = record.CreatedAt,
        CreatedBy = record.CreatedBy
    };
}

public class MaintenanceRecordDto
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public string? ComputerHostname { get; set; }
    public string? ComputerSerialNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? Cost { get; set; }
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateMaintenanceRequest
{
    public int ComputerId { get; set; }
    public MaintenanceType Type { get; set; }
    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    [Range(0, 1_000_000)]
    public decimal? Cost { get; set; }
    [StringLength(200)]
    public string? PerformedBy { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool SetComputerInMaintenance { get; set; }
}

public class UpdateMaintenanceRequest
{
    public MaintenanceType Type { get; set; }
    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    [Range(0, 1_000_000)]
    public decimal? Cost { get; set; }
    [StringLength(200)]
    public string? PerformedBy { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public MaintenanceStatus Status { get; set; }
}

public class CompleteMaintenanceRequest
{
    [Range(0, 1_000_000)]
    public decimal? Cost { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public bool SetComputerAvailable { get; set; }
}
