using ComputerInventory.Api.Services;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MapController : ControllerBase
{
    private readonly IGeoLocationService _geoLocationService;
    private readonly IComputerRepository _computerRepository;
    private readonly ICacheService _cacheService;
    private readonly CacheServiceOptions _cacheOptions;
    private readonly ILogger<MapController> _logger;

    public MapController(
        IGeoLocationService geoLocationService,
        IComputerRepository computerRepository,
        ICacheService cacheService,
        CacheServiceOptions cacheOptions,
        ILogger<MapController> logger)
    {
        _geoLocationService = geoLocationService;
        _computerRepository = computerRepository;
        _cacheService = cacheService;
        _cacheOptions = cacheOptions;
        _logger = logger;
    }

    /// <summary>
    /// Get all computer locations with geolocation data
    /// </summary>
    [HttpGet("locations")]
    [ProducesResponseType(typeof(IEnumerable<ComputerLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ComputerLocationDto>>> GetLocations()
    {
        try
        {
            // Cache locations to reduce load for map polling
            var locations = await _cacheService.GetOrCreateAsync(
                CacheKeys.MapLocations,
                async () => (await _geoLocationService.GetAllComputerLocationsAsync()).ToList(),
                TimeSpan.FromSeconds(_cacheOptions.MapLocationsCacheSeconds));
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting computer locations");
            return StatusCode(500, "Error retrieving computer locations");
        }
    }

    /// <summary>
    /// Get the last refresh time for geolocation data
    /// </summary>
    [HttpGet("last-refresh")]
    [ProducesResponseType(typeof(MapRefreshInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MapRefreshInfo>> GetLastRefreshTime()
    {
        try
        {
            var lastRefresh = await _geoLocationService.GetLastRefreshTimeAsync();
            var isRefreshNeeded = await _geoLocationService.IsRefreshNeededAsync();
            
            return Ok(new MapRefreshInfo
            {
                LastRefresh = lastRefresh,
                IsRefreshNeeded = isRefreshNeeded,
                RefreshIntervalHours = 6
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last refresh time");
            return StatusCode(500, "Error retrieving refresh information");
        }
    }

    /// <summary>
    /// Refresh geolocation data for all computers
    /// This is rate-limited and should only be called on demand or every 6 hours
    /// </summary>
    [HttpPost("refresh")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(GeoLocationRefreshResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GeoLocationRefreshResult>> RefreshAllLocations()
    {
        try
        {
            _logger.LogInformation("Manual geolocation refresh requested by {User}", User.Identity?.Name);
            var result = await _geoLocationService.RefreshAllLocationsAsync();
            
            // Invalidate map cache after refresh
            _cacheService.Invalidate(CacheKeys.MapLocations);
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing geolocation data");
            return StatusCode(500, "Error refreshing geolocation data");
        }
    }

    /// <summary>
    /// Refresh geolocation for a specific computer
    /// </summary>
    [HttpPost("refresh/{computerId}")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(GeoLocationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GeoLocationResult>> RefreshComputerLocation(int computerId)
    {
        try
        {
            _logger.LogInformation("Geolocation refresh requested for computer {ComputerId} by {User}", 
                computerId, User.Identity?.Name);
            var result = await _geoLocationService.RefreshComputerLocationAsync(computerId);
            
            if (!result.Success)
            {
                return BadRequest(result);
            }
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing geolocation for computer {ComputerId}", computerId);
            return StatusCode(500, "Error refreshing geolocation data");
        }
    }

    /// <summary>
    /// Set the preferred location source for a computer (GPS, IP, or Auto)
    /// </summary>
    [HttpPut("location-source/{computerId}")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> SetPreferredLocationSource(int computerId, [FromBody] SetLocationSourceRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LocationSource))
            {
                return BadRequest("Location source is required");
            }

            var validSources = new[] { "GPS", "IP", "Auto" };
            if (!validSources.Contains(request.LocationSource, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest("Location source must be 'GPS', 'IP', or 'Auto'");
            }

            var updated = await _computerRepository.SetLocationSourcePreferenceAsync(computerId, request.LocationSource);
            if (!updated)
            {
                return NotFound($"Computer with ID {computerId} not found");
            }

            _logger.LogInformation(
                "Location source preference set to '{Source}' for computer {ComputerId} by {User}",
                request.LocationSource, computerId, User.Identity?.Name);

            return Ok(new { 
                message = $"Location source set to {request.LocationSource}",
                computerId,
                preferredLocationSource = request.LocationSource
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting location source for computer {ComputerId}", computerId);
            return StatusCode(500, "Error setting location source");
        }
    }
}

/// <summary>
/// Information about the map refresh status
/// </summary>
public class MapRefreshInfo
{
    public DateTime? LastRefresh { get; set; }
    public bool IsRefreshNeeded { get; set; }
    public int RefreshIntervalHours { get; set; }
}
/// <summary>
/// Request to set the preferred location source for a computer
/// </summary>
public class SetLocationSourceRequest
{
    /// <summary>
    /// The preferred location source: "GPS", "IP", or "Auto"
    /// </summary>
    [Required, RegularExpression("^(GPS|IP|Auto)$", ErrorMessage = "LocationSource must be GPS, IP, or Auto.")]
    public string LocationSource { get; set; } = "Auto";
}