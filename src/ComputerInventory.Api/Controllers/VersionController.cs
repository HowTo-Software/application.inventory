using ComputerInventory.Api.Services;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VersionController : ControllerBase
{
    private readonly IVersionHistoryRepository _versionHistoryRepository;
    private readonly ILogger<VersionController> _logger;
    private readonly IConfiguration _configuration;
    private readonly IVersionInfoCacheService _versionInfoCache;

    public VersionController(
        IVersionHistoryRepository versionHistoryRepository,
        ILogger<VersionController> logger,
        IConfiguration configuration,
        IVersionInfoCacheService versionInfoCache)
    {
        _versionHistoryRepository = versionHistoryRepository;
        _logger = logger;
        _configuration = configuration;
        _versionInfoCache = versionInfoCache;
    }

    /// <summary>
    /// Get current server version info
    /// </summary>
    [HttpGet("server/current")]
    [Authorize]
    [ProducesResponseType(typeof(VersionInfoDto), StatusCodes.Status200OK)]
    public ActionResult<VersionInfoDto> GetCurrentServerVersion()
    {
        var serverInfo = _versionInfoCache.GetServerVersionInfo();

        return Ok(new VersionInfoDto
        {
            Version = serverInfo.Version,
            Type = "Server",
            ReleasedAt = serverInfo.BuildDate,
            IsCurrentVersion = true
        });
    }

    /// <summary>
    /// Get current thin client version info
    /// </summary>
    [HttpGet("thinclient/current")]
    [Authorize]
    [ProducesResponseType(typeof(VersionInfoDto), StatusCodes.Status200OK)]
    public ActionResult<VersionInfoDto> GetCurrentThinClientVersion()
    {
        var info = _versionInfoCache.GetThinClientVersionInfo();

        return Ok(new VersionInfoDto
        {
            Version = info.Version,
            Type = "ThinClient",
            ReleasedAt = info.ReleasedAt,
            IsCurrentVersion = true,
            Checksum = info.Checksum,
            FileSize = info.FileSize
        });
    }

    /// <summary>
    /// Get all version history (server and thin client)
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    [ProducesResponseType(typeof(VersionHistoryResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VersionHistoryResponseDto>> GetVersionHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (serverItems, serverTotal, thinClientItems, thinClientTotal) = await _versionHistoryRepository.GetAllVersionHistoryPagedAsync(page, pageSize);

        // Get current versions
        var currentServerVersion = _versionInfoCache.GetServerVersionInfo().Version;
        var currentThinClientVersion = _versionInfoCache.GetThinClientVersionInfo().Version;

        return Ok(new VersionHistoryResponseDto
        {
            CurrentServerVersion = currentServerVersion,
            CurrentThinClientVersion = currentThinClientVersion,
            ServerHistory = serverItems.Select(MapToDto).ToList(),
            ThinClientHistory = thinClientItems.Select(MapToDto).ToList(),
            ServerTotalCount = serverTotal,
            ThinClientTotalCount = thinClientTotal,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get version history for server only
    /// </summary>
    [HttpGet("history/server")]
    [Authorize]
    [ProducesResponseType(typeof(Contracts.PagedResult<VersionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<VersionHistoryDto>>> GetServerHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _versionHistoryRepository.GetByTypePagedAsync(VersionType.Server, page, pageSize);

        return Ok(new Contracts.PagedResult<VersionHistoryDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get version history for thin client only
    /// </summary>
    [HttpGet("history/thinclient")]
    [Authorize]
    [ProducesResponseType(typeof(Contracts.PagedResult<VersionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<VersionHistoryDto>>> GetThinClientHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _versionHistoryRepository.GetByTypePagedAsync(VersionType.ThinClient, page, pageSize);

        return Ok(new Contracts.PagedResult<VersionHistoryDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Add a new version history entry
    /// </summary>
    [HttpPost("history")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(VersionHistoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VersionHistoryDto>> AddVersionHistory([FromBody] CreateVersionHistoryRequest request)
    {
        // Check if version already exists for this type
        var exists = await _versionHistoryRepository.ExistsAsync(request.Type, request.Version);

        if (exists)
        {
            return BadRequest(new { message = $"Version {request.Version} already exists for {request.Type}" });
        }

        // If marking as current, unmark others
        if (request.IsCurrentVersion)
        {
            var currentVersions = await _versionHistoryRepository.GetCurrentVersionsByTypeAsync(request.Type);
            
            foreach (var v in currentVersions)
            {
                v.IsCurrentVersion = false;
            }
        }

        var versionHistory = new VersionHistory
        {
            Version = request.Version,
            TypeId = (int)request.Type,
            ChangeLog = request.ChangeLog,
            ReleasedAt = request.ReleasedAt ?? DateTime.UtcNow,
            ReleasedBy = User.Identity?.Name ?? "System",
            IsCurrentVersion = request.IsCurrentVersion,
            Checksum = request.Checksum,
            FileSize = request.FileSize
        };

        await _versionHistoryRepository.AddAsync(versionHistory);

        _logger.LogInformation("Added version history: {Type} {Version} by {User}", 
            request.Type, request.Version, User.Identity?.Name);

        return CreatedAtAction(nameof(GetVersionHistory), MapToDto(versionHistory));
    }

    /// <summary>
    /// Update an existing version history entry
    /// </summary>
    [HttpPut("history/{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateVersionHistory(int id, [FromBody] UpdateVersionHistoryRequest request)
    {
        var versionHistory = await _versionHistoryRepository.GetByIdAsync(id);
        if (versionHistory == null)
        {
            return NotFound();
        }

        versionHistory.ChangeLog = request.ChangeLog;
        
        if (request.IsCurrentVersion && !versionHistory.IsCurrentVersion)
        {
            // Unmark other current versions of the same type
            var currentVersions = await _versionHistoryRepository.GetCurrentVersionsByTypeAsync((VersionType)versionHistory.TypeId);
            
            foreach (var v in currentVersions.Where(v => v.Id != id))
            {
                v.IsCurrentVersion = false;
            }
            versionHistory.IsCurrentVersion = true;
        }

        await _versionHistoryRepository.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Delete a version history entry
    /// </summary>
    [HttpDelete("history/{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteVersionHistory(int id)
    {
        var versionHistory = await _versionHistoryRepository.GetByIdAsync(id);
        if (versionHistory == null)
        {
            return NotFound();
        }

        await _versionHistoryRepository.DeleteAsync(versionHistory);

        _logger.LogInformation("Deleted version history: {Type} {Version}", 
            versionHistory.TypeId, versionHistory.Version);

        return NoContent();
    }

    /// <summary>
    /// Auto-record current version (called during deployment)
    /// </summary>
    [HttpPost("record")]
    [AllowAnonymous]  // Called by deployment scripts with API key
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RecordCurrentVersion([FromBody] RecordVersionRequest request)
    {
        // Validate API key for non-authenticated requests
        var apiKey = Request.Headers["X-API-Key"].FirstOrDefault();
        var configuredKey = _configuration["AgentSettings:ApiKey"];
        
        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogWarning("AgentSettings:ApiKey is not configured — rejecting version record request.");
            return Unauthorized();
        }
        
        if (!string.Equals(apiKey, configuredKey, StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        var type = request.Type.ToLowerInvariant() == "server" ? VersionType.Server : VersionType.ThinClient;

        // Check if version already exists
        var existing = await _versionHistoryRepository.FindByTypeAndVersionAsync(type, request.Version);

        if (existing != null)
        {
            // Update existing entry
            existing.ChangeLog = request.ChangeLog ?? existing.ChangeLog;
            existing.ReleasedAt = DateTime.UtcNow;
            existing.Checksum = request.Checksum ?? existing.Checksum;
            existing.FileSize = request.FileSize ?? existing.FileSize;
            await _versionHistoryRepository.SaveChangesAsync();
        }
        else
        {
            // Unmark previous current versions
            var currentVersions = await _versionHistoryRepository.GetCurrentVersionsByTypeAsync(type);
            
            foreach (var v in currentVersions)
            {
                v.IsCurrentVersion = false;
            }

            // Create new entry
            var versionHistory = new VersionHistory
            {
                Version = request.Version,
                TypeId = (int)type,
                ChangeLog = request.ChangeLog ?? $"Auto-deployed version {request.Version}",
                ReleasedAt = DateTime.UtcNow,
                ReleasedBy = "Deployment",
                IsCurrentVersion = true,
                Checksum = request.Checksum,
                FileSize = request.FileSize
            };

            await _versionHistoryRepository.AddAsync(versionHistory);
        }

        _logger.LogInformation("Recorded version: {Type} {Version}", type, request.Version);

        return Ok(new { message = $"Version {request.Version} recorded successfully" });
    }

    private static VersionHistoryDto MapToDto(VersionHistory vh) => new()
    {
        Id = vh.Id,
        Version = vh.Version,
        Type = vh.TypeId.ToString(),
        ChangeLog = vh.ChangeLog,
        ReleasedAt = vh.ReleasedAt,
        ReleasedBy = vh.ReleasedBy,
        IsCurrentVersion = vh.IsCurrentVersion,
        Checksum = vh.Checksum,
        FileSize = vh.FileSize
    };
}

public class VersionInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class VersionHistoryDto
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ChangeLog { get; set; } = string.Empty;
    public DateTime ReleasedAt { get; set; }
    public string? ReleasedBy { get; set; }
    public bool IsCurrentVersion { get; set; }
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class VersionHistoryResponseDto
{
    public string CurrentServerVersion { get; set; } = string.Empty;
    public string CurrentThinClientVersion { get; set; } = string.Empty;
    public List<VersionHistoryDto> ServerHistory { get; set; } = new();
    public List<VersionHistoryDto> ThinClientHistory { get; set; } = new();
    public int ServerTotalCount { get; set; }
    public int ThinClientTotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int ServerTotalPages => PageSize > 0 ? (int)Math.Ceiling((double)ServerTotalCount / PageSize) : 0;
    public int ThinClientTotalPages => PageSize > 0 ? (int)Math.Ceiling((double)ThinClientTotalCount / PageSize) : 0;
}

public class CreateVersionHistoryRequest
{
    [Required, StringLength(50)]
    public string Version { get; set; } = string.Empty;
    public VersionType Type { get; set; }
    [Required, StringLength(5000)]
    public string ChangeLog { get; set; } = string.Empty;
    public DateTime? ReleasedAt { get; set; }
    public bool IsCurrentVersion { get; set; }
    [StringLength(128)]
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}

public class UpdateVersionHistoryRequest
{
    [Required, StringLength(5000)]
    public string ChangeLog { get; set; } = string.Empty;
    public bool IsCurrentVersion { get; set; }
}

public class RecordVersionRequest
{
    [Required, StringLength(50)]
    public string Type { get; set; } = string.Empty;  // "Server" or "ThinClient"
    [Required, StringLength(50)]
    public string Version { get; set; } = string.Empty;
    [StringLength(5000)]
    public string? ChangeLog { get; set; }
    [StringLength(128)]
    public string? Checksum { get; set; }
    public long? FileSize { get; set; }
}
