using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupRepository _lookupRepository;
    private readonly ICacheService _cache;
    private readonly TimeSpan _lookupExpiration;

    public LookupsController(ILookupRepository lookupRepository, ICacheService cache, CacheServiceOptions cacheOptions)
    {
        _lookupRepository = lookupRepository;
        _cache = cache;
        _lookupExpiration = TimeSpan.FromSeconds(cacheOptions.LookupCacheSeconds);
    }

    [HttpGet("computer-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetComputerTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupComputerTypes, () => _lookupRepository.GetComputerTypesAsync(), page, pageSize);

    [HttpGet("computer-statuses")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetComputerStatuses([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupComputerStatuses, () => _lookupRepository.GetComputerStatusesAsync(), page, pageSize);

    [HttpGet("maintenance-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMaintenanceTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupMaintenanceTypes, () => _lookupRepository.GetMaintenanceTypesAsync(), page, pageSize);

    [HttpGet("maintenance-statuses")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMaintenanceStatuses([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupMaintenanceStatuses, () => _lookupRepository.GetMaintenanceStatusesAsync(), page, pageSize);

    [HttpGet("user-roles")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetUserRoles([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupUserRoles, () => _lookupRepository.GetUserRolesAsync(), page, pageSize);

    [HttpGet("version-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetVersionTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupVersionTypes, () => _lookupRepository.GetVersionTypesAsync(), page, pageSize);

    [HttpGet("departments")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetDepartments([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupDepartments, () => _lookupRepository.GetDepartmentsAsync(), page, pageSize);

    [HttpGet("locations")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetLocations([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupLocations, () => _lookupRepository.GetLocationsAsync(), page, pageSize);

    [HttpGet("manufacturers")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetManufacturers([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupManufacturers, () => _lookupRepository.GetManufacturersAsync(), page, pageSize);

    [HttpGet("models")]
    [ProducesResponseType(typeof(PagedResult<ModelLookupItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModels([FromQuery] int? manufacturerId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 100)
    {
        var items = await _cache.GetOrCreateAsync(CacheKeys.LookupModels(manufacturerId), async () =>
            (await _lookupRepository.GetModelsAsync(manufacturerId))
                .Select(m => new ModelLookupItem
                {
                    Id = m.Id,
                    Name = m.Name,
                    ManufacturerId = m.ManufacturerId,
                    ManufacturerName = m.ManufacturerName
                }).ToList(),
            _lookupExpiration);
        return Ok(Paginate(items!, page, pageSize));
    }

    [HttpGet("location-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetLocationTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupLocationTypes, () => _lookupRepository.GetLocationTypesAsync(), page, pageSize);

    [HttpGet("department-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetDepartmentTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupDepartmentTypes, () => _lookupRepository.GetDepartmentTypesAsync(), page, pageSize);

    [HttpGet("manufacturer-types")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetManufacturerTypes([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupManufacturerTypes, () => _lookupRepository.GetManufacturerTypesAsync(), page, pageSize);

    [HttpGet("operating-systems")]
    [ProducesResponseType(typeof(PagedResult<LookupItem>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetOperatingSystems([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        => GetLookupItems(CacheKeys.LookupOperatingSystems, () => _lookupRepository.GetOperatingSystemsAsync(), page, pageSize);

    private async Task<IActionResult> GetLookupItems<TEntity>(
        string cacheKey, Func<Task<List<TEntity>>> fetchAsync, int page, int pageSize)
        where TEntity : ILookupEntity
    {
        var items = await _cache.GetOrCreateAsync(cacheKey, async () =>
            (await fetchAsync())
                .Select(e => new LookupItem { Id = e.Id, Name = e.Name }).ToList(),
            _lookupExpiration);
        return Ok(Paginate(items!, page, pageSize));
    }

    private static PagedResult<T> Paginate<T>(List<T> items, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        return new PagedResult<T>
        {
            Items = items.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = items.Count,
            Page = page,
            PageSize = pageSize
        };
    }
}

public class LookupItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class ModelLookupItem : LookupItem
{
    public int ManufacturerId { get; set; }
    public string ManufacturerName { get; set; } = string.Empty;
}
