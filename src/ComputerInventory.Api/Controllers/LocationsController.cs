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
public class LocationsController : ControllerBase
{
    private readonly ILookupRepository _lookupRepository;
    private readonly ICacheService _cache;

    public LocationsController(ILookupRepository lookupRepository, ICacheService cache)
    {
        _lookupRepository = lookupRepository;
        _cache = cache;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<LocationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LocationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var locations = await _lookupRepository.GetLocationsAsync(cancellationToken);
        var dtos = new List<LocationDto>();
        foreach (var l in locations)
        {
            var usageCount = await _lookupRepository.GetLocationUsageCountAsync(l.Id, cancellationToken);
            dtos.Add(new LocationDto { Id = l.Id, Name = l.Name, UsageCount = usageCount });
        }
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var location = await _lookupRepository.GetLocationByIdAsync(id, cancellationToken);
        if (location == null)
            return NotFound();

        var usageCount = await _lookupRepository.GetLocationUsageCountAsync(id, cancellationToken);
        return Ok(new LocationDto { Id = location.Id, Name = location.Name, UsageCount = usageCount });
    }

    [HttpPost]
    [ProducesResponseType(typeof(LocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationDto>> Create([FromBody] CreateLocationRequest request, CancellationToken cancellationToken)
    {
        if (await _lookupRepository.LocationNameExistsAsync(request.Name, cancellationToken: cancellationToken))
            return BadRequest("A location with this name already exists");

        var location = new Location { Name = request.Name.Trim() };
        var created = await _lookupRepository.AddLocationAsync(location, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupLocations);
        var dto = new LocationDto { Id = created.Id, Name = created.Name, UsageCount = 0 };
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] CreateLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await _lookupRepository.GetLocationByIdAsync(id, cancellationToken);
        if (location == null)
            return NotFound();

        if (await _lookupRepository.LocationNameExistsAsync(request.Name, excludeId: id, cancellationToken: cancellationToken))
            return BadRequest("A location with this name already exists");

        location.Name = request.Name.Trim();
        await _lookupRepository.UpdateLocationAsync(location, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupLocations);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var location = await _lookupRepository.GetLocationByIdAsync(id, cancellationToken);
        if (location == null)
            return NotFound();

        var usageCount = await _lookupRepository.GetLocationUsageCountAsync(id, cancellationToken);
        if (usageCount > 0)
            return BadRequest($"Cannot delete location '{location.Name}' because it is assigned to {usageCount} computer(s)");

        await _lookupRepository.DeleteLocationAsync(id, cancellationToken);
        _cache.Invalidate(CacheKeys.LookupLocations);
        return NoContent();
    }
}

public class LocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class CreateLocationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
}
