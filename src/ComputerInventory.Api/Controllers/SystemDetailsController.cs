using ComputerInventory.Api.Mapping;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemDetailsController : ControllerBase
{
    private readonly ISystemDetailsRepository _systemDetailsRepository;
    private readonly IComputerRepository _computerRepository;
    private readonly ILogger<SystemDetailsController> _logger;
    private readonly IConfiguration _configuration;

    public SystemDetailsController(
        ISystemDetailsRepository systemDetailsRepository,
        IComputerRepository computerRepository,
        ILogger<SystemDetailsController> logger,
        IConfiguration configuration)
    {
        _systemDetailsRepository = systemDetailsRepository;
        _computerRepository = computerRepository;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Submit comprehensive system details from agent
    /// </summary>
    [HttpPost("submit")]
    [AllowAnonymous]  // Agent uses serial number for identification
    [RequestSizeLimit(10_485_760)] // 10 MB cap — system details JSON should never exceed this
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitSystemDetails([FromBody] SystemDetailsRequest request)
    {
        if (!ValidateApiKey())
        {
            return Unauthorized(new { message = "Invalid or missing API key" });
        }

        try
        {
            var computer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);

            if (computer == null)
            {
                return NotFound(new { message = "Computer not found" });
            }

            var details = request.ToEntity(computer.Id);

            await _systemDetailsRepository.AddAsync(details);

            _logger.LogInformation("System details collected for computer {SerialNumber}", request.SerialNumber);

            return Ok(new { success = true, id = details.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving system details for {SerialNumber}", request.SerialNumber);
            return StatusCode(500, new { message = "Error saving system details" });
        }
    }

    /// <summary>
    /// Get latest system details for a computer
    /// </summary>
    [HttpGet("computer/{computerId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatestForComputer(int computerId)
    {
        var d = await _systemDetailsRepository.GetLatestByComputerIdAsync(computerId);

        if (d == null)
        {
            return NotFound(new { message = "No system details found" });
        }

        return Ok(d.ToReport());
    }

    /// <summary>
    /// Get system details history for a computer
    /// </summary>
    [HttpGet("computer/{computerId}/history")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryForComputer(int computerId, int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 100);

        var details = (await _systemDetailsRepository.GetHistoryByComputerIdAsync(computerId, limit))
            .Select(d => new
            {
                d.Id,
                d.CollectedAt,
                OsName = d.Os?.OsName,
                CpuUsagePercent = d.Performance?.CpuUsagePercent,
                MemoryUsagePercent = d.Performance?.MemoryUsagePercent,
                DiskUsagePercent = d.Performance?.DiskUsagePercent,
                InstalledSoftwareCount = d.Software?.InstalledSoftwareCount,
                RunningProcessesCount = d.Software?.RunningProcessesCount,
                RunningServicesCount = d.Software?.RunningServicesCount
            });

        return Ok(details);
    }

    /// <summary>
    /// Get lightweight summaries of available reports (for dropdowns/lists)
    /// </summary>
    [HttpGet("report/summaries")]
    [Authorize]
    [ProducesResponseType(typeof(List<ReportSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportSummaries()
    {
        var latestDetails = await _systemDetailsRepository.GetLatestForAllComputersAsync();

        var summaries = latestDetails
            .Select(d => new ReportSummaryDto
            {
                Id = d.Id,
                ComputerId = d.ComputerId,
                ComputerName = d.Computer?.Hostname ?? "Unknown",
                SerialNumber = d.Computer?.SerialNumber,
                CollectedAt = d.CollectedAt
            })
            .OrderBy(s => s.ComputerName)
            .ToList();

        return Ok(summaries);
    }

    /// <summary>
    /// Get comprehensive report for all computers (paginated)
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReport(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (items, totalCount) = await _systemDetailsRepository.GetPagedSystemDetailsReportAsync(page, pageSize, cancellationToken);

        return Ok(new PagedResult<SystemDetailsReport>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private bool ValidateApiKey()
    {
        var configuredKey = _configuration["AgentSettings:ApiKey"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            _logger.LogWarning("AgentSettings:ApiKey is not configured — rejecting agent request. " +
                "Set the 'AgentSettings__ApiKey' environment variable.");
            return false;
        }
        var providedKey = Request.Headers["X-API-Key"].FirstOrDefault();
        return string.Equals(providedKey, configuredKey, StringComparison.Ordinal);
    }
}

/// <summary>
/// Request model for submitting system details from agent.
/// Properties are grouped into logical sub-requests.
/// </summary>
public class SystemDetailsRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string? MachineGuid { get; set; }
    [StringLength(200)]
    public string? ProductId { get; set; }

    public BiosRequest? Bios { get; set; }
    public MotherboardRequest? Motherboard { get; set; }
    public CpuRequest? Cpu { get; set; }
    public MemoryRequest? Memory { get; set; }
    public StorageRequest? Storage { get; set; }
    public GpuRequest? Gpu { get; set; }
    public NetworkRequest? Network { get; set; }
    public OsRequest? Os { get; set; }
    public SecurityRequest? Security { get; set; }
    public BatteryRequest? Battery { get; set; }
    public ChassisRequest? Chassis { get; set; }
    public SoftwareRequest? Software { get; set; }
    public UsersGroupsRequest? UsersGroups { get; set; }
    public PeripheralsRequest? Peripherals { get; set; }
    public EventLogsRequest? EventLogs { get; set; }
    public PerformanceRequest? Performance { get; set; }
}

public class BiosRequest
{
    [StringLength(200)]
    public string? BiosVersion { get; set; }
    [StringLength(200)]
    public string? BiosManufacturer { get; set; }
    public DateTime? BiosReleaseDate { get; set; }
    [StringLength(200)]
    public string? BiosSerialNumber { get; set; }
    [StringLength(50)]
    public string? SmbiosVersion { get; set; }
}

public class MotherboardRequest
{
    [StringLength(200)]
    public string? MotherboardManufacturer { get; set; }
    [StringLength(200)]
    public string? MotherboardProduct { get; set; }
    [StringLength(200)]
    public string? MotherboardSerialNumber { get; set; }
    [StringLength(100)]
    public string? MotherboardVersion { get; set; }
}

public class CpuRequest
{
    [StringLength(300)]
    public string? CpuName { get; set; }
    [StringLength(200)]
    public string? CpuManufacturer { get; set; }
    [Range(0, 1024)]
    public int? CpuCores { get; set; }
    [Range(0, 2048)]
    public int? CpuLogicalProcessors { get; set; }
    [Range(0, 100000)]
    public int? CpuMaxClockSpeed { get; set; }
    [Range(0, 100000)]
    public int? CpuCurrentClockSpeed { get; set; }
    [StringLength(50)]
    public string? CpuArchitecture { get; set; }
    [Range(0, 1_000_000)]
    public int? CpuL2CacheSize { get; set; }
    [Range(0, 1_000_000)]
    public int? CpuL3CacheSize { get; set; }
    [StringLength(100)]
    public string? CpuSocketDesignation { get; set; }
    [StringLength(200)]
    public string? CpuProcessorId { get; set; }
}

public class MemoryRequest
{
    public long? TotalPhysicalMemory { get; set; }
    public long? AvailablePhysicalMemory { get; set; }
    public long? TotalVirtualMemory { get; set; }
    public long? AvailableVirtualMemory { get; set; }
    [Range(0, 256)]
    public int? MemorySlots { get; set; }
    [Range(0, 256)]
    public int? MemorySlotsUsed { get; set; }
    [StringLength(1_048_576)]
    public string? MemoryModulesJson { get; set; }
}

public class StorageRequest
{
    [StringLength(1_048_576)]
    public string? DisksJson { get; set; }
    [StringLength(1_048_576)]
    public string? PartitionsJson { get; set; }
    [StringLength(1_048_576)]
    public string? LogicalDrivesJson { get; set; }
    public long? TotalDiskSpace { get; set; }
    public long? FreeDiskSpace { get; set; }
    [StringLength(1_048_576)]
    public string? SharedFoldersJson { get; set; }
    public string? MappedDrivesJson { get; set; }
}

public class GpuRequest
{
    [StringLength(300)]
    public string? GpuName { get; set; }
    [StringLength(200)]
    public string? GpuManufacturer { get; set; }
    [StringLength(100)]
    public string? GpuDriverVersion { get; set; }
    public DateTime? GpuDriverDate { get; set; }
    public long? GpuRam { get; set; }
    [StringLength(50)]
    public string? GpuResolution { get; set; }
    [StringLength(1_048_576)]
    public string? GraphicsCardsJson { get; set; }
    [StringLength(1_048_576)]
    public string? MonitorsJson { get; set; }
    [Range(0, 100)]
    public int? MonitorCount { get; set; }
}

public class NetworkRequest
{
    [StringLength(1_048_576)]
    public string? NetworkAdaptersJson { get; set; }
    [StringLength(200)]
    public string? DomainName { get; set; }
    [StringLength(200)]
    public string? DnsHostname { get; set; }
    [StringLength(200)]
    public string? Workgroup { get; set; }
    public bool? DomainJoined { get; set; }
    [StringLength(45)]
    public string? DefaultGateway { get; set; }
    [StringLength(500)]
    public string? DnsServers { get; set; }
    [StringLength(45)]
    public string? DhcpServer { get; set; }
}

public class OsRequest
{
    [StringLength(300)]
    public string? OsName { get; set; }
    [StringLength(100)]
    public string? OsVersion { get; set; }
    [StringLength(100)]
    public string? OsBuild { get; set; }
    [StringLength(50)]
    public string? OsArchitecture { get; set; }
    [StringLength(50)]
    public string? OsInstallDate { get; set; }
    [StringLength(50)]
    public string? OsLastBootTime { get; set; }
    [StringLength(200)]
    public string? OsSerialNumber { get; set; }
    [StringLength(200)]
    public string? OsProductKey { get; set; }
    public int? OsServicePackMajor { get; set; }
    public int? OsServicePackMinor { get; set; }
    [StringLength(50)]
    public string? OsLocale { get; set; }
    [StringLength(50)]
    public string? OsLanguage { get; set; }
    [StringLength(100)]
    public string? TimeZone { get; set; }
    public long? OsUptime { get; set; }
    [StringLength(200)]
    public string? WindowsProductId { get; set; }
    [StringLength(200)]
    public string? WindowsRegisteredOwner { get; set; }
    [StringLength(200)]
    public string? WindowsRegisteredOrganization { get; set; }
    public bool? WindowsIsActivated { get; set; }
    [StringLength(100)]
    public string? WindowsActivationStatus { get; set; }
    [StringLength(1_048_576)]
    public string? EnvironmentVariablesJson { get; set; }
}

public class SecurityRequest
{
    [StringLength(200)]
    public string? AntivirusProduct { get; set; }
    [StringLength(100)]
    public string? AntivirusStatus { get; set; }
    [StringLength(100)]
    public string? FirewallStatus { get; set; }
    public bool? BitLockerEnabled { get; set; }
    [StringLength(200)]
    public string? BitLockerStatus { get; set; }
    public bool? SecureBootEnabled { get; set; }
    public bool? TpmPresent { get; set; }
    [StringLength(50)]
    public string? TpmVersion { get; set; }
    public bool? UacEnabled { get; set; }
}

public class BatteryRequest
{
    public bool? HasBattery { get; set; }
    [Range(0, 100)]
    public int? BatteryChargePercent { get; set; }
    [StringLength(100)]
    public string? BatteryStatus { get; set; }
    public int? BatteryEstimatedRuntime { get; set; }
    [StringLength(100)]
    public string? BatteryHealth { get; set; }
    public int? BatteryCycleCount { get; set; }
    public int? BatteryDesignCapacity { get; set; }
    public int? BatteryFullChargeCapacity { get; set; }
}

public class ChassisRequest
{
    [StringLength(200)]
    public string? SystemSkuNumber { get; set; }
    [StringLength(200)]
    public string? SystemFamily { get; set; }
    [StringLength(100)]
    public string? ChassisType { get; set; }
    [StringLength(200)]
    public string? ChassisManufacturer { get; set; }
    [StringLength(200)]
    public string? ChassisSerialNumber { get; set; }
    [StringLength(200)]
    public string? ChassisAssetTag { get; set; }
}

public class SoftwareRequest
{
    [StringLength(5_242_880)]
    public string? InstalledSoftwareJson { get; set; }
    [Range(0, 100000)]
    public int? InstalledSoftwareCount { get; set; }
    [StringLength(5_242_880)]
    public string? InstalledUpdatesJson { get; set; }
    [Range(0, 100000)]
    public int? InstalledUpdatesCount { get; set; }
    public DateTime? LastUpdateDate { get; set; }
    [StringLength(5_242_880)]
    public string? RunningProcessesJson { get; set; }
    [Range(0, 100000)]
    public int? RunningProcessesCount { get; set; }
    [StringLength(5_242_880)]
    public string? ServicesJson { get; set; }
    [Range(0, 100000)]
    public int? RunningServicesCount { get; set; }
    [Range(0, 100000)]
    public int? StoppedServicesCount { get; set; }
    [StringLength(5_242_880)]
    public string? StartupProgramsJson { get; set; }
    [Range(0, 10000)]
    public int? StartupProgramsCount { get; set; }
    [StringLength(5_242_880)]
    public string? ScheduledTasksJson { get; set; }
    [Range(0, 100000)]
    public int? ScheduledTasksCount { get; set; }
}

public class UsersGroupsRequest
{
    [StringLength(1_048_576)]
    public string? LocalUsersJson { get; set; }
    [StringLength(1_048_576)]
    public string? LocalGroupsJson { get; set; }
    [StringLength(1_048_576)]
    public string? LoggedOnUsersJson { get; set; }
}

public class PeripheralsRequest
{
    [StringLength(1_048_576)]
    public string? PrintersJson { get; set; }
    [StringLength(1_048_576)]
    public string? UsbDevicesJson { get; set; }
    [StringLength(1_048_576)]
    public string? AudioDevicesJson { get; set; }
}

public class EventLogsRequest
{
    [StringLength(5_242_880)]
    public string? RecentErrorsJson { get; set; }
    [StringLength(5_242_880)]
    public string? RecentWarningsJson { get; set; }
    [Range(0, 100000)]
    public int? BlueScreenCount { get; set; }
    public DateTime? LastBlueScreen { get; set; }
}

public class PerformanceRequest
{
    [Range(0, 100)]
    public double? CpuUsagePercent { get; set; }
    [Range(0, 100)]
    public double? MemoryUsagePercent { get; set; }
    [Range(0, 100)]
    public double? DiskUsagePercent { get; set; }
}
