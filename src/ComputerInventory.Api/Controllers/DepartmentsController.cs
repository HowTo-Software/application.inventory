using ComputerInventory.Api.Services;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class DepartmentsController : ControllerBase
{
    private readonly ILookupRepository _lookupRepository;
    private readonly ICacheService _cache;

    public DepartmentsController(ILookupRepository lookupRepository, ICacheService cache)
    {
        _lookupRepository = lookupRepository;
        _cache = cache;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DepartmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _lookupRepository.GetDepartmentsAsync(cancellationToken);
        var dtos = new List<DepartmentDto>();
        foreach (var d in departments)
        {
            var usageCount = await _lookupRepository.GetDepartmentUsageCountAsync(d.Id, cancellationToken);
            dtos.Add(new DepartmentDto { Id = d.Id, Name = d.Name, UsageCount = usageCount });
        }
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartmentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var department = await _lookupRepository.GetDepartmentByIdAsync(id, cancellationToken);
        if (department == null)
            return NotFound();

        var usageCount = await _lookupRepository.GetDepartmentUsageCountAsync(id, cancellationToken);
        return Ok(new DepartmentDto { Id = department.Id, Name = department.Name, UsageCount = usageCount });
    }

    [HttpPost]
    [ProducesResponseType(typeof(DepartmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (await _lookupRepository.DepartmentNameExistsAsync(request.Name, cancellationToken: cancellationToken))
            return BadRequest("A department with this name already exists");

        var department = new Department { Name = request.Name.Trim() };
        var created = await _lookupRepository.AddDepartmentAsync(department, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupDepartments);
        var dto = new DepartmentDto { Id = created.Id, Name = created.Name, UsageCount = 0 };
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var department = await _lookupRepository.GetDepartmentByIdAsync(id, cancellationToken);
        if (department == null)
            return NotFound();

        if (await _lookupRepository.DepartmentNameExistsAsync(request.Name, excludeId: id, cancellationToken: cancellationToken))
            return BadRequest("A department with this name already exists");

        department.Name = request.Name.Trim();
        await _lookupRepository.UpdateDepartmentAsync(department, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupDepartments);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var department = await _lookupRepository.GetDepartmentByIdAsync(id, cancellationToken);
        if (department == null)
            return NotFound();

        var usageCount = await _lookupRepository.GetDepartmentUsageCountAsync(id, cancellationToken);
        if (usageCount > 0)
            return BadRequest($"Cannot delete department '{department.Name}' because it is assigned to {usageCount} computer(s)/user(s)");

        await _lookupRepository.DeleteDepartmentAsync(id, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupDepartments);
        return NoContent();
    }
}

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class CreateDepartmentRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
