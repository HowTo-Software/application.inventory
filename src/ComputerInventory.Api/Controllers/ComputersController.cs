using ComputerInventory.Core.Constants;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Api.Authorization;
using ComputerInventory.Api.Hubs;
using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel.DataAnnotations;

// Use aliases to avoid conflicts with local request types that use entity enums
using ContractComputerDto = ComputerInventory.Contracts.ComputerDto;
using ContractComputerListDto = ComputerInventory.Contracts.ComputerListDto;
using ContractInventoryStats = ComputerInventory.Contracts.InventoryStats;
using ContractBulkStatusUpdateRequest = ComputerInventory.Contracts.BulkStatusUpdateRequest;
using ContractBulkOperationResult = ComputerInventory.Contracts.BulkOperationResult;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComputersController : ControllerBase
{
    private readonly IComputerRepository _computerRepository;
    private readonly IComputerHistoryRepository _historyRepository;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly ICacheService _cacheService;
    private readonly CacheServiceOptions _cacheOptions;
    private readonly ILogger<ComputersController> _logger;
    private readonly ReferenceDataResolver _resolver;
    private readonly IHubContext<UpdateProgressHub> _hubContext;
    private readonly IErrorAnalyzerService _errorAnalyzer;
    private readonly IAuthorizationService _authorizationService;

    private static readonly SameDepartmentRequirement _sameDepartmentRequirement = new();

    public ComputersController(
        IComputerRepository computerRepository,
        IComputerHistoryRepository historyRepository,
        IBackgroundTaskQueue backgroundTaskQueue,
        ICacheService cacheService,
        CacheServiceOptions cacheOptions,
        ILogger<ComputersController> logger,
        ReferenceDataResolver resolver,
        IHubContext<UpdateProgressHub> hubContext,
        IErrorAnalyzerService errorAnalyzer,
        IAuthorizationService authorizationService)
    {
        _computerRepository = computerRepository;
        _historyRepository = historyRepository;
        _backgroundTaskQueue = backgroundTaskQueue;
        _cacheService = cacheService;
        _cacheOptions = cacheOptions;
        _logger = logger;
        _resolver = resolver;
        _hubContext = hubContext;
        _errorAnalyzer = errorAnalyzer;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractComputerListDto>>> GetAll(
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        // Cache the full list; apply sort in-memory to keep one cache entry.
        // Uses paginated overload with large page size to avoid loading unbounded results.
        const int maxPageSize = 10_000;
        var computers = await _cacheService.GetOrCreateAsync(
            CacheKeys.ComputerList,
            async () =>
            {
                var (items, _) = await _computerRepository.GetAllAsync(
                    1, maxPageSize, cancellationToken: cancellationToken);
                return items.ToList();
            },
            TimeSpan.FromSeconds(_cacheOptions.ComputerListCacheSeconds));
        
        // Calculate cutoff once for the entire list (optimization)
        var onlineCutoff = GetOnlineCutoff();
        var dtos = computers.Select(c => MapToListDto(c, onlineCutoff));

        // Apply in-memory sort if requested
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var desc = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
            dtos = sortBy.ToLowerInvariant() switch
            {
                "hostname" => desc ? dtos.OrderByDescending(c => c.Hostname) : dtos.OrderBy(c => c.Hostname),
                "serialnumber" => desc ? dtos.OrderByDescending(c => c.SerialNumber) : dtos.OrderBy(c => c.SerialNumber),
                "ipaddress" => desc ? dtos.OrderByDescending(c => c.IpAddress) : dtos.OrderBy(c => c.IpAddress),
                "status" => desc ? dtos.OrderByDescending(c => c.Status) : dtos.OrderBy(c => c.Status),
                "type" => desc ? dtos.OrderByDescending(c => c.Type) : dtos.OrderBy(c => c.Type),
                "location" => desc ? dtos.OrderByDescending(c => c.Location) : dtos.OrderBy(c => c.Location),
                "department" => desc ? dtos.OrderByDescending(c => c.Department) : dtos.OrderBy(c => c.Department),
                "manufacturer" => desc ? dtos.OrderByDescending(c => c.Manufacturer) : dtos.OrderBy(c => c.Manufacturer),
                "model" => desc ? dtos.OrderByDescending(c => c.Model) : dtos.OrderBy(c => c.Model),
                "assignedto" => desc ? dtos.OrderByDescending(c => c.AssignedTo) : dtos.OrderBy(c => c.AssignedTo),
                "updatedat" => desc ? dtos.OrderByDescending(c => c.UpdatedAt) : dtos.OrderBy(c => c.UpdatedAt),
                "lastseen" => desc ? dtos.OrderByDescending(c => c.AgentStatus.LastSeen) : dtos.OrderBy(c => c.AgentStatus.LastSeen),
                "agentversion" => desc ? dtos.OrderByDescending(c => c.AgentStatus.AgentVersion) : dtos.OrderBy(c => c.AgentStatus.AgentVersion),
                "isonline" => desc ? dtos.OrderByDescending(c => c.AgentStatus.IsOnline) : dtos.OrderBy(c => c.AgentStatus.IsOnline),
                _ => dtos // unrecognized field — keep default order
            };
        }

        return Ok(dtos);
    }

    [HttpGet("paged")]
    [ProducesResponseType(typeof(Contracts.PagedResult<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<ContractComputerListDto>>> GetAllPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? location = null,
        [FromQuery] string? department = null,
        [FromQuery] string? connection = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (computers, totalCount) = await _computerRepository.GetAllAsync(
            page, pageSize, search, status, type, location, department, connection,
            sortBy, sortDirection, cancellationToken);

        var onlineCutoff = GetOnlineCutoff();
        return Ok(new Contracts.PagedResult<ContractComputerListDto>
        {
            Items = computers.Select(c => MapToListDto(c, onlineCutoff)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ContractComputerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractComputerDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var computer = await _computerRepository.GetByIdAsync(id, cancellationToken);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        return Ok(MapToDto(computer));
    }

    /// <summary>
    /// Download error logs from a failed agent update
    /// </summary>
    [HttpGet("{id}/error-logs")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetUpdateErrorLogs(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();
        
        if (string.IsNullOrEmpty(computer.AgentStatus.ActiveUpdateRequest?.UpdateErrorLogs))
        {
            return NotFound(new { message = "No error logs available for this computer" });
        }
        
        // Return as a downloadable text file
        var fileName = $"{computer.Hostname}_{computer.SerialNumber}_update_error_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
        var content = System.Text.Encoding.UTF8.GetBytes(computer.AgentStatus.ActiveUpdateRequest.UpdateErrorLogs);
        
        return File(content, "text/plain", fileName);
    }

    /// <summary>
    /// Analyze error logs and return structured error categorization with remediation suggestions
    /// </summary>
    [HttpGet("{id}/error-analysis")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(ErrorAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ErrorAnalysisResult>> GetErrorAnalysis(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();
        
        if (string.IsNullOrEmpty(computer.AgentStatus.ActiveUpdateRequest?.UpdateErrorLogs))
        {
            return NotFound(new { message = "No error logs available to analyze for this computer" });
        }
        
        var activeReq = computer.AgentStatus.ActiveUpdateRequest!;
        var analysis = _errorAnalyzer.Analyze(computer.Id, activeReq.UpdateErrorLogs!, (AgentUpdateStatus)activeReq.UpdateStatusId);
        return Ok(analysis);
    }

    /// <summary>
    /// Clear error logs for a computer (when retrying or dismissing)
    /// </summary>
    [HttpDelete("{id}/error-logs")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ClearUpdateErrorLogs(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();
        
        if (computer.AgentStatus.ActiveUpdateRequest != null)
        {
            computer.AgentStatus.ActiveUpdateRequest.UpdateErrorLogs = null;
        }
        await _computerRepository.UpdateAsync(computer);
        
        return Ok(new { message = "Error logs cleared" });
    }

    [HttpGet("serial/{serialNumber}")]
    [ProducesResponseType(typeof(ContractComputerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractComputerDto>> GetBySerialNumber(string serialNumber, CancellationToken cancellationToken)
    {
        var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber, cancellationToken);
        if (computer == null)
        {
            return NotFound();
        }
        return Ok(MapToDto(computer));
    }

    [HttpGet("status/{status}")]
    [ProducesResponseType(typeof(Contracts.PagedResult<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<ContractComputerListDto>>> GetByStatus(
        ComputerStatus status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (computers, totalCount) = await _computerRepository.GetByStatusAsync(status, page, pageSize, sortBy, sortDirection, cancellationToken);
        return Ok(new Contracts.PagedResult<ContractComputerListDto>
        {
            Items = computers.Select(MapToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("location/{locationId:int}")]
    [ProducesResponseType(typeof(Contracts.PagedResult<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<ContractComputerListDto>>> GetByLocation(
        int locationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (computers, totalCount) = await _computerRepository.GetByLocationAsync(locationId, page, pageSize, sortBy, sortDirection, cancellationToken);
        return Ok(new Contracts.PagedResult<ContractComputerListDto>
        {
            Items = computers.Select(MapToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("department/{departmentId:int}")]
    [ProducesResponseType(typeof(Contracts.PagedResult<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<ContractComputerListDto>>> GetByDepartment(
        int departmentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (computers, totalCount) = await _computerRepository.GetByDepartmentAsync(departmentId, page, pageSize, sortBy, sortDirection, cancellationToken);
        return Ok(new Contracts.PagedResult<ContractComputerListDto>
        {
            Items = computers.Select(MapToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(Contracts.PagedResult<ContractComputerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Contracts.PagedResult<ContractComputerListDto>>> Search(
        [FromQuery] string term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;

        var (computers, totalCount) = await _computerRepository.SearchAsync(term, page, pageSize, sortBy, sortDirection, cancellationToken);
        return Ok(new Contracts.PagedResult<ContractComputerListDto>
        {
            Items = computers.Select(MapToListDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ContractInventoryStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContractInventoryStats>> GetStats(CancellationToken cancellationToken)
    {
        // Cache stats for 30 seconds - they don't change frequently
        var stats = await _cacheService.GetOrCreateAsync(
            CacheKeys.ComputerStats,
            async () =>
            {
                var total = await _computerRepository.GetTotalCountAsync(cancellationToken);
                var statusCounts = await _computerRepository.GetStatusCountsAsync(cancellationToken);
                return new ContractInventoryStats
                {
                    TotalComputers = total,
                    StatusCounts = statusCounts.ToDictionary(x => x.Key.ToString(), x => x.Value)
                };
            },
            TimeSpan.FromSeconds(_cacheOptions.ComputerStatsCacheSeconds));
        
        return Ok(stats);
    }

    [HttpPost]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(ContractComputerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractComputerDto>> Create([FromBody] CreateComputerRequest request)
    {
        var existingComputer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);
        if (existingComputer != null)
        {
            return BadRequest("A computer with this serial number already exists.");
        }

        var computer = new Computer
        {
            SerialNumber = request.SerialNumber,
            Hostname = request.Hostname,
            IpAddress = request.IpAddress,
            MacAddress = request.MacAddress,
            ModelId = await _resolver.ResolveModelIdAsync(request.Manufacturer, request.Model),
            OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem),
            Processor = request.Processor,
            RamGB = request.RamGB,
            StorageGB = request.StorageGB,
            TypeId = (int)request.Type,
            StatusId = (int)request.Status,
            LocationId = await _resolver.ResolveLocationIdAsync(request.Location),
            DepartmentId = await _resolver.ResolveDepartmentIdAsync(request.Department),
            Notes = request.Notes,
            PurchaseDate = request.PurchaseDate,
            WarrantyExpiration = request.WarrantyExpiration,
            PurchasePrice = request.PurchasePrice,
            AssignedTo = request.AssignedTo
        };

        var created = await _computerRepository.AddAsync(computer);
        
        // Invalidate caches after modification
        InvalidateComputerCaches();
        
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateComputerRequest request)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        // Parse Type enum from string
        if (!Enum.TryParse<ComputerType>(request.Type, true, out var computerType))
        {
            return BadRequest($"Invalid computer type: {request.Type}");
        }
        
        // Parse Status enum from string
        if (!Enum.TryParse<ComputerStatus>(request.Status, true, out var computerStatus))
        {
            return BadRequest($"Invalid computer status: {request.Status}");
        }

        // Capture old values for audit log
        var oldHostname = computer.Hostname;
        var oldStatus = computer.StatusLookup?.Name;
        var oldAssignedTo = computer.AssignedTo;
        var oldLocation = computer.LocationEntity?.Name;
        var oldDepartment = computer.DepartmentEntity?.Name;

        computer.Hostname = request.Hostname;
        computer.IpAddress = request.IpAddress;
        computer.MacAddress = request.MacAddress;
        computer.ModelId = await _resolver.ResolveModelIdAsync(request.Manufacturer, request.Model);
        computer.OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem);
        computer.Processor = request.Processor;
        computer.RamGB = request.RamGB;
        computer.StorageGB = request.StorageGB;
        computer.TypeId = (int)computerType;
        computer.StatusId = (int)computerStatus;
        computer.LocationId = await _resolver.ResolveLocationIdAsync(request.Location);
        computer.DepartmentId = await _resolver.ResolveDepartmentIdAsync(request.Department);
        computer.Notes = request.Notes;
        computer.WarrantyExpiration = request.WarrantyExpiration;
        computer.AssignedTo = request.AssignedTo;
        computer.AgentStatus.HeartbeatIntervalMinutes = request.HeartbeatIntervalMinutes;

        await _computerRepository.UpdateAsync(computer);

        // Build change summary for audit log
        var changes = new List<string>();
        if (oldHostname != request.Hostname) changes.Add($"Hostname: {oldHostname} → {request.Hostname}");
        if (oldStatus != request.Status) changes.Add($"Status: {oldStatus} → {request.Status}");
        if (oldAssignedTo != request.AssignedTo) changes.Add($"AssignedTo: {oldAssignedTo ?? "(none)"} → {request.AssignedTo ?? "(none)"}");
        if (oldLocation != request.Location) changes.Add($"Location: {oldLocation ?? "(none)"} → {request.Location ?? "(none)"}");
        if (oldDepartment != request.Department) changes.Add($"Department: {oldDepartment ?? "(none)"} → {request.Department ?? "(none)"}");

        var changedBy = User.Identity?.Name;
        var changeSummary = changes.Count > 0 ? string.Join("; ", changes) : "Properties updated";

        await _historyRepository.AddHistorySpAsync(
            computer.Id,
            HistoryActions.ComputerEdited,
            changedBy: changedBy,
            newValue: changeSummary);
        
        // Invalidate caches after modification
        InvalidateComputerCaches();
        
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        await _computerRepository.DeleteAsync(id);
        
        // Invalidate caches after modification
        InvalidateComputerCaches();
        
        return NoContent();
    }

    /// <summary>
    /// Export all computers as CSV
    /// </summary>
    [HttpGet("export")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Export(CancellationToken cancellationToken)
    {
        // Page through all records in batches to avoid loading entire table at once
        const int batchSize = 1000;
        var allComputers = new List<Computer>();
        int page = 1;

        while (true)
        {
            var (items, totalCount) = await _computerRepository.GetAllAsync(
                page, batchSize, cancellationToken: cancellationToken);
            allComputers.AddRange(items);
            if (allComputers.Count >= totalCount) break;
            page++;
        }

        var onlineCutoff = GetOnlineCutoff();
        var dtos = allComputers.Select(c => MapToDto(c, onlineCutoff)).ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SerialNumber,Hostname,IpAddress,MacAddress,Manufacturer,Model,OperatingSystem,Processor,RamGB,StorageGB,Type,Status,Location,Department,AssignedTo,PurchaseDate,WarrantyExpiration,PurchasePrice,Notes");

        foreach (var c in dtos)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(c.SerialNumber),
                CsvEscape(c.Hostname),
                CsvEscape(c.IpAddress),
                CsvEscape(c.MacAddress),
                CsvEscape(c.Manufacturer),
                CsvEscape(c.Model),
                CsvEscape(c.OperatingSystem),
                CsvEscape(c.Processor),
                c.RamGB?.ToString() ?? "",
                c.StorageGB?.ToString() ?? "",
                CsvEscape(c.Type),
                CsvEscape(c.Status),
                CsvEscape(c.Location),
                CsvEscape(c.Department),
                CsvEscape(c.AssignedTo),
                c.PurchaseDate.ToString("yyyy-MM-dd"),
                c.WarrantyExpiration?.ToString("yyyy-MM-dd") ?? "",
                c.PurchasePrice?.ToString() ?? "",
                CsvEscape(c.Notes)));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"computers_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    /// <summary>
    /// Import computers from CSV content
    /// </summary>
    [HttpPost("import")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(ContractBulkOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractBulkOperationResult>> Import([FromBody] string csvContent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
            return BadRequest("CSV content is empty.");

        var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2)
            return BadRequest("CSV must contain a header row and at least one data row.");

        // Validate header
        var header = lines[0];
        var expectedHeader = "SerialNumber,Hostname,IpAddress,MacAddress,Manufacturer,Model,OperatingSystem,Processor,RamGB,StorageGB,Type,Status,Location,Department,AssignedTo,PurchaseDate,WarrantyExpiration,PurchasePrice,Notes";
        if (!header.StartsWith("SerialNumber,Hostname"))
            return BadRequest("Invalid CSV header. Expected: " + expectedHeader);

        var result = new ContractBulkOperationResult();
        var userName = User.Identity?.Name;

        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var fields = ParseCsvLine(lines[i]);
                if (fields.Count < 6)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Row {i + 1}: insufficient columns (minimum 6 required).");
                    continue;
                }

                var serialNumber = fields[0].Trim();
                if (string.IsNullOrWhiteSpace(serialNumber))
                {
                    result.FailureCount++;
                    result.Errors.Add($"Row {i + 1}: serial number is required.");
                    continue;
                }

                // Check for duplicate
                var existing = await _computerRepository.GetBySerialNumberAsync(serialNumber, cancellationToken);
                if (existing != null)
                {
                    result.FailureCount++;
                    result.Errors.Add($"Row {i + 1}: serial number '{serialNumber}' already exists.");
                    continue;
                }

                var hostname = fields.Count > 1 ? fields[1].Trim() : serialNumber;
                if (string.IsNullOrWhiteSpace(hostname))
                    hostname = serialNumber;

                var manufacturer = fields.Count > 4 ? fields[4].Trim() : "Unknown";
                var model = fields.Count > 5 ? fields[5].Trim() : "Unknown";
                if (string.IsNullOrWhiteSpace(manufacturer)) manufacturer = "Unknown";
                if (string.IsNullOrWhiteSpace(model)) model = "Unknown";

                var typeStr = fields.Count > 10 ? fields[10].Trim() : "Desktop";
                var statusStr = fields.Count > 11 ? fields[11].Trim() : ComputerStatusNames.Available;

                if (!Enum.TryParse<ComputerType>(typeStr, true, out var computerType))
                    computerType = ComputerType.Desktop;
                if (!Enum.TryParse<ComputerStatus>(statusStr, true, out var computerStatus))
                    computerStatus = ComputerStatus.Available;

                int? ramGb = fields.Count > 8 && int.TryParse(fields[8].Trim(), out var ram) ? ram : null;
                int? storageGb = fields.Count > 9 && int.TryParse(fields[9].Trim(), out var storage) ? storage : null;
                decimal? purchasePrice = fields.Count > 17 && decimal.TryParse(fields[17].Trim(), out var price) ? price : null;
                DateTime purchaseDate = fields.Count > 15 && DateTime.TryParse(fields[15].Trim(), out var pd) ? pd : DateTime.Today;
                DateTime? warrantyExp = fields.Count > 16 && DateTime.TryParse(fields[16].Trim(), out var we) ? we : null;

                var computer = new Computer
                {
                    SerialNumber = serialNumber,
                    Hostname = hostname,
                    IpAddress = fields.Count > 2 ? NullIfEmpty(fields[2]) : null,
                    MacAddress = fields.Count > 3 ? NullIfEmpty(fields[3]) : null,
                    ModelId = await _resolver.ResolveModelIdAsync(manufacturer, model, cancellationToken),
                    OperatingSystemId = fields.Count > 6 ? await _resolver.ResolveOperatingSystemIdAsync(NullIfEmpty(fields[6]), cancellationToken) : null,
                    Processor = fields.Count > 7 ? NullIfEmpty(fields[7]) : null,
                    RamGB = ramGb,
                    StorageGB = storageGb,
                    TypeId = (int)computerType,
                    StatusId = (int)computerStatus,
                    LocationId = fields.Count > 12 ? await _resolver.ResolveLocationIdAsync(NullIfEmpty(fields[12]), cancellationToken) : null,
                    DepartmentId = fields.Count > 13 ? await _resolver.ResolveDepartmentIdAsync(NullIfEmpty(fields[13]), cancellationToken) : null,
                    AssignedTo = fields.Count > 14 ? NullIfEmpty(fields[14]) : null,
                    PurchaseDate = purchaseDate,
                    WarrantyExpiration = warrantyExp,
                    PurchasePrice = purchasePrice,
                    Notes = fields.Count > 18 ? NullIfEmpty(fields[18]) : null,
                    CreatedBy = userName
                };

                await _computerRepository.AddAsync(computer, cancellationToken);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add($"Row {i + 1}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to import computer from CSV row {Row}", i + 1);
            }
        }

        if (result.SuccessCount > 0)
            InvalidateComputerCaches();

        return Ok(result);
    }

    /// <summary>
    /// Batch update status for multiple computers
    /// </summary>
    [HttpPost("bulk-status")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(typeof(ContractBulkOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractBulkOperationResult>> BulkUpdateStatus(
        [FromBody] ContractBulkStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (request.ComputerIds == null || request.ComputerIds.Count == 0)
            return BadRequest("No computer IDs specified.");

        if (request.ComputerIds.Count > 500)
            return BadRequest("Cannot update more than 500 computers at once.");

        if (!Enum.TryParse<ComputerStatus>(request.Status, true, out var status))
            return BadRequest($"Invalid status: {request.Status}");

        // Per-computer authorization: filter to only computers the user can access
        var computers = await _computerRepository.GetByIdsAsync(request.ComputerIds, cancellationToken);
        var authorizedIds = new List<int>();
        foreach (var computer in computers)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
            if (authResult.Succeeded)
                authorizedIds.Add(computer.Id);
        }

        if (authorizedIds.Count == 0)
            return Forbid();

        var updatedCount = await _computerRepository.BulkUpdateStatusAsync(
            authorizedIds, status, User.Identity?.Name, cancellationToken);

        InvalidateComputerCaches();

        return Ok(new ContractBulkOperationResult
        {
            SuccessCount = updatedCount,
            FailureCount = request.ComputerIds.Count - updatedCount
        });
    }

    /// <summary>
    /// Batch delete multiple computers
    /// </summary>
    [HttpPost("bulk-delete")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(ContractBulkOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContractBulkOperationResult>> BulkDelete(
        [FromBody] List<int> computerIds, CancellationToken cancellationToken)
    {
        if (computerIds == null || computerIds.Count == 0)
            return BadRequest("No computer IDs specified.");

        if (computerIds.Count > 500)
            return BadRequest("Cannot delete more than 500 computers at once.");

        var result = new ContractBulkOperationResult();

        // Per-computer authorization: verify access before deleting
        var computers = await _computerRepository.GetByIdsAsync(computerIds, cancellationToken);
        var authorizedIds = new HashSet<int>();
        foreach (var computer in computers)
        {
            var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
            if (authResult.Succeeded)
                authorizedIds.Add(computer.Id);
        }

        foreach (var id in computerIds)
        {
            if (!authorizedIds.Contains(id))
            {
                result.FailureCount++;
                continue;
            }
            var deleted = await _computerRepository.DeleteDirectAsync(id, cancellationToken);
            if (deleted)
                result.SuccessCount++;
            else
                result.FailureCount++;
        }

        if (result.SuccessCount > 0)
            InvalidateComputerCaches();

        return Ok(result);
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        var oldStatus = computer.StatusLookup?.Name ?? computer.StatusId.ToString();

        computer.StatusId = (int)request.Status;
        await _computerRepository.UpdateAsync(computer);

        await _historyRepository.AddHistorySpAsync(
            computer.Id,
            HistoryActions.StatusChange,
            changedBy: User.Identity?.Name,
            oldValue: oldStatus,
            newValue: request.Status.ToString());

        return NoContent();
    }

    [HttpPatch("{id}/assign")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AssignUser(int id, [FromBody] AssignUserRequest request)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            return NotFound();
        }

        var authResult2 = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult2.Succeeded)
            return Forbid();

        var oldAssignedTo = computer.AssignedTo;

        computer.AssignedTo = request.AssignedTo;
        computer.StatusId = !string.IsNullOrWhiteSpace(request.AssignedTo) ? (int)ComputerStatus.Assigned : (int)ComputerStatus.Available;
        await _computerRepository.UpdateAsync(computer);

        await _historyRepository.AddHistorySpAsync(
            computer.Id,
            HistoryActions.AssignmentChanged,
            changedBy: User.Identity?.Name,
            oldValue: oldAssignedTo ?? "(none)",
            newValue: request.AssignedTo ?? "(none)");

        return NoContent();
    }

    [HttpPost("{id}/force-update")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ForceUpdate(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            _logger.LogWarning("Force update requested for non-existent computer ID {Id} by {User}", 
                id, User.Identity?.Name);
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        if (!computer.AgentStatus.HasAgent)
        {
            _logger.LogWarning("Force update requested for computer {Serial} without agent by {User}", 
                computer.SerialNumber, User.Identity?.Name);
            return BadRequest(new { message = "Computer does not have an agent installed" });
        }

        // Check if update is already pending
        if (computer.AgentStatus.ForceUpdate)
        {
            var existingReq = computer.AgentStatus.ActiveUpdateRequest;
            var pendingDuration = existingReq?.UpdateRequestedAt.HasValue == true
                ? (DateTime.UtcNow - existingReq.UpdateRequestedAt!.Value).TotalMinutes 
                : 0;
            
            _logger.LogWarning(
                "Force update re-requested for {Serial} by {User}. Already pending for {Duration:F1}min (requested by {OriginalUser})",
                computer.SerialNumber, 
                User.Identity?.Name,
                pendingDuration,
                existingReq?.UpdateRequestedBy ?? "unknown");
            
            // Reset the timer but keep it pending
            var reReq = computer.AgentStatus.EnsureActiveUpdateRequest();
            reReq.UpdateRequestedAt = DateTime.UtcNow;
            reReq.UpdateRequestedBy = User.Identity?.Name;
            await _computerRepository.UpdateAsync(computer);
            
            return Ok(new { 
                message = $"Update already pending for {computer.Hostname}. Timer reset.",
                alreadyPending = true,
                previousDuration = pendingDuration
            });
        }

        // Log current state before update
        _logger.LogInformation(
            "Force update initiated for {Serial} by {User}. Current state: Version={Version}, Online={Online}, LastSeen={LastSeen}",
            computer.SerialNumber, 
            User.Identity?.Name,
            computer.AgentStatus.AgentVersion ?? "unknown",
            computer.AgentStatus.IsOnline,
            computer.AgentStatus.LastSeen?.ToString("o") ?? "never");

        computer.AgentStatus.ForceUpdate = true;
        var updateReq = computer.AgentStatus.EnsureActiveUpdateRequest();
        updateReq.UpdateRequestedAt = DateTime.UtcNow;
        updateReq.UpdateRequestedBy = User.Identity?.Name;
        await _computerRepository.UpdateAsync(computer);

        // Broadcast update-requested state to connected UI clients
        await _hubContext.Clients.All.SendAsync("UpdateProgress", new UpdateProgressMessage
        {
            ComputerId = computer.Id,
            SerialNumber = computer.SerialNumber,
            Hostname = computer.Hostname,
            UpdateStatus = (int)updateReq.UpdateStatusId,
            UpdateProgress = updateReq.UpdateProgress,
            UpdateStatusMessage = "Update requested — waiting for agent heartbeat",
            UpdateStartedAt = updateReq.UpdateStartedAt,
            HasUpdateErrorLogs = !string.IsNullOrEmpty(updateReq.UpdateErrorLogs),
            ForceUpdate = true
        });
        
        // Estimate next heartbeat based on last seen
        var nextHeartbeatEstimate = "~5 min";
        if (computer.AgentStatus.LastSeen.HasValue)
        {
            var timeSinceLastSeen = DateTime.UtcNow - computer.AgentStatus.LastSeen.Value;
            var heartbeatInterval = TimeSpan.FromMinutes(5);
            var timeUntilNext = heartbeatInterval - TimeSpan.FromMinutes(timeSinceLastSeen.TotalMinutes % 5);
            nextHeartbeatEstimate = $"~{timeUntilNext.TotalMinutes:F0} min";
        }
        
        return Ok(new { 
            message = $"Update signal sent to {computer.Hostname}. Computer will update on next heartbeat ({nextHeartbeatEstimate}).",
            currentVersion = computer.AgentStatus.AgentVersion,
            nextHeartbeatEstimate = nextHeartbeatEstimate
        });
    }

    [HttpPost("{id}/cancel-update")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelUpdate(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            _logger.LogWarning("Cancel update requested for non-existent computer ID {Id} by {User}", 
                id, User.Identity?.Name);
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        // Check if there's anything to clear (pending update OR failed/in-progress status)
        var cancelReq = computer.AgentStatus.ActiveUpdateRequest;
        var hasUpdateState = computer.AgentStatus.ForceUpdate 
            || cancelReq?.UpdateRequestedAt.HasValue == true
            || (cancelReq?.UpdateStatusId ?? (int)AgentUpdateStatus.None) != (int)AgentUpdateStatus.None
            || cancelReq?.UpdateErrorLogs != null;

        if (!hasUpdateState)
        {
            _logger.LogDebug("Cancel update requested for {Serial} but no update state to clear", 
                computer.SerialNumber);
            return BadRequest(new { message = "No pending or failed update to clear" });
        }

        // Log the cancellation details
        var pendingDuration = cancelReq?.UpdateRequestedAt.HasValue == true
            ? (DateTime.UtcNow - cancelReq.UpdateRequestedAt!.Value).TotalMinutes 
            : 0;
        
        _logger.LogInformation(
            "Update state cleared for {Serial} by {User}. Was pending for {Duration:F1}min (originally requested by {OriginalUser}). ForceUpdate={ForceUpdate}, UpdateRequestedAt={RequestedAt}, UpdateStatus={UpdateStatus}",
            computer.SerialNumber, 
            User.Identity?.Name,
            pendingDuration,
            cancelReq?.UpdateRequestedBy ?? "unknown",
            computer.AgentStatus.ForceUpdate,
            cancelReq?.UpdateRequestedAt?.ToString("o") ?? "null",
            cancelReq?.UpdateStatusId ?? (int)AgentUpdateStatus.None);

        computer.AgentStatus.ForceUpdate = false;
        computer.AgentStatus.ClearActiveUpdateRequest();
        computer.AgentStatus.ReinstallAttempted = false;
        await _computerRepository.UpdateAsync(computer);

        // Broadcast cancellation to connected UI clients
        await _hubContext.Clients.All.SendAsync("UpdateProgress", new UpdateProgressMessage
        {
            ComputerId = computer.Id,
            SerialNumber = computer.SerialNumber,
            Hostname = computer.Hostname,
            UpdateStatus = 0,
            UpdateProgress = 0,
            UpdateStatusMessage = null,
            ForceUpdate = false,
            ReinstallAttempted = false
        });
        
        return Ok(new { 
            message = $"Update status cleared for {computer.Hostname}.",
            wasPendingMinutes = pendingDuration
        });
    }

    /// <summary>
    /// Trigger last resort reinstall for a computer. This is a nuclear option that forces
    /// the agent to download and execute the install script, completely reinstalling itself.
    /// Use this when normal force-update repeatedly fails.
    /// </summary>
    [HttpPost("{id}/force-reinstall")]
    [Authorize(Roles = "Admin")]  // Admin only - this is dangerous
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ForceReinstall(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            _logger.LogWarning("Force reinstall requested for non-existent computer ID {Id} by {User}", 
                id, User.Identity?.Name);
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        if (!computer.AgentStatus.HasAgent)
        {
            return BadRequest(new { message = "Computer does not have an agent installed" });
        }

        if (!computer.AgentStatus.IsOnline)
        {
            return BadRequest(new { message = "Computer is offline. Agent must be online to receive reinstall command." });
        }

        computer.AgentStatus.ForceReinstall = true;
        computer.AgentStatus.ReinstallAttempted = true;
        computer.AgentStatus.ForceUpdate = false;  // Clear normal update flag
        var reinstallReq = computer.AgentStatus.EnsureActiveUpdateRequest();
        reinstallReq.UpdateRequestedAt = DateTime.UtcNow;
        reinstallReq.UpdateRequestedBy = User.Identity?.Name;
        await _computerRepository.UpdateAsync(computer);

        _logger.LogWarning(
            "🔴 FORCE REINSTALL initiated for {Serial} ({Hostname}) by {User}. Current version: {Version}",
            computer.SerialNumber, 
            computer.Hostname,
            User.Identity?.Name,
            computer.AgentStatus.AgentVersion ?? "unknown");
        
        return Ok(new { 
            message = $"Force reinstall signal sent to {computer.Hostname}. Agent will perform a full reinstall on next heartbeat (~5 min max). This is a nuclear option.",
            currentVersion = computer.AgentStatus.AgentVersion,
            warning = "The agent will download and execute the install script, completely replacing itself."
        });
    }

    /// <summary>
    /// Manual override for computers stuck in a failed state after both update and reinstall
    /// have failed. Clears all update state and records the admin's decision.
    /// </summary>
    [HttpPost("{id}/override-update")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> OverrideUpdate(int id, [FromBody] OverrideUpdateRequest request)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            _logger.LogWarning("Override update requested for non-existent computer ID {Id} by {User}",
                id, User.Identity?.Name);
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        var overrideReq = computer.AgentStatus.ActiveUpdateRequest;
        var hasFailedState = (overrideReq?.UpdateStatusId ?? (int)AgentUpdateStatus.None) == (int)AgentUpdateStatus.Failed
            || computer.AgentStatus.ReinstallAttempted;

        if (!hasFailedState)
        {
            return BadRequest(new { message = "Computer is not in a failed update state requiring override." });
        }

        _logger.LogWarning(
            "\u26a0\ufe0f MANUAL OVERRIDE for {Serial} ({Hostname}) by {User}. Reason: {Reason}. " +
            "Previous state: UpdateStatus={Status}, ReinstallAttempted={Reinstall}, Message={Message}",
            computer.SerialNumber,
            computer.Hostname,
            User.Identity?.Name,
            request.Reason ?? "No reason provided",
            overrideReq?.UpdateStatusId ?? (int)AgentUpdateStatus.None,
            computer.AgentStatus.ReinstallAttempted,
            overrideReq?.UpdateStatusMessage);

        // Clear all update state
        computer.AgentStatus.ForceUpdate = false;
        computer.AgentStatus.ForceReinstall = false;
        computer.AgentStatus.ReinstallAttempted = false;
        computer.AgentStatus.ClearActiveUpdateRequest();
        await _computerRepository.UpdateAsync(computer);

        // Broadcast state reset to connected UI clients
        await _hubContext.Clients.All.SendAsync("UpdateProgress", new UpdateProgressMessage
        {
            ComputerId = computer.Id,
            SerialNumber = computer.SerialNumber,
            Hostname = computer.Hostname,
            UpdateStatus = 0,
            UpdateProgress = 0,
            UpdateStatusMessage = null,
            ForceUpdate = false,
            ReinstallAttempted = false
        });

        return Ok(new
        {
            message = $"Manual override applied for {computer.Hostname}. All update state has been cleared.",
            overriddenBy = User.Identity?.Name,
            reason = request.Reason
        });
    }

    [HttpPost("{id}/request-checkin")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RequestImmediateCheckin(int id)
    {
        var computer = await _computerRepository.GetByIdAsync(id);
        if (computer == null)
        {
            _logger.LogWarning("Immediate check-in requested for non-existent computer ID {Id} by {User}", 
                id, User.Identity?.Name);
            return NotFound();
        }

        var authResult = await _authorizationService.AuthorizeAsync(User, computer, _sameDepartmentRequirement);
        if (!authResult.Succeeded)
            return Forbid();

        if (!computer.AgentStatus.HasAgent)
        {
            return BadRequest(new { message = "Computer does not have an agent installed" });
        }

        if (!computer.AgentStatus.IsOnline)
        {
            return BadRequest(new { message = "Computer is offline. Cannot request immediate check-in." });
        }

        computer.AgentStatus.RequestImmediateCheckin = true;
        await _computerRepository.UpdateAsync(computer);

        // Estimate next heartbeat
        var nextHeartbeatEstimate = "~5 min";
        if (computer.AgentStatus.LastSeen.HasValue)
        {
            var timeSinceLastSeen = DateTime.UtcNow - computer.AgentStatus.LastSeen.Value;
            var heartbeatInterval = TimeSpan.FromMinutes(5);
            var timeUntilNext = heartbeatInterval - TimeSpan.FromMinutes(timeSinceLastSeen.TotalMinutes % 5);
            nextHeartbeatEstimate = $"~{Math.Max(1, (int)timeUntilNext.TotalMinutes)} min";
        }

        _logger.LogInformation(
            "Immediate check-in requested for {Serial} by {User}. Expected in {NextHeartbeat}.",
            computer.SerialNumber, User.Identity?.Name, nextHeartbeatEstimate);
        
        return Ok(new { 
            message = $"Check-in request sent to {computer.Hostname}. Agent will respond on next heartbeat ({nextHeartbeatEstimate}) then immediately check in again.",
            nextHeartbeatEstimate = nextHeartbeatEstimate
        });
    }

    /// <summary>
    /// Invalidate computer-related caches after modifications.
    /// </summary>
    private void InvalidateComputerCaches()
    {
        _cacheService.InvalidatePrefix(CacheKeys.PrefixComputers);
        _cacheService.InvalidatePrefix(CacheKeys.PrefixComputer);
    }


    // Calculate online cutoff once to avoid repeated DateTime.UtcNow.AddMinutes calls in hot paths
    private static DateTime GetOnlineCutoff() => DateTime.UtcNow.AddMinutes(-10);

    private static ContractComputerListDto MapToListDto(Computer computer) => MapToListDto(computer, GetOnlineCutoff());

    private static ContractComputerListDto MapToListDto(Computer computer, DateTime onlineCutoff) => new()
    {
        Id = computer.Id,
        SerialNumber = computer.SerialNumber,
        Hostname = computer.Hostname,
        IpAddress = computer.IpAddress,
        Manufacturer = computer.ComputerModel?.Manufacturer?.Name ?? "",
        Model = computer.ComputerModel?.ModelName ?? "",
        Type = computer.TypeLookup?.Name ?? computer.TypeId.ToString(),
        Status = computer.StatusLookup?.Name ?? computer.StatusId.ToString(),
        Location = computer.LocationEntity?.Name,
        Department = computer.DepartmentEntity?.Name,
        AssignedTo = computer.AssignedTo,
        UpdatedAt = computer.UpdatedAt,
        AgentStatus = new ComputerAgentStatusDto
        {
            LastSeen = computer.AgentStatus.LastSeen,
            IsOnline = computer.AgentStatus.IsOnline && computer.AgentStatus.LastSeen.HasValue && computer.AgentStatus.LastSeen.Value > onlineCutoff,
            CurrentUser = computer.AgentStatus.CurrentUser,
            HasAgent = computer.AgentStatus.HasAgent,
            AgentVersion = computer.AgentStatus.AgentVersion,
            HeartbeatIntervalMinutes = computer.AgentStatus.HeartbeatIntervalMinutes
        },
        UpdateInfo = new ComputerUpdateStatusDto
        {
            ForceUpdate = computer.AgentStatus.ForceUpdate,
            UpdateRequestedAt = computer.AgentStatus.ActiveUpdateRequest?.UpdateRequestedAt,
            UpdateRequestedBy = computer.AgentStatus.ActiveUpdateRequest?.UpdateRequestedBy,
            LastUpdatedAt = computer.AgentStatus.ActiveUpdateRequest?.LastUpdatedAt,
            PreviousAgentVersion = computer.AgentStatus.ActiveUpdateRequest?.PreviousAgentVersion,
            UpdateStatus = computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusId ?? (int)AgentUpdateStatus.None,
            UpdateProgress = computer.AgentStatus.ActiveUpdateRequest?.UpdateProgress ?? 0,
            UpdateStatusMessage = computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusMessage,
            UpdateStartedAt = computer.AgentStatus.ActiveUpdateRequest?.UpdateStartedAt,
            HasUpdateErrorLogs = !string.IsNullOrEmpty(computer.AgentStatus.ActiveUpdateRequest?.UpdateErrorLogs),
            ReinstallAttempted = computer.AgentStatus.ReinstallAttempted
        }
    };

    private static ContractComputerDto MapToDto(Computer computer) => MapToDto(computer, GetOnlineCutoff());

    private static ContractComputerDto MapToDto(Computer computer, DateTime onlineCutoff) => new()
    {
        Id = computer.Id,
        SerialNumber = computer.SerialNumber,
        Hostname = computer.Hostname,
        IpAddress = computer.IpAddress,
        MacAddress = computer.MacAddress,
        Manufacturer = computer.ComputerModel?.Manufacturer?.Name ?? "",
        Model = computer.ComputerModel?.ModelName ?? "",
        OperatingSystem = computer.OperatingSystemEntity?.Name,
        Processor = computer.Processor,
        RamGB = computer.RamGB,
        StorageGB = computer.StorageGB,
        Type = computer.TypeLookup?.Name ?? computer.TypeId.ToString(),
        Status = computer.StatusLookup?.Name ?? computer.StatusId.ToString(),
        Location = computer.LocationEntity?.Name,
        Department = computer.DepartmentEntity?.Name,
        Notes = computer.Notes,
        PurchaseDate = computer.PurchaseDate,
        WarrantyExpiration = computer.WarrantyExpiration,
        PurchasePrice = computer.PurchasePrice,
        AssignedTo = computer.AssignedTo,
        CreatedAt = computer.CreatedAt,
        UpdatedAt = computer.UpdatedAt,
        AgentStatus = new ComputerAgentStatusDto
        {
            LastSeen = computer.AgentStatus.LastSeen,
            // Use precalculated cutoff time to avoid DateTime.UtcNow.AddMinutes on every item
            IsOnline = computer.AgentStatus.IsOnline && computer.AgentStatus.LastSeen.HasValue && computer.AgentStatus.LastSeen.Value > onlineCutoff,
            CurrentUser = computer.AgentStatus.CurrentUser,
            HasAgent = computer.AgentStatus.HasAgent,
            AgentVersion = computer.AgentStatus.AgentVersion,
            AgentSessionId = computer.AgentStatus.AgentSessionId,
            HeartbeatIntervalMinutes = computer.AgentStatus.HeartbeatIntervalMinutes
        },
        UpdateInfo = new ComputerUpdateStatusDto
        {
            ForceUpdate = computer.AgentStatus.ForceUpdate,
            UpdateRequestedAt = computer.AgentStatus.ActiveUpdateRequest?.UpdateRequestedAt,
            UpdateRequestedBy = computer.AgentStatus.ActiveUpdateRequest?.UpdateRequestedBy,
            LastUpdatedAt = computer.AgentStatus.ActiveUpdateRequest?.LastUpdatedAt,
            PreviousAgentVersion = computer.AgentStatus.ActiveUpdateRequest?.PreviousAgentVersion,
            UpdateStatus = computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusId ?? (int)AgentUpdateStatus.None,
            UpdateProgress = computer.AgentStatus.ActiveUpdateRequest?.UpdateProgress ?? 0,
            UpdateStatusMessage = computer.AgentStatus.ActiveUpdateRequest?.UpdateStatusMessage,
            UpdateStartedAt = computer.AgentStatus.ActiveUpdateRequest?.UpdateStartedAt,
            UpdateErrorLogs = computer.AgentStatus.ActiveUpdateRequest?.UpdateErrorLogs,
            HasUpdateErrorLogs = !string.IsNullOrEmpty(computer.AgentStatus.ActiveUpdateRequest?.UpdateErrorLogs),
            ReinstallAttempted = computer.AgentStatus.ReinstallAttempted
        }
    };

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }
        fields.Add(current.ToString());
        return fields;
    }
}

// Note: ComputerDto and InventoryStats are now in ComputerInventory.Contracts
// The local CreateComputerRequest uses entity enums (ComputerType, ComputerStatus) for direct model binding

/// <summary>
/// Request model for creating a new computer.
/// Uses entity enums for direct model binding from the API.
/// </summary>
public class CreateComputerRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Hostname { get; set; } = string.Empty;
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [Required, StringLength(200)]
    public string Manufacturer { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Model { get; set; } = string.Empty;
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    public ComputerType Type { get; set; }
    public ComputerStatus Status { get; set; }
    [StringLength(200)]
    public string? Location { get; set; }
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    [Range(0, 1_000_000)]
    public decimal? PurchasePrice { get; set; }
    [StringLength(200)]
    public string? AssignedTo { get; set; }
}

/// <summary>
/// Request model for updating an existing computer.
/// Uses strings for Type and Status to allow flexible input.
/// </summary>
public class UpdateComputerRequest
{
    [Required, StringLength(100)]
    public string Hostname { get; set; } = string.Empty;
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [Required, StringLength(200)]
    public string Manufacturer { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string Model { get; set; } = string.Empty;
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    [Required, StringLength(50)]
    public string Type { get; set; } = "Desktop";
    [Required, StringLength(50)]
    public string Status { get; set; } = ComputerStatusNames.Available;
    [StringLength(200)]
    public string? Location { get; set; }
    [StringLength(200)]
    public string? Department { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    [StringLength(200)]
    public string? AssignedTo { get; set; }
    [Range(1, 1440)]
    public int HeartbeatIntervalMinutes { get; set; } = 5;
}

/// <summary>
/// Request model for updating computer status.
/// </summary>
public class UpdateStatusRequest
{
    public ComputerStatus Status { get; set; }
}

/// <summary>
/// Request model for assigning a user to a computer.
/// </summary>
public class AssignUserRequest
{
    [StringLength(200)]
    public string? AssignedTo { get; set; }
}

/// <summary>
/// Request model for manual override of a stuck update/reinstall failure.
/// </summary>
public class OverrideUpdateRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
