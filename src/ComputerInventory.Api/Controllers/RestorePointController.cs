using ComputerInventory.Core.Constants;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Api.Authorization;
using ComputerInventory.Api.Services;
using ComputerInventory.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

/// <summary>
/// Controller for managing system restore points - complete snapshots saved on reboot
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RestorePointController : ControllerBase
{
    private readonly IRestorePointRepository _restorePointRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly IComputerHistoryRepository _historyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RestorePointController> _logger;
    private readonly IAuthorizationService _authorizationService;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;

    private static readonly SameDepartmentRequirement _sameDepartmentRequirement = new();

    public RestorePointController(
        IRestorePointRepository restorePointRepository,
        IComputerRepository computerRepository,
        IComputerHistoryRepository historyRepository,
        IConfiguration configuration,
        ILogger<RestorePointController> logger,
        IAuthorizationService authorizationService,
        IBackgroundTaskQueue backgroundTaskQueue)
    {
        _restorePointRepository = restorePointRepository;
        _computerRepository = computerRepository;
        _historyRepository = historyRepository;
        _configuration = configuration;
        _logger = logger;
        _authorizationService = authorizationService;
        _backgroundTaskQueue = backgroundTaskQueue;
    }

    private const long MaxTotalJsonBytes = 15 * 1024 * 1024; // 15 MB combined JSON limit

    /// <summary>
    /// Create a new restore point (called by ThinClient on every reboot)
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(20_971_520)] // 20 MB hard cap for restore point requests
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateRestorePoint([FromBody] RestorePointDto dto)
    {
        if (!ValidateApiKey())
        {
            _logger.LogWarning("Unauthorized restore point attempt for {SerialNumber} — invalid or missing API key", dto.SerialNumber);
            return Unauthorized(new { error = "Invalid or missing API key" });
        }

        // Validate total combined JSON blob size
        var totalJsonSize = CalculateDtoJsonSize(dto);
        if (totalJsonSize > MaxTotalJsonBytes)
        {
            _logger.LogWarning("Restore point for {SerialNumber} rejected — total JSON size {Size} bytes exceeds {Max} byte limit",
                dto.SerialNumber, totalJsonSize, MaxTotalJsonBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                error = "Request payload too large",
                totalBytes = totalJsonSize,
                maxBytes = MaxTotalJsonBytes
            });
        }

        try
        {
            _logger.LogInformation("Creating restore point for {SerialNumber} - Type: {Type}", 
                dto.SerialNumber, dto.Type);

            // Find the computer by serial number
            var computer = await _computerRepository.GetBySerialNumberAsync(dto.SerialNumber);

            if (computer == null)
            {
                _logger.LogWarning("Computer not found for restore point: {SerialNumber}", dto.SerialNumber);
                return NotFound(new { error = "Computer not found. Register first." });
            }

            // Create the restore point
            var restorePoint = new RestorePoint
            {
                ComputerId = computer.Id,
                RestorePointId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                TypeId = (int)dto.Type,
                Description = dto.Description ?? $"Auto restore point - {dto.Type}",
                
                // System info sub-entity
                SystemInfo = new RestorePointSystemInfo
                {
                    SystemBootTime = dto.SystemBootTime,
                    UptimeSeconds = dto.UptimeSeconds,
                    Hostname = dto.Hostname ?? computer.Hostname,
                    SerialNumber = dto.SerialNumber,
                    IpAddress = dto.IpAddress ?? computer.IpAddress,
                    MacAddress = dto.MacAddress ?? computer.MacAddress,
                    Manufacturer = dto.Manufacturer ?? (computer.ComputerModel?.Manufacturer?.Name),
                    Model = dto.Model ?? (computer.ComputerModel?.ModelName),
                    CurrentUser = dto.CurrentUser,
                },
                
                // Complete system state JSON blobs sub-entity
                Snapshot = new RestorePointSnapshot
                {
                    SystemDetailsJson = dto.SystemDetailsJson,
                    InstalledSoftwareJson = dto.InstalledSoftwareJson,
                    RunningProcessesJson = dto.RunningProcessesJson,
                    WindowsServicesJson = dto.WindowsServicesJson,
                    StartupProgramsJson = dto.StartupProgramsJson,
                    ScheduledTasksJson = dto.ScheduledTasksJson,
                    EnvironmentVariablesJson = dto.EnvironmentVariablesJson,
                    NetworkConfigJson = dto.NetworkConfigJson,
                    StorageConfigJson = dto.StorageConfigJson,
                    EventLogSummaryJson = dto.EventLogSummaryJson,
                    WindowsUpdatesJson = dto.WindowsUpdatesJson,
                    DeviceDriversJson = dto.DeviceDriversJson,
                    FirewallRulesJson = dto.FirewallRulesJson,
                    LocalUsersJson = dto.LocalUsersJson,
                    LocalGroupsJson = dto.LocalGroupsJson,
                    SharedFoldersJson = dto.SharedFoldersJson,
                    PrintersJson = dto.PrintersJson,
                    UsbDevicesJson = dto.UsbDevicesJson,
                    BatteryStatusJson = dto.BatteryStatusJson,
                    BitLockerStatusJson = dto.BitLockerStatusJson,
                    SecurityStatusJson = dto.SecurityStatusJson,
                },
                
                IsComplete = true
            };

            // Calculate data size
            restorePoint.DataSizeBytes = CalculateDataSize(restorePoint);
            
            // Generate hash for integrity
            restorePoint.DataHash = GenerateDataHash(restorePoint);

            await _restorePointRepository.AddAsync(restorePoint);

            _logger.LogInformation("Created restore point {RestorePointId} for computer {ComputerId} ({Hostname})", 
                restorePoint.RestorePointId, computer.Id, computer.Hostname);

            await _historyRepository.AddAsync(new ComputerHistory
            {
                ComputerId = computer.Id,
                Action = HistoryActions.RestorePointCreated,
                ChangedBy = ActorNames.Agent,
                ChangedAt = DateTime.UtcNow,
                NewValue = restorePoint.RestorePointId.ToString(),
                Notes = $"Type: {dto.Type}, Size: {restorePoint.DataSizeBytes} bytes"
            });

            // Clean up old restore points in background
            var computerId = computer.Id;
            var keepCount = _configuration.GetValue("DatabaseMaintenance:MaxRestorePointsPerComputer", 50);
            await _backgroundTaskQueue.QueueBackgroundWorkItemAsync(async ct =>
            {
                await _restorePointRepository.CleanupOldRestorePointsAsync(computerId, keepCount);
            }, $"CleanupOldRestorePoints-Computer-{computerId}");

            return Ok(new 
            { 
                restorePointId = restorePoint.RestorePointId,
                createdAt = restorePoint.CreatedAt,
                dataSizeBytes = restorePoint.DataSizeBytes,
                message = "Restore point created successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restore point for {SerialNumber}", dto.SerialNumber);
            return StatusCode(500, new { error = "Failed to create restore point", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all restore points for a computer
    /// </summary>
    [Authorize]
    [HttpGet("computer/{serialNumber}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRestorePoints(string serialNumber)
    {
        var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);

        if (computer == null)
            return NotFound(new { error = "Computer not found" });

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        var restorePoints = (await _restorePointRepository.GetByComputerIdAsync(computer.Id))
            .Select(r => new
            {
                r.RestorePointId,
                r.CreatedAt,
                Type = r.TypeId,
                r.Description,
                SystemBootTime = r.SystemInfo?.SystemBootTime,
                UptimeSeconds = r.SystemInfo?.UptimeSeconds,
                Hostname = r.SystemInfo?.Hostname,
                IpAddress = r.SystemInfo?.IpAddress,
                CurrentUser = r.SystemInfo?.CurrentUser,
                r.DataSizeBytes,
                r.IsComplete
            });

        return Ok(restorePoints);
    }

    /// <summary>
    /// Get a specific restore point with all data
    /// </summary>
    [Authorize]
    [HttpGet("{restorePointId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRestorePoint(Guid restorePointId)
    {
        var restorePoint = await _restorePointRepository.GetByGuidWithDetailsAsync(restorePointId);

        if (restorePoint == null)
            return NotFound(new { error = "Restore point not found" });

        var computer = await _computerRepository.GetByIdAsync(restorePoint.ComputerId);
        if (computer != null)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
            if (!authResult.Succeeded)
                return Forbid();
        }

        await _historyRepository.AddAsync(new ComputerHistory
        {
            ComputerId = restorePoint.ComputerId,
            Action = HistoryActions.RestorePointAccessed,
            ChangedBy = User.Identity?.Name ?? "unknown",
            ChangedAt = DateTime.UtcNow,
            NewValue = restorePointId.ToString(),
            Notes = $"Accessed full restore point data"
        });

        return Ok(restorePoint);
    }

    /// <summary>
    /// Get the latest restore point for a computer
    /// </summary>
    [Authorize]
    [HttpGet("computer/{serialNumber}/latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestRestorePoint(string serialNumber)
    {
        var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);

        if (computer == null)
            return NotFound(new { error = "Computer not found" });

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        var restorePoint = await _restorePointRepository.GetLatestCompleteByComputerIdAsync(computer.Id);

        if (restorePoint == null)
            return NotFound(new { error = "No restore points found" });

        return Ok(restorePoint);
    }

    /// <summary>
    /// Get restore point summary/statistics for all computers
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRestorePointSummary([FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var (summary, totalCount) = await _restorePointRepository.GetSummaryAsync(page, pageSize);

        var items = summary.Select(s => new
        {
            s.ComputerId,
            s.Hostname,
            s.SerialNumber,
            s.TotalRestorePoints,
            s.LatestRestorePoint,
            s.OldestRestorePoint,
            s.TotalSizeBytes,
            TotalSizeMB = (s.TotalSizeBytes / 1024.0 / 1024.0).ToString("F2"),
            s.RebootCount
        });

        return Ok(new
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 0
        });
    }

    /// <summary>
    /// Delete a specific restore point
    /// </summary>
    [Authorize]
    [HttpDelete("{restorePointId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRestorePoint(Guid restorePointId)
    {
        var restorePoint = await _restorePointRepository.GetByGuidAsync(restorePointId);

        if (restorePoint == null)
            return NotFound(new { error = "Restore point not found" });

        var computer = await _computerRepository.GetByIdAsync(restorePoint.ComputerId);
        if (computer != null)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
            if (!authResult.Succeeded)
                return Forbid();
        }

        await _restorePointRepository.DeleteAsync(restorePoint);

        await _historyRepository.AddAsync(new ComputerHistory
        {
            ComputerId = restorePoint.ComputerId,
            Action = HistoryActions.RestorePointDeleted,
            ChangedBy = User.Identity?.Name ?? "unknown",
            ChangedAt = DateTime.UtcNow,
            OldValue = restorePointId.ToString(),
            Notes = $"Deleted restore point created at {restorePoint.CreatedAt:O}"
        });

        return Ok(new { message = "Restore point deleted" });
    }

    /// <summary>
    /// Delete all restore points for a computer except the latest N
    /// </summary>
    [Authorize]
    [HttpDelete("computer/{serialNumber}/cleanup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CleanupRestorePoints(string serialNumber, [FromQuery] int keepCount = 10)
    {
        var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);

        if (computer == null)
            return NotFound(new { error = "Computer not found" });

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        var deletedCount = await _restorePointRepository.CleanupOldRestorePointsAsync(computer.Id, keepCount);

        if (deletedCount > 0)
        {
            await _historyRepository.AddAsync(new ComputerHistory
            {
                ComputerId = computer.Id,
                Action = HistoryActions.RestorePointCleanup,
                ChangedBy = User.Identity?.Name ?? "unknown",
                ChangedAt = DateTime.UtcNow,
                Notes = $"Cleaned up {deletedCount} old restore points, kept latest {keepCount}"
            });
        }

        return Ok(new { message = $"Deleted {deletedCount} old restore points", keepCount });
    }

    private long CalculateDataSize(RestorePoint rp)
    {
        long size = 0;
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.SystemDetailsJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.InstalledSoftwareJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.RunningProcessesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.WindowsServicesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.StartupProgramsJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.ScheduledTasksJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.EnvironmentVariablesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.NetworkConfigJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.StorageConfigJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.EventLogSummaryJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.WindowsUpdatesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.DeviceDriversJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.FirewallRulesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.LocalUsersJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.LocalGroupsJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.SharedFoldersJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.PrintersJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.UsbDevicesJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.BatteryStatusJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.BitLockerStatusJson);
        size += GZipCompressedStringConverter.GetCompressedSize(rp.Snapshot?.SecurityStatusJson);
        return size;
    }

    private static long CalculateDtoJsonSize(RestorePointDto dto)
    {
        long size = 0;
        size += dto.SystemDetailsJson?.Length ?? 0;
        size += dto.InstalledSoftwareJson?.Length ?? 0;
        size += dto.RunningProcessesJson?.Length ?? 0;
        size += dto.WindowsServicesJson?.Length ?? 0;
        size += dto.StartupProgramsJson?.Length ?? 0;
        size += dto.ScheduledTasksJson?.Length ?? 0;
        size += dto.EnvironmentVariablesJson?.Length ?? 0;
        size += dto.NetworkConfigJson?.Length ?? 0;
        size += dto.StorageConfigJson?.Length ?? 0;
        size += dto.EventLogSummaryJson?.Length ?? 0;
        size += dto.WindowsUpdatesJson?.Length ?? 0;
        size += dto.DeviceDriversJson?.Length ?? 0;
        size += dto.FirewallRulesJson?.Length ?? 0;
        size += dto.LocalUsersJson?.Length ?? 0;
        size += dto.LocalGroupsJson?.Length ?? 0;
        size += dto.SharedFoldersJson?.Length ?? 0;
        size += dto.PrintersJson?.Length ?? 0;
        size += dto.UsbDevicesJson?.Length ?? 0;
        size += dto.BatteryStatusJson?.Length ?? 0;
        size += dto.BitLockerStatusJson?.Length ?? 0;
        size += dto.SecurityStatusJson?.Length ?? 0;
        return size;
    }

    private bool ValidateApiKey()
    {
        var configuredKey = _configuration["AgentSettings:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogWarning("AgentSettings:ApiKey is not configured — rejecting restore point request");
            return false;
        }

        var providedKey = Request.Headers["X-API-Key"].FirstOrDefault();
        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
    }

    private string GenerateDataHash(RestorePoint rp)
    {
        var si = rp.SystemInfo;
        var sn = rp.Snapshot;

        var combined = string.Join("|",
            rp.ComputerId.ToString(),
            rp.CreatedAt.ToString("O"),
            rp.TypeId.ToString(),
            rp.Description ?? "",
            // SystemInfo fields
            si?.SystemBootTime?.ToString("O") ?? "",
            si?.UptimeSeconds?.ToString() ?? "",
            si?.Hostname ?? "",
            si?.SerialNumber ?? "",
            si?.IpAddress ?? "",
            si?.MacAddress ?? "",
            si?.Manufacturer ?? "",
            si?.Model ?? "",
            si?.CurrentUser ?? "",
            // Snapshot JSON fields
            sn?.SystemDetailsJson ?? "",
            sn?.InstalledSoftwareJson ?? "",
            sn?.RunningProcessesJson ?? "",
            sn?.WindowsServicesJson ?? "",
            sn?.StartupProgramsJson ?? "",
            sn?.ScheduledTasksJson ?? "",
            sn?.EnvironmentVariablesJson ?? "",
            sn?.NetworkConfigJson ?? "",
            sn?.StorageConfigJson ?? "",
            sn?.EventLogSummaryJson ?? "",
            sn?.WindowsUpdatesJson ?? "",
            sn?.DeviceDriversJson ?? "",
            sn?.FirewallRulesJson ?? "",
            sn?.LocalUsersJson ?? "",
            sn?.LocalGroupsJson ?? "",
            sn?.SharedFoldersJson ?? "",
            sn?.PrintersJson ?? "",
            sn?.UsbDevicesJson ?? "",
            sn?.BatteryStatusJson ?? "",
            sn?.BitLockerStatusJson ?? "",
            sn?.SecurityStatusJson ?? "");

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}

/// <summary>
/// DTO for creating restore points
/// </summary>
public class RestorePointDto
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    public RestorePointType Type { get; set; }
    [StringLength(500)]
    public string? Description { get; set; }
    public DateTime? SystemBootTime { get; set; }
    public long? UptimeSeconds { get; set; }
    
    // Basic info
    [StringLength(100)]
    public string? Hostname { get; set; }
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [StringLength(200)]
    public string? Manufacturer { get; set; }
    [StringLength(200)]
    public string? Model { get; set; }
    [StringLength(200)]
    public string? CurrentUser { get; set; }
    
    // JSON data blobs — capped at 5MB each to prevent abuse
    [StringLength(5_242_880)]
    public string? SystemDetailsJson { get; set; }
    [StringLength(5_242_880)]
    public string? InstalledSoftwareJson { get; set; }
    [StringLength(5_242_880)]
    public string? RunningProcessesJson { get; set; }
    [StringLength(5_242_880)]
    public string? WindowsServicesJson { get; set; }
    [StringLength(5_242_880)]
    public string? StartupProgramsJson { get; set; }
    [StringLength(5_242_880)]
    public string? ScheduledTasksJson { get; set; }
    [StringLength(5_242_880)]
    public string? EnvironmentVariablesJson { get; set; }
    [StringLength(5_242_880)]
    public string? NetworkConfigJson { get; set; }
    [StringLength(5_242_880)]
    public string? StorageConfigJson { get; set; }
    [StringLength(5_242_880)]
    public string? EventLogSummaryJson { get; set; }
    [StringLength(5_242_880)]
    public string? WindowsUpdatesJson { get; set; }
    [StringLength(5_242_880)]
    public string? DeviceDriversJson { get; set; }
    [StringLength(5_242_880)]
    public string? FirewallRulesJson { get; set; }
    [StringLength(5_242_880)]
    public string? LocalUsersJson { get; set; }
    [StringLength(5_242_880)]
    public string? LocalGroupsJson { get; set; }
    [StringLength(5_242_880)]
    public string? SharedFoldersJson { get; set; }
    [StringLength(5_242_880)]
    public string? PrintersJson { get; set; }
    [StringLength(5_242_880)]
    public string? UsbDevicesJson { get; set; }
    [StringLength(5_242_880)]
    public string? BatteryStatusJson { get; set; }
    [StringLength(5_242_880)]
    public string? BitLockerStatusJson { get; set; }
    [StringLength(5_242_880)]
    public string? SecurityStatusJson { get; set; }
}
