using ComputerInventory.Core.Constants;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Api.Hubs;
using ComputerInventory.Api.Services;
using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ComputerInventory.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IComputerRepository _computerRepository;
    private readonly IComputerHistoryRepository _historyRepository;
    private readonly IGeoLocationRepository _geoLocationRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgentController> _logger;
    private readonly IBackgroundTaskQueue _backgroundTaskQueue;
    private readonly IUpdateInfoCacheService _updateInfoCache;
    private readonly IPackageService _packageService;
    private readonly ReferenceDataResolver _resolver;
    private readonly IHubContext<UpdateProgressHub> _hubContext;
    private readonly IInstallTokenService _installTokenService;
    private readonly AgentFileSettings _fileSettings;

    public AgentController(
        IComputerRepository computerRepository,
        IComputerHistoryRepository historyRepository,
        IGeoLocationRepository geoLocationRepository,
        IConfiguration configuration,
        ILogger<AgentController> logger,
        IBackgroundTaskQueue backgroundTaskQueue,
        IUpdateInfoCacheService updateInfoCache,
        IPackageService packageService,
        ReferenceDataResolver resolver,
        IHubContext<UpdateProgressHub> hubContext,
        IInstallTokenService installTokenService,
        AgentFileSettings fileSettings)
    {
        _computerRepository = computerRepository;
        _historyRepository = historyRepository;
        _geoLocationRepository = geoLocationRepository;
        _configuration = configuration;
        _logger = logger;
        _backgroundTaskQueue = backgroundTaskQueue;
        _updateInfoCache = updateInfoCache;
        _packageService = packageService;
        _resolver = resolver;
        _hubContext = hubContext;
        _installTokenService = installTokenService;
        _fileSettings = fileSettings;
    }

    private static ComputerHistory CreateHistory(int computerId, string action, string? changedBy = ActorNames.Agent, string? oldValue = null, string? newValue = null, string? notes = null)
    {
        return new ComputerHistory
        {
            ComputerId = computerId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedBy = changedBy,
            ChangedAt = DateTime.UtcNow,
            Notes = notes
        };
    }

    /// <summary>
    /// Extracts the X-Agent-Session-Id header sent by the thin client to correlate
    /// registration, heartbeat, and progress calls from the same agent session.
    /// </summary>
    private string? GetAgentSessionId()
    {
        var sessionId = Request.Headers["X-Agent-Session-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > 64)
            return null;
        return sessionId;
    }

    /// <summary>
    /// Gets the configured server URL for agents, falling back to request-derived URL.
    /// </summary>
    private string GetConfiguredServerUrl()
    {
        var configuredUrl = _configuration["AgentSettings:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl.TrimEnd('/');
        }
        return $"{Request.Scheme}://{Request.Host}";
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AgentRegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentRegistrationResponse>> Register([FromBody] AgentRegistrationRequest request)
    {
        // Fetch the computer once up front — reused for both API key validation and registration
        Computer? existingComputer = null;

        // Validate API key — allow known computers with wrong/empty key to re-register
        if (!ValidateApiKey())
        {
            existingComputer = await _computerRepository.GetBySerialNumberBasicAsync(request.SerialNumber, includeDeleted: true);
            if (existingComputer == null)
            {
                return Unauthorized(new AgentRegistrationResponse { Success = false, Message = "Invalid API key" });
            }
            var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _logger.LogWarning("Registration accepted with wrong/empty API key from known computer {Serial} (IP: {Ip}).",
                request.SerialNumber, clientIp);
        }

        try
        {
            // Use BasicAsync with includeDeleted to bypass both INNER JOIN on AgentStatus
            // and the soft-delete query filter — both cause null returns for valid computers
            existingComputer ??= await _computerRepository.GetBySerialNumberBasicAsync(request.SerialNumber, includeDeleted: true);
            var agentSessionId = GetAgentSessionId();

            if (existingComputer != null)
            {
                // Undelete if this computer was soft-deleted — agent is alive, so restore it
                if (existingComputer.IsDeleted)
                {
                    existingComputer.IsDeleted = false;
                    existingComputer.DeletedAt = null;
                    _logger.LogInformation("Restoring soft-deleted computer {Serial} via agent registration", request.SerialNumber);
                }

                // Ensure AgentStatus exists (may be missing if computer was created without one)
                if (existingComputer.AgentStatus == null)
                {
                    existingComputer.AgentStatus = new ComputerAgentStatus { ComputerId = existingComputer.Id };
                }

                // Update existing computer with all fields
                existingComputer.Hostname = request.Hostname;
                existingComputer.IpAddress = request.IpAddress;
                existingComputer.MacAddress = request.MacAddress;
                if (!string.IsNullOrEmpty(request.Manufacturer) && !string.IsNullOrEmpty(request.Model))
                    existingComputer.ModelId = await _resolver.ResolveModelIdAsync(
                        request.Manufacturer, request.Model);
                existingComputer.OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem);
                existingComputer.Processor = request.Processor;
                existingComputer.RamGB = request.RamGB;
                existingComputer.StorageGB = request.StorageGB;
                existingComputer.TypeId = (int)ComputerTypeParser.Parse(request.Type);
                existingComputer.UpdatedBy = ActorNames.Agent;
                existingComputer.AgentStatus.LastSeen = DateTime.UtcNow;
                existingComputer.AgentStatus.IsOnline = true;
                existingComputer.AgentStatus.CurrentUser = request.CurrentUser;
                existingComputer.AgentStatus.HasAgent = true;
                existingComputer.AgentStatus.AgentSessionId = agentSessionId;

                await _computerRepository.UpdateAsync(existingComputer);

                _logger.LogInformation("Updated computer {Serial} via agent (SessionId={AgentSessionId})", request.SerialNumber, agentSessionId);

                return Ok(new AgentRegistrationResponse
                {
                    Success = true,
                    ComputerId = existingComputer.Id,
                    Message = "Computer updated successfully",
                    Configuration = GetAgentConfiguration()
                });
            }
            else
            {
                // Create new computer
                var computer = new Computer
                {
                    SerialNumber = request.SerialNumber,
                    Hostname = request.Hostname,
                    IpAddress = request.IpAddress,
                    MacAddress = request.MacAddress,
                    ModelId = await _resolver.ResolveModelIdAsync(
                        request.Manufacturer ?? "Auto-Registered", request.Model ?? "Auto-Registered"),
                    OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem),
                    Processor = request.Processor,
                    RamGB = request.RamGB,
                    StorageGB = request.StorageGB,
                    TypeId = (int)ComputerTypeParser.Parse(request.Type),
                    StatusId = (int)ComputerStatus.Available,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedBy = ActorNames.Agent,
                    AgentStatus = new ComputerAgentStatus
                    {
                        LastSeen = DateTime.UtcNow,
                        IsOnline = true,
                        CurrentUser = request.CurrentUser,
                        HasAgent = true,
                        AgentSessionId = agentSessionId
                    }
                };

                var created = await _computerRepository.AddAsync(computer);

                _logger.LogInformation("Registered new computer {Serial} via agent (SessionId={AgentSessionId})", request.SerialNumber, agentSessionId);


                return Ok(new AgentRegistrationResponse
                {
                    Success = true,
                    ComputerId = created.Id,
                    Message = "Computer registered successfully",
                    Configuration = GetAgentConfiguration()
                });
            }
        }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx && sqlEx.Number == 2601)
        {
            // Duplicate key: computer exists in DB but GetBySerialNumberAsync returned null
            // (can happen if computer_agent_statuses row is missing — the Include causes INNER JOIN).
            // Clear change tracker and retry with a direct query that doesn't require AgentStatus.
            _logger.LogWarning("Duplicate key on register for {Serial} — retrying as upsert", request.SerialNumber);
            try
            {
                _computerRepository.ClearChangeTracker();
                var existing = await _computerRepository.GetBySerialNumberBasicAsync(request.SerialNumber, includeDeleted: true);
                if (existing != null)
                {
                    // Undelete if soft-deleted
                    if (existing.IsDeleted)
                    {
                        existing.IsDeleted = false;
                        existing.DeletedAt = null;
                    }

                    // Create AgentStatus if missing
                    if (existing.AgentStatus == null)
                    {
                        existing.AgentStatus = new ComputerAgentStatus { ComputerId = existing.Id };
                    }

                    var agentSessionId2 = GetAgentSessionId();
                    existing.Hostname = request.Hostname;
                    existing.IpAddress = request.IpAddress;
                    existing.MacAddress = request.MacAddress;
                    if (!string.IsNullOrEmpty(request.Manufacturer) && !string.IsNullOrEmpty(request.Model))
                        existing.ModelId = await _resolver.ResolveModelIdAsync(request.Manufacturer, request.Model);
                    existing.OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem);
                    existing.Processor = request.Processor;
                    existing.RamGB = request.RamGB;
                    existing.StorageGB = request.StorageGB;
                    existing.TypeId = (int)ComputerTypeParser.Parse(request.Type);
                    existing.UpdatedBy = ActorNames.Agent;
                    existing.AgentStatus.LastSeen = DateTime.UtcNow;
                    existing.AgentStatus.IsOnline = true;
                    existing.AgentStatus.CurrentUser = request.CurrentUser;
                    existing.AgentStatus.HasAgent = true;
                    existing.AgentStatus.AgentSessionId = agentSessionId2;

                    await _computerRepository.UpdateAsync(existing);

                    _logger.LogInformation("Resolved duplicate key for {Serial} — updated successfully", request.SerialNumber);
                    return Ok(new AgentRegistrationResponse
                    {
                        Success = true,
                        ComputerId = existing.Id,
                        Message = "Computer updated successfully (resolved duplicate)",
                        Configuration = GetAgentConfiguration()
                    });
                }
                else
                {
                    _logger.LogError("Duplicate key error for {Serial} but computer not found in retry query", request.SerialNumber);
                }
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Retry update also failed for {Serial}", request.SerialNumber);
            }

            return StatusCode(500, new AgentRegistrationResponse { Success = false, Message = "Internal server error" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering computer {Serial}", request.SerialNumber);
            return StatusCode(500, new AgentRegistrationResponse { Success = false, Message = "Internal server error" });
        }
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var apiKeyValid = ValidateApiKey();
        if (!apiKeyValid)
        {
            // Allow heartbeats from known computers with wrong/empty API key so they stay online.
            // Log a warning for visibility. The agent will function normally but is flagged.
            var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (string.IsNullOrEmpty(request?.SerialNumber))
            {
                return Unauthorized();
            }
            var knownComputer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (knownComputer == null)
            {
                _logger.LogWarning("Heartbeat rejected: unknown serial {Serial} from {Ip} with invalid API key.",
                    request.SerialNumber, clientIp);
                return Unauthorized();
            }
            _logger.LogWarning("Heartbeat accepted with wrong/empty API key from known computer {Serial} (IP: {Ip}). " +
                "Agent needs API key update.", request.SerialNumber, clientIp);
        }

        try
        {
            var computer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (computer == null)
            {
                // Reject heartbeat for unregistered computers — they must register via /register first
                _logger.LogWarning("Heartbeat rejected for unregistered serial {Serial}. Agent must call /register first.", request.SerialNumber);
                return NotFound(new HeartbeatResponse { RequiresRegistration = true });
            }

            // Collect history entries to write AFTER SaveChangesAsync succeeds.
            // SP calls auto-commit, so writing them before Save means orphaned
            // history rows when Save fails (e.g. concurrency conflict).
            var pendingHistory = new List<(int ComputerId, string Action, string? ChangedBy, string? OldValue, string? NewValue, string? Notes)>();

            // Track agent session across requests
            var agentSessionId = GetAgentSessionId();
            computer.AgentStatus.AgentSessionId = agentSessionId;

            // Update last seen information
            computer.IpAddress = request.IpAddress;
            computer.AgentStatus.PublicIpAddress = request.PublicIpAddress;
            computer.AgentStatus.LastSeen = DateTime.UtcNow;
            computer.AgentStatus.IsOnline = true;
            computer.AgentStatus.CurrentUser = request.CurrentUser;

            // Self-healing: If agent is reporting in but marked as No Agent, restore status
            if (!computer.AgentStatus.HasAgent)
            {
                computer.AgentStatus.HasAgent = true;
                _logger.LogInformation(
                    "Self-healing: Restored HasAgent=true for {Serial} on heartbeat reconnection. AgentVersion={Version}",
                    request.SerialNumber, request.AgentVersion ?? "unknown");

                pendingHistory.Add((computer.Id, HistoryActions.AgentSelfHealed, null,
                    "No Agent", $"Agent (v{request.AgentVersion ?? "unknown"})", null));
            }

            ProcessVersionChange(computer, request, pendingHistory);
            
            if (!string.IsNullOrEmpty(request.Hostname))
                computer.Hostname = request.Hostname;
            if (!string.IsNullOrEmpty(request.MacAddress))
                computer.MacAddress = request.MacAddress;
            if (!string.IsNullOrEmpty(request.OperatingSystem))
                computer.OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(request.OperatingSystem);
            
            // Update hardware info if we have better data than "Auto-Registered"
            if (!string.IsNullOrEmpty(request.Manufacturer) && !string.IsNullOrEmpty(request.Model) &&
                (computer.ComputerModel?.Manufacturer?.Name == "Auto-Registered" || string.IsNullOrEmpty(computer.ComputerModel?.Manufacturer?.Name)))
                computer.ModelId = await _resolver.ResolveModelIdAsync(request.Manufacturer, request.Model);
            if (!string.IsNullOrEmpty(request.Processor) && string.IsNullOrEmpty(computer.Processor))
                computer.Processor = request.Processor;
            if (request.RamGB.HasValue && (!computer.RamGB.HasValue || computer.RamGB == 0))
                computer.RamGB = request.RamGB;
            if (request.StorageGB.HasValue && (!computer.StorageGB.HasValue || computer.StorageGB == 0))
                computer.StorageGB = request.StorageGB;
            if (!string.IsNullOrEmpty(request.Type))
                computer.TypeId = (int)ComputerTypeParser.Parse(request.Type);
            
            await ProcessLocationUpdate(computer, request);

            var (forceUpdate, requestImmediateCheckin, forceReinstall) = CheckForceUpdate(computer, request, pendingHistory);

            // Heartbeat history entry
            pendingHistory.Add((computer.Id, HistoryActions.Heartbeat, null,
                null, $"Online, User: {request.CurrentUser}", null));

            // Single SaveChangesAsync for all batched entity changes:
            // computer entity updates, GPS location, and force-update flag changes
            await _computerRepository.SaveChangesAsync();

            // Only write history AFTER entity save succeeds — SP calls auto-commit
            // and must not run when the entity save might roll back.
            foreach (var h in pendingHistory)
            {
                await _historyRepository.AddHistorySpAsync(
                    h.ComputerId, h.Action, h.ChangedBy, h.OldValue, h.NewValue, h.Notes);
            }

            _logger.LogDebug(
                "Heartbeat response for {Serial}: ForceUpdate={ForceUpdate}, RequestImmediateCheckin={RequestImmediateCheckin}, ForceReinstall={ForceReinstall}, Version={Version}, SessionId={AgentSessionId}",
                request.SerialNumber, forceUpdate, requestImmediateCheckin, forceReinstall, request.AgentVersion ?? "null", agentSessionId);

            return Ok(new HeartbeatResponse { ForceUpdate = forceUpdate, RequestImmediateCheckin = requestImmediateCheckin, ForceReinstall = forceReinstall, HeartbeatIntervalMinutes = computer.AgentStatus.HeartbeatIntervalMinutes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat for {Serial}", request.SerialNumber);
            return StatusCode(500, new { message = "Error processing heartbeat", serial = request.SerialNumber });
        }
    }

    private void ProcessVersionChange(Computer computer, HeartbeatRequest request, List<(int ComputerId, string Action, string? ChangedBy, string? OldValue, string? NewValue, string? Notes)> pendingHistory)
    {
        if (string.IsNullOrEmpty(request.AgentVersion))
            return;

        var oldVersion = computer.AgentStatus.AgentVersion;
        var versionChanged = !string.IsNullOrEmpty(oldVersion) && oldVersion != request.AgentVersion;

        if (versionChanged)
        {
            var updateRequest = computer.AgentStatus.EnsureActiveUpdateRequest();
            updateRequest.PreviousAgentVersion = oldVersion;
            updateRequest.LastUpdatedAt = DateTime.UtcNow;

            var wasUpdatePending = computer.AgentStatus.ForceUpdate || updateRequest.UpdateRequestedAt.HasValue;
            computer.AgentStatus.ForceUpdate = false;
            updateRequest.UpdateRequestedAt = null;
            updateRequest.UpdateRequestedBy = null;

            _logger.LogInformation(
                "Agent version changed on {Serial}: {OldVersion} -> {NewVersion}. UpdateWasPending={WasPending}, UpdateDuration={Duration}",
                request.SerialNumber,
                oldVersion,
                request.AgentVersion,
                wasUpdatePending,
                wasUpdatePending && updateRequest.LastUpdatedAt.HasValue
                    ? $"{(DateTime.UtcNow - updateRequest.LastUpdatedAt.Value).TotalMinutes:F1}min"
                    : "N/A");

            pendingHistory.Add((computer.Id, HistoryActions.AgentUpdated, null,
                oldVersion, request.AgentVersion, null));
        }

        // NOTE: Do NOT clear ForceUpdate here just because version matches!
        // The flag is cleared in CheckForceUpdate after signaling it to the agent.
        computer.AgentStatus.AgentVersion = request.AgentVersion;
    }

    private async Task ProcessLocationUpdate(Computer computer, HeartbeatRequest request)
    {
        if (!request.GpsLatitude.HasValue || !request.GpsLongitude.HasValue)
            return;

        await _geoLocationRepository.TrackGpsLocationUpdateAsync(
            computer.Id,
            request.GpsLatitude.Value,
            request.GpsLongitude.Value,
            request.PublicIpAddress);

        _logger.LogDebug("Updated GPS location for {Serial}: Lat={Lat}, Lon={Lon}",
            request.SerialNumber, request.GpsLatitude, request.GpsLongitude);
    }

    private (bool ForceUpdate, bool RequestImmediateCheckin, bool ForceReinstall) CheckForceUpdate(Computer computer, HeartbeatRequest request, List<(int ComputerId, string Action, string? ChangedBy, string? OldValue, string? NewValue, string? Notes)> pendingHistory)
    {
        // IMPORTANT: Keep sending ForceUpdate until:
        // 1. Agent reports update completed/failed via progress endpoint
        // 2. Agent version actually changes
        // 3. Admin manually cancels the update
        // 4. Agent is already at the latest version (no update available)
        // NOTE: All entity changes here are tracked and saved in the batched SaveChangesAsync at the end of Heartbeat.
        var forceUpdate = computer.AgentStatus.ForceUpdate;
        var activeReq = computer.AgentStatus.ActiveUpdateRequest;
        if (forceUpdate)
        {
            var updateInfo = _updateInfoCache.GetUpdateInfo();
            var availableVersion = updateInfo?.Version;

            if (!string.IsNullOrEmpty(availableVersion) && !string.IsNullOrEmpty(request.AgentVersion))
            {
                try
                {
                    var current = new Version(request.AgentVersion);
                    var available = new Version(availableVersion);

                    if (current >= available)
                    {
                        _logger.LogInformation(
                            "Clearing ForceUpdate for {Serial} - already at latest version ({Current} >= {Available})",
                            request.SerialNumber, request.AgentVersion, availableVersion);

                        computer.AgentStatus.ForceUpdate = false;
                        if (activeReq != null)
                        {
                            activeReq.UpdateRequestedAt = null;
                            activeReq.UpdateRequestedBy = null;
                            activeReq.UpdateStartedAt = null;
                        }
                        computer.AgentStatus.ClearActiveUpdateRequest();
                        forceUpdate = false;
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to parse version strings for {Serial}: AgentVersion='{AgentVersion}', AvailableVersion='{AvailableVersion}'", request.SerialNumber, request.AgentVersion, availableVersion); }
            }

            if (forceUpdate)
            {
                _logger.LogInformation(
                    "Force update signal sent to {Serial}. State: Version={Version}, RequestedAt={RequestedAt}, RequestedBy={RequestedBy}",
                    request.SerialNumber,
                    computer.AgentStatus.AgentVersion ?? "null",
                    activeReq?.UpdateRequestedAt?.ToString("o") ?? "null",
                    activeReq?.UpdateRequestedBy ?? "null");
            }
        }
        else if (activeReq?.UpdateRequestedAt.HasValue == true)
        {
            var requestAge = DateTime.UtcNow - activeReq.UpdateRequestedAt!.Value;
            if (requestAge.TotalMinutes > 30)
            {
                _logger.LogWarning(
                    "Clearing orphaned update request for {Serial}. Age={Minutes}min, RequestedBy={RequestedBy}",
                    request.SerialNumber,
                    (int)requestAge.TotalMinutes,
                    activeReq.UpdateRequestedBy ?? "null");

                activeReq.UpdateRequestedAt = null;
                activeReq.UpdateRequestedBy = null;
                computer.AgentStatus.ClearActiveUpdateRequest();
            }
        }

        var requestImmediateCheckin = computer.AgentStatus.RequestImmediateCheckin;
        if (requestImmediateCheckin)
        {
            computer.AgentStatus.RequestImmediateCheckin = false;
            _logger.LogInformation("Immediate check-in signaled for {Serial}", request.SerialNumber);
        }

        var forceReinstall = computer.AgentStatus.ForceReinstall;

        // AUTO-ESCALATION: If force update has been pending too long, escalate to reinstall
        if (!forceReinstall && forceUpdate && activeReq?.UpdateRequestedAt.HasValue == true)
        {
            var updatePendingDuration = DateTime.UtcNow - activeReq.UpdateRequestedAt!.Value;
            var autoEscalateMinutes = _configuration.GetValue<int>("AgentSettings:AutoEscalateMinutes", 10);

            if (updatePendingDuration.TotalMinutes >= autoEscalateMinutes)
            {
                _logger.LogWarning(
                    "⚠️ Auto-escalating to ForceReinstall for {Serial}. Force update pending for {Minutes:F1} minutes without success. Version={Version}",
                    request.SerialNumber,
                    updatePendingDuration.TotalMinutes,
                    request.AgentVersion ?? "unknown");

                forceReinstall = true;
                forceUpdate = false;

                computer.AgentStatus.ForceUpdate = false;
                computer.AgentStatus.ReinstallAttempted = true;
                if (activeReq != null)
                {
                    activeReq.UpdateRequestedAt = null;
                    activeReq.UpdateRequestedBy = null;
                    activeReq.UpdateStartedAt = null;
                }
                computer.AgentStatus.ClearActiveUpdateRequest();

                pendingHistory.Add((computer.Id, "ForceReinstallAutoEscalated", "System",
                    $"ForceUpdate pending {updatePendingDuration.TotalMinutes:F1}min",
                    "Escalated to ForceReinstall", null));
            }
        }

        if (forceReinstall)
        {
            computer.AgentStatus.ForceReinstall = false;
            _logger.LogWarning("🔴 Force reinstall signaled for {Serial}", request.SerialNumber);
        }

        return (forceUpdate, requestImmediateCheckin, forceReinstall);
    }

    [HttpPost("checkin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CheckIn([FromBody] CheckInRequest request)
    {
        if (!ValidateApiKey())
        {
            // Allow known computers with wrong/empty API key
            var known = await _computerRepository.GetBySerialNumberAsync(request?.SerialNumber ?? "");
            if (known == null) return Unauthorized();
        }

        if (request is null) return BadRequest();

        try
        {
            var computer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (computer == null)
            {
                // Auto-register computer if it doesn't exist
                _logger.LogInformation("Computer {Serial} not found during check-in, auto-registering...", request.SerialNumber);
                computer = new Computer
                {
                    SerialNumber = request.SerialNumber,
                    Hostname = request.Hostname ?? Environment.MachineName,
                    IpAddress = request.IpAddress,
                    ModelId = await _resolver.ResolveModelIdAsync("Auto-Registered", "Auto-Registered"),
                    OperatingSystemId = await _resolver.ResolveOperatingSystemIdAsync(Environment.OSVersion.ToString()),
                    TypeId = (int)ComputerType.Desktop,  // Default to Desktop (most common)
                    StatusId = (int)ComputerStatus.Available,
                    PurchaseDate = DateTime.UtcNow,
                    CreatedBy = ActorNames.Agent,
                    AgentStatus = new ComputerAgentStatus
                    {
                        LastSeen = DateTime.UtcNow,
                        IsOnline = true,
                        CurrentUser = request.UserName,
                        HasAgent = true
                    }
                };
                computer = await _computerRepository.AddAsync(computer);
                _logger.LogInformation("Auto-registered computer {Serial} via check-in", request.SerialNumber);
            }

            string action;
            // Normalize Type: Handle both enum values (0, 1, 2) and string values ("CheckIn", "CheckOut", "AutoCheckOut")
            // This is for backward compatibility since ThinClient sends enum, but API originally expected strings
            var checkInType = request.Type?.Trim() ?? "";
            
            // Check for numeric enum values from ThinClient (CheckInType enum serializes as 0, 1, 2)
            if (checkInType == "0" || checkInType.Equals("CheckIn", StringComparison.OrdinalIgnoreCase))
            {
                computer.StatusId = (int)ComputerStatus.Assigned;
                action = $"Checked in by {request.UserName}";
            }
            else if (checkInType == "1" || checkInType.Equals("CheckOut", StringComparison.OrdinalIgnoreCase))
            {
                computer.StatusId = (int)ComputerStatus.Available;
                action = "Checked out";
            }
            else if (checkInType == "2" || checkInType.Equals("AutoCheckOut", StringComparison.OrdinalIgnoreCase))
            {
                computer.StatusId = (int)ComputerStatus.Available;
                action = "Auto check-out (idle timeout)";
            }
            else
            {
                _logger.LogWarning("Invalid check-in type received: '{Type}' for {Serial}", request.Type, request.SerialNumber);
                return BadRequest($"Invalid check-in type: {request.Type}");
            }

            // Log the action - use UpdateWithHistoryAsync for atomic operation
            var historyRecord = CreateHistory(
                computer.Id, action,
                changedBy: request.UserName ?? ActorNames.Agent,
                notes: request.Notes);
            await _computerRepository.UpdateWithHistoryAsync(computer, historyRecord);

            _logger.LogInformation("{Action} for computer {Serial}", action, request.SerialNumber);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing check-in for {Serial}", request.SerialNumber);
            return StatusCode(500, new { message = "Error processing check-in", serial = request.SerialNumber });
        }
    }

    [HttpPost("checkout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> CheckOut([FromBody] CheckInRequest request)
    {
        request.Type = "CheckOut";
        return await CheckIn(request);
    }

    /// <summary>
    /// Receive update progress from ThinClient/AgentUpdater
    /// </summary>
    [HttpPost("update-progress")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UpdateProgress([FromBody] UpdateProgressRequest request)
    {
        if (!ValidateApiKey())
        {
            // Allow known computers with wrong/empty API key
            var known = await _computerRepository.GetBySerialNumberAsync(request?.SerialNumber ?? "");
            if (known == null) return Unauthorized();
        }

        if (request is null) return BadRequest();

        try
        {
            var computer = await _computerRepository.GetBySerialNumberAsync(request.SerialNumber);
            if (computer == null)
            {
                _logger.LogWarning("Update progress received for unknown computer: {Serial}", request.SerialNumber);
                return NotFound(new { message = "Computer not found" });
            }

            // Track agent session across requests
            var agentSessionId = GetAgentSessionId();
            computer.AgentStatus.AgentSessionId = agentSessionId;

            // Update progress fields on the active update request
            var updateReq = computer.AgentStatus.EnsureActiveUpdateRequest();
            updateReq.UpdateStatusId = (int)request.Status;
            updateReq.UpdateProgress = request.Progress;
            updateReq.UpdateStatusMessage = request.Message;
            
            // Store error logs if provided (usually on failure)
            if (!string.IsNullOrEmpty(request.ErrorLogs))
            {
                updateReq.UpdateErrorLogs = request.ErrorLogs;
                _logger.LogInformation("Received {LogSize} bytes of error logs from {Serial}", 
                    request.ErrorLogs.Length, request.SerialNumber);
            }
            
            // Set start time if transitioning from None/Pending to an active state
            if (request.Status > AgentUpdateStatus.Pending && !updateReq.UpdateStartedAt.HasValue)
            {
                updateReq.UpdateStartedAt = DateTime.UtcNow;
            }
            
            // Clear start time and progress if completed or failed
            if (request.Status == AgentUpdateStatus.Completed || request.Status == AgentUpdateStatus.Failed)
            {
                // Log completion
                var duration = updateReq.UpdateStartedAt.HasValue 
                    ? DateTime.UtcNow - updateReq.UpdateStartedAt.Value 
                    : TimeSpan.Zero;
                
                _logger.LogInformation(
                    "Update {Result} for {Serial}. Duration: {Duration:F1}s, Message: {Message}, SessionId={AgentSessionId}",
                    request.Status == AgentUpdateStatus.Completed ? "completed" : "FAILED",
                    request.SerialNumber,
                    duration.TotalSeconds,
                    request.Message,
                    agentSessionId);
                
                // Log to history
                await _historyRepository.AddHistorySpAsync(
                    computer.Id,
                    request.Status == AgentUpdateStatus.Completed ? HistoryActions.UpdateCompleted : HistoryActions.UpdateFailed,
                    newValue: request.Message);
                
                // If update failed, clear the tracking fields so admin can retry
                if (request.Status == AgentUpdateStatus.Failed)
                {
                    _logger.LogWarning("Update failed for {Serial}, clearing update tracking fields for retry", 
                        request.SerialNumber);

                    if (computer.AgentStatus.ReinstallAttempted)
                    {
                        _logger.LogError(
                            "⛔ COMPOUND FAILURE for {Serial}: Both update AND reinstall have failed. Manual override required.",
                            request.SerialNumber);
                        updateReq.UpdateStatusMessage = "Update and reinstall both failed — manual override required";
                    }

                    // Clear ForceUpdate so we stop sending signals
                    computer.AgentStatus.ForceUpdate = false;
                    // Keep UpdateStatus as Failed so UI shows it, but clear the request tracking
                    // Admin can trigger another ForceUpdate via the UI
                    updateReq.UpdateRequestedAt = null;
                    updateReq.UpdateRequestedBy = null;
                    updateReq.UpdateStartedAt = null;
                    // Keep UpdateErrorLogs for admin to download
                }
                else if (request.Status == AgentUpdateStatus.Completed)
                {
                    // Success - clear all update tracking fields including ForceUpdate
                    computer.AgentStatus.ForceUpdate = false;
                    updateReq.UpdateRequestedAt = null;
                    updateReq.UpdateRequestedBy = null;
                    updateReq.UpdateStartedAt = null;
                    updateReq.UpdateProgress = 100;
                    // Clear any previous error logs on successful update
                    updateReq.UpdateErrorLogs = null;
                    computer.AgentStatus.ReinstallAttempted = false;
                    // Completed — unlink the active request (record remains for history)
                    computer.AgentStatus.ClearActiveUpdateRequest();
                }
            }
            else
            {
                _logger.LogDebug(
                    "Update progress for {Serial}: {Status} ({Progress}%) - {Message}",
                    request.SerialNumber,
                    request.Status,
                    request.Progress,
                    request.Message);
            }

            await _computerRepository.UpdateAsync(computer);

            // Broadcast real-time update progress to connected UI clients
            await _hubContext.Clients.All.SendAsync("UpdateProgress", new UpdateProgressMessage
            {
                ComputerId = computer.Id,
                SerialNumber = computer.SerialNumber,
                Hostname = computer.Hostname,
                UpdateStatus = (int)updateReq.UpdateStatusId,
                UpdateProgress = updateReq.UpdateProgress,
                UpdateStatusMessage = updateReq.UpdateStatusMessage,
                UpdateStartedAt = updateReq.UpdateStartedAt,
                HasUpdateErrorLogs = !string.IsNullOrEmpty(updateReq.UpdateErrorLogs),
                ForceUpdate = computer.AgentStatus.ForceUpdate,
                ReinstallAttempted = computer.AgentStatus.ReinstallAttempted
            });

            return Ok(new { received = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing update progress for {Serial}", request.SerialNumber);
            return StatusCode(500, new { message = "Error processing update progress" });
        }
    }

    [HttpGet("status/{serialNumber}")]
    [ProducesResponseType(typeof(ComputerStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComputerStatusResponse>> GetStatus(string serialNumber)
    {
        if (!ValidateApiKey())
        {
            return Unauthorized();
        }

        var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);
        if (computer == null)
        {
            return NotFound();
        }

        return Ok(new ComputerStatusResponse
        {
            SerialNumber = computer.SerialNumber,
            Status = computer.StatusId.ToString(),
            AssignedTo = computer.AssignedTo,
            LastUpdated = computer.UpdatedAt ?? computer.CreatedAt
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
        if (string.Equals(providedKey, configuredKey, StringComparison.Ordinal))
            return true;

        // Log the mismatch with masked keys for diagnostics
        var clientIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var maskedProvided = string.IsNullOrEmpty(providedKey)
            ? "(empty)"
            : providedKey.Length > 6 ? providedKey[..3] + "***" + providedKey[^3..] : "***";
        var maskedExpected = configuredKey.Length > 6 ? configuredKey[..3] + "***" + configuredKey[^3..] : "***";
        _logger.LogWarning("API key mismatch from {ClientIp}. Provided={MaskedProvided}, Expected={MaskedExpected}",
            clientIp, maskedProvided, maskedExpected);
        return false;
    }

    private AgentConfiguration GetAgentConfiguration()
    {
        return new AgentConfiguration
        {
            HeartbeatIntervalMinutes = _configuration.GetValue<int>("AgentSettings:HeartbeatIntervalMinutes", 5),
            CheckInIntervalMinutes = _configuration.GetValue<int>("AgentSettings:CheckInIntervalMinutes", 15),
            IdleTimeoutMinutes = _configuration.GetValue<int>("AgentSettings:IdleTimeoutMinutes", 30),
            EnableAutoCheckOut = _configuration.GetValue<bool>("AgentSettings:EnableAutoCheckOut", true)
        };
    }

    /// <summary>
    /// Download the thin client installer package
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DownloadInstaller()
    {
        var agentPath = _fileSettings.GetInstallerPath();
        
        if (!System.IO.File.Exists(agentPath))
        {
            _logger.LogWarning("Agent installer not found at {Path}", agentPath);
            return NotFound(new { message = "Agent installer not available. Please contact administrator." });
        }

        var fileBytes = System.IO.File.ReadAllBytes(agentPath);
        return File(fileBytes, "application/zip", "ComputerInventoryAgent.zip");
    }

    /// <summary>
    /// Generate a time-limited, single-use install token.
    /// Returns the token and a ready-to-use install URL.
    /// </summary>
    [HttpPost("install-tokens")]
    [Authorize(Policy = "TechnicianOrAdmin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateInstallToken([FromQuery] int expirationMinutes = 60)
    {
        if (expirationMinutes < 1 || expirationMinutes > 1440)
            return BadRequest(new { message = "Expiration must be between 1 and 1440 minutes." });

        var username = User.Identity?.Name ?? "unknown";
        var (rawToken, entity) = await _installTokenService.CreateTokenAsync(username, expirationMinutes);

        var serverUrl = GetConfiguredServerUrl();
        var installUrl = $"{serverUrl}/api/agent/download/install-script?token={Uri.EscapeDataString(rawToken)}";

        return Ok(new
        {
            installUrl,
            expiresAt = entity.ExpiresAt,
            oneTimeUse = true,
            powershellCommand = $"irm '{installUrl}' | iex"
        });
    }

    /// <summary>
    /// Claim the API key using a valid install token. Called by the install script at install time.
    /// The token is NOT consumed here — it was already consumed when the script was downloaded.
    /// This endpoint uses the same token validation (must be unexpired) but checks via API key header
    /// which was already set by the install script's first-phase download.
    /// </summary>
    [HttpGet("claim-api-key")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult ClaimApiKey()
    {
        if (!ValidateApiKey())
            return Unauthorized(new { message = "Invalid API key." });

        // The agent already has the API key (it's calling with it), so this is a no-op confirmation.
        // The real security is that the install script endpoint now requires a token.
        return Ok();
    }

    /// <summary>
    /// Get installer configuration script with server URL pre-configured.
    /// Requires a valid single-use install token generated by an authenticated user.
    /// </summary>
    [HttpGet("download/install-script")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetInstallScript([FromQuery] string? token = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "Install token required. Generate one from the admin panel or via POST /api/agent/install-tokens." });
        }

        if (!await _installTokenService.ValidateAndConsumeTokenAsync(token))
        {
            return Unauthorized(new { message = "Invalid, expired, or already-used install token." });
        }

        var serverUrl = GetConfiguredServerUrl();
        var apiKey = _configuration["AgentSettings:ApiKey"] ?? "";
        
        var script = $@"#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Downloads and installs the MKS Inventory Agent.
.DESCRIPTION
    This script downloads the agent from the server and installs it as a Scheduled Task.
    The agent runs at system startup with automatic restart on failure.
    The API key is securely retrieved from the server at install time.
#>

$ErrorActionPreference = 'Stop'
$ServerUrl = '{serverUrl}'
$ApiKey = '{apiKey}'

Write-Host '======================================' -ForegroundColor Cyan
Write-Host ' MKS Inventory Agent Installer' -ForegroundColor Cyan
Write-Host '======================================' -ForegroundColor Cyan
Write-Host ''

# Create temp directory
$tempDir = Join-Path $env:TEMP 'ComputerInventoryAgent'
if (Test-Path $tempDir) {{ Remove-Item $tempDir -Recurse -Force }}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {{
    # Download agent package
    Write-Host 'Downloading agent package...' -ForegroundColor Green
    $zipPath = Join-Path $tempDir 'agent.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri ""$ServerUrl/api/agent/download"" -OutFile $zipPath -UseBasicParsing

    # Extract
    Write-Host 'Extracting files...' -ForegroundColor Green
    Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

    # Unblock all downloaded files
    Get-ChildItem -Path $tempDir -Recurse | Unblock-File -ErrorAction SilentlyContinue

    # Run PowerShell installer (installs as Scheduled Task)
    Write-Host 'Running installer...' -ForegroundColor Green
    $psInstallerPath = Join-Path $tempDir 'install.ps1'
    
    if (Test-Path $psInstallerPath) {{
        # Run install.ps1 directly - it installs as Scheduled Task (not Windows Service)
        $installerArgs = @('-ExecutionPolicy', 'Bypass', '-File', $psInstallerPath, '-ServerUrl', $ServerUrl)
        if ($ApiKey) {{ $installerArgs += @('-ApiKey', $ApiKey) }}
        & powershell.exe @installerArgs
        
        # Check if scheduled task exists and agent is running
        Start-Sleep -Seconds 2
        $task = Get-ScheduledTask -TaskName 'ComputerInventoryAgent' -ErrorAction SilentlyContinue
        $proc = Get-Process -Name 'MKS.InventoryAgent' -ErrorAction SilentlyContinue
        
        if ($task -and $proc) {{
            Write-Host ''
            Write-Host 'Installation successful!' -ForegroundColor Green
            Write-Host ""Agent running as PID: $($proc.Id)"" -ForegroundColor Gray
        }} elseif ($task) {{
            Write-Host 'Scheduled task created but agent not yet running.' -ForegroundColor Yellow
        }} else {{
            Write-Host 'Installation may have encountered issues.' -ForegroundColor Yellow
        }}
    }} else {{
        throw 'Installer script not found in package'
    }}
}}
catch {{
    Write-Host ""Error: $_"" -ForegroundColor Red
    exit 1
}}
finally {{
    # Cleanup
    if (Test-Path $tempDir) {{ Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }}
}}
";
        
        return Content(script, "text/plain", System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Check if an update is available for the agent.
    /// Uses the centralized package versioning service to resolve which version
    /// a computer should receive, with fallback to legacy file-based update.json.
    /// If the computer has ForceReinstall flag set, returns ReinstallRequired=true
    /// which tells the Updater to perform a full reinstall instead of normal update.
    /// </summary>
    [HttpGet("check-update")]
    [ProducesResponseType(typeof(AgentUpdateInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AgentUpdateInfo>> CheckUpdate([FromQuery] string currentVersion, [FromQuery] string? serialNumber = null)
    {
        try
        {
            var serverUrl = GetConfiguredServerUrl();

            // Try centralized package service first
            var resolvedPackage = await _packageService.ResolvePackageForComputerAsync(
                serialNumber ?? "", currentVersion);

            if (resolvedPackage != null)
            {
                var updateInfo = new AgentUpdateInfo
                {
                    Version = resolvedPackage.Version,
                    DownloadUrl = $"{serverUrl}/api/packages/{resolvedPackage.Id}/download",
                    Checksum = resolvedPackage.Checksum,
                    FileSize = resolvedPackage.FileSize,
                    IsRequired = resolvedPackage.IsRequired,
                    IsBreakingChange = resolvedPackage.IsBreakingChange,
                    ReleaseNotes = resolvedPackage.ReleaseNotes ?? "",
                    ReleasedAt = resolvedPackage.PublishedAt ?? resolvedPackage.CreatedAt
                };

                // Check if this computer needs a full reinstall
                if (!string.IsNullOrEmpty(serialNumber))
                {
                    var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);
                    if (computer != null && computer.AgentStatus.ForceReinstall)
                    {
                        updateInfo.ReinstallRequired = true;
                        updateInfo.InstallScriptUrl = $"{serverUrl}/api/agent/download/install-script";
                        _logger.LogWarning("Reinstall required for {Serial} - sending ReinstallRequired=true in update info", serialNumber);

                        computer.AgentStatus.ForceReinstall = false;
                        await _computerRepository.UpdateAsync(computer);
                    }
                }

                _logger.LogInformation("Update available (package registry) for agent: {Current} -> {New}, ReinstallRequired={Reinstall}",
                    currentVersion, updateInfo.Version, updateInfo.ReinstallRequired);
                return Ok(updateInfo);
            }

            // Fallback to legacy file-based update.json
            var updatePath = _fileSettings.GetUpdatePath();
            var updateInfoPath = _fileSettings.GetUpdateJsonPath();

            if (!System.IO.File.Exists(updateInfoPath))
            {
                return NotFound(new { message = "No update available" });
            }

            var legacyInfo = UpdateJsonHelper.ParseUpdateJson(updateInfoPath);

            if (legacyInfo == null)
            {
                return NotFound(new { message = "Invalid update configuration" });
            }

            // Compare versions
            if (!string.IsNullOrEmpty(currentVersion) && !string.IsNullOrEmpty(legacyInfo.Version))
            {
                try
                {
                    var current = new Version(currentVersion);
                    var available = new Version(legacyInfo.Version);

                    if (available <= current)
                    {
                        return NotFound(new { message = "No update available", currentVersion, availableVersion = legacyInfo.Version });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing versions: current={Current}, available={Available}", 
                        currentVersion, legacyInfo.Version);
                }
            }

            legacyInfo.DownloadUrl = $"{serverUrl}/api/agent/download-update";
            
            // Check if this computer needs a full reinstall
            if (!string.IsNullOrEmpty(serialNumber))
            {
                var computer = await _computerRepository.GetBySerialNumberAsync(serialNumber);
                if (computer != null && computer.AgentStatus.ForceReinstall)
                {
                    legacyInfo.ReinstallRequired = true;
                    legacyInfo.InstallScriptUrl = $"{serverUrl}/api/agent/download/install-script";
                    _logger.LogWarning("Reinstall required for {Serial} - sending ReinstallRequired=true in update info", serialNumber);
                    
                    computer.AgentStatus.ForceReinstall = false;
                    await _computerRepository.UpdateAsync(computer);
                }
            }

            _logger.LogInformation("Update available (legacy) for agent: {Current} -> {New}, ReinstallRequired={Reinstall}", 
                currentVersion, legacyInfo.Version, legacyInfo.ReinstallRequired);
            return Ok(legacyInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for agent update");
            return StatusCode(500, new { message = "Error checking for updates" });
        }
    }

    /// <summary>
    /// Download the agent update package. Supports HTTP Range requests for download resume.
    /// If a Range request is invalid (e.g., stale partial download), strips the Range header
    /// and serves the full file to help old agents that don't handle 416.
    /// </summary>
    [HttpGet("download-update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult DownloadUpdate()
    {
        try
        {
            var updatePath = _fileSettings.GetUpdatePath();
            var packagePath = _fileSettings.GetUpdatePackagePath();

            if (!System.IO.File.Exists(packagePath))
            {
                _logger.LogWarning("Update package not found at {Path}", packagePath);
                return NotFound(new { message = "Update package not found" });
            }

            // Strip invalid Range headers to avoid 416 for agents with stale partial downloads.
            // Old agents don't handle 416 and would fail repeatedly.
            var rangeHeader = Request.Headers.Range.FirstOrDefault();
            if (!string.IsNullOrEmpty(rangeHeader))
            {
                var fileLength = new FileInfo(packagePath).Length;
                // Parse "bytes=N-" style range
                if (rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                {
                    var rangeSpec = rangeHeader["bytes=".Length..].TrimEnd();
                    var dashIdx = rangeSpec.IndexOf('-');
                    if (dashIdx > 0 && long.TryParse(rangeSpec[..dashIdx], out var rangeStart) && rangeStart >= fileLength)
                    {
                        _logger.LogWarning("Stripping invalid Range header (start={Start} >= fileSize={FileSize}). " +
                            "Client has a stale partial download.", rangeStart, fileLength);
                        Request.Headers.Remove("Range");
                    }
                }
            }

            _logger.LogInformation("Serving agent update package from {Path}", packagePath);
            
            return PhysicalFile(packagePath, "application/zip", _fileSettings.UpdatePackageFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading agent update");
            return StatusCode(500, new { message = "Error downloading update" });
        }
    }

    /// <summary>
    /// Upload a new agent update package (requires admin authorization).
    /// Saves to both the legacy file location and the centralized package registry.
    /// </summary>
    [HttpPost("upload-update")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(200_000_000)] // 200 MB hard cap for update packages
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> UploadUpdate([FromForm] AgentUpdateUploadRequest request)
    {
        try
        {
            if (request.Package == null || request.Package.Length == 0)
            {
                return BadRequest(new { message = "No package file provided" });
            }

            // Server-side size limit (200 MB)
            const long maxFileSize = 200 * 1024 * 1024;
            if (request.Package.Length > maxFileSize)
            {
                return BadRequest(new { message = $"Package exceeds maximum allowed size of {maxFileSize / (1024 * 1024)} MB" });
            }

            // File extension validation
            var extension = Path.GetExtension(request.Package.FileName);
            if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only .zip files are accepted" });
            }

            // Content-type validation
            var allowedContentTypes = new[] { "application/zip", "application/x-zip-compressed", "application/octet-stream" };
            if (!allowedContentTypes.Contains(request.Package.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid content type. Expected a ZIP file." });
            }

            // Magic bytes validation — ZIP files start with PK\x03\x04
            byte[] magicBytes = new byte[4];
            using (var headerStream = request.Package.OpenReadStream())
            {
                if (await headerStream.ReadAsync(magicBytes, 0, 4) < 4
                    || magicBytes[0] != 0x50 || magicBytes[1] != 0x4B
                    || magicBytes[2] != 0x03 || magicBytes[3] != 0x04)
                {
                    return BadRequest(new { message = "File is not a valid ZIP archive" });
                }
            }

            var updatePath = _fileSettings.GetUpdatePath();
            Directory.CreateDirectory(updatePath);

            // Save the package to legacy location
            var packagePath = _fileSettings.GetUpdatePackagePath();
            using (var stream = System.IO.File.Create(packagePath))
            {
                await request.Package.CopyToAsync(stream);
            }

            // Calculate checksum
            string checksum;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            using (var stream = System.IO.File.OpenRead(packagePath))
            {
                var hash = sha256.ComputeHash(stream);
                checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }

            // Save update info to legacy location
            var updateInfo = new AgentUpdateInfo
            {
                Version = request.Version,
                Checksum = checksum,
                FileSize = new FileInfo(packagePath).Length,
                IsRequired = request.IsRequired,
                IsBreakingChange = request.IsBreakingChange,
                ReleaseNotes = request.ReleaseNotes ?? "",
                ReleasedAt = DateTime.UtcNow
            };

            var updateInfoPath = _fileSettings.GetUpdateJsonPath();
            await UpdateJsonHelper.WriteUpdateJsonAsync(updateInfoPath, updateInfo);

            // Also register in centralized package registry
            try
            {
                using var regStream = System.IO.File.OpenRead(packagePath);
                var package = await _packageService.UploadPackageAsync(
                    regStream, request.Package.FileName, request.Version,
                    request.ReleaseNotes, request.IsRequired, request.IsBreakingChange,
                    null, "stable",
                    User.Identity?.Name ?? "Admin");
                
                // Auto-publish and activate
                await _packageService.PublishPackageAsync(package.Id);
                await _packageService.ActivatePackageAsync(package.Id);

                _logger.LogInformation("Package also registered in centralized registry: Version={Version}, Id={Id}",
                    request.Version, package.Id);
            }
            catch (Exception ex)
            {
                // Don't fail the upload if registry fails - legacy path still works
                _logger.LogWarning(ex, "Failed to register package in centralized registry (legacy upload still succeeded)");
            }

            _logger.LogInformation("Agent update uploaded: Version {Version}, Size {Size} bytes, Checksum {Checksum}",
                request.Version, updateInfo.FileSize, checksum);

            return Ok(new
            {
                message = "Update package uploaded successfully",
                version = request.Version,
                checksum,
                fileSize = updateInfo.FileSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading agent update");
            return StatusCode(500, new { message = "Error uploading update" });
        }
    }

    /// <summary>
    /// Get current update information (for admin panel)
    /// </summary>
    [HttpGet("update-info")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult GetUpdateInfo()
    {
        try
        {
            var updatePath = _fileSettings.GetUpdatePath();
            var updateInfoPath = _fileSettings.GetUpdateJsonPath();

            if (!System.IO.File.Exists(updateInfoPath))
            {
                return Ok(new { hasUpdate = false });
            }

            var updateInfo = UpdateJsonHelper.ParseUpdateJson(updateInfoPath);

            return Ok(new
            {
                hasUpdate = true,
                updateInfo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting update info");
            return StatusCode(500, new { message = "Error getting update info" });
        }
    }

    /// <summary>
    /// Delete the current update package (for admin panel)
    /// </summary>
    [HttpDelete("update")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public ActionResult DeleteUpdate()
    {
        try
        {
            var updatePath = _fileSettings.GetUpdatePath();

            if (Directory.Exists(updatePath))
            {
                Directory.Delete(updatePath, true);
                _logger.LogInformation("Agent update package deleted");
            }

            return Ok(new { message = "Update package deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting update");
            return StatusCode(500, new { message = "Error deleting update" });
        }
    }



}

public class AgentRegistrationRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string Hostname { get; set; } = string.Empty;
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [StringLength(200)]
    public string? Manufacturer { get; set; }
    [StringLength(200)]
    public string? Model { get; set; }
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    [StringLength(50)]
    public string? Type { get; set; }  // Desktop, Laptop, Tablet, Server, etc.
    [StringLength(200)]
    public string? CurrentUser { get; set; }
    public DateTime? LastBootTime { get; set; }
    public bool IsOnline { get; set; }
}

public class HeartbeatRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    [StringLength(200)]
    public string? CurrentUser { get; set; }
    public bool IsOnline { get; set; }
    [StringLength(45)]
    public string? IpAddress { get; set; }
    [StringLength(45)]
    public string? PublicIpAddress { get; set; }
    [StringLength(100)]
    public string? Hostname { get; set; }
    [StringLength(17)]
    public string? MacAddress { get; set; }
    [StringLength(200)]
    public string? OperatingSystem { get; set; }
    [StringLength(50)]
    public string? AgentVersion { get; set; }  // Thin client version
    // Extended fields for auto-registration
    [StringLength(200)]
    public string? Manufacturer { get; set; }
    [StringLength(200)]
    public string? Model { get; set; }
    [StringLength(200)]
    public string? Processor { get; set; }
    [Range(0, 10000)]
    public int? RamGB { get; set; }
    [Range(0, 100000)]
    public int? StorageGB { get; set; }
    [StringLength(50)]
    public string? Type { get; set; }  // Desktop, Laptop, Tablet, Server, etc.
    
    // GPS Location (exact coordinates from Windows Location Services on agent)
    [Range(-90, 90)]
    public double? GpsLatitude { get; set; }
    [Range(-180, 180)]
    public double? GpsLongitude { get; set; }
}

public class CheckInRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    public int? UserId { get; set; }
    [StringLength(200)]
    public string? UserName { get; set; }
    [Required, StringLength(50)]
    public string Type { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    [StringLength(2000)]
    public string? Notes { get; set; }
    [StringLength(100)]
    public string? Hostname { get; set; }
    [StringLength(45)]
    public string? IpAddress { get; set; }
}

public class AgentRegistrationResponse
{
    public bool Success { get; set; }
    public int? ComputerId { get; set; }
    public string? Message { get; set; }
    public AgentConfiguration? Configuration { get; set; }
}

public class AgentConfiguration
{
    public int HeartbeatIntervalMinutes { get; set; }
    public int CheckInIntervalMinutes { get; set; }
    public int IdleTimeoutMinutes { get; set; }
    public bool EnableAutoCheckOut { get; set; }
}

public class ComputerStatusResponse
{
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class AgentUpdateUploadRequest
{
    public IFormFile? Package { get; set; }
    [Required, StringLength(50)]
    public string Version { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsBreakingChange { get; set; }
    [StringLength(5000)]
    public string? ReleaseNotes { get; set; }
}

public class HeartbeatResponse
{
    public bool ForceUpdate { get; set; }
    public bool RequestImmediateCheckin { get; set; }
    public bool ForceReinstall { get; set; }
    public bool RequiresRegistration { get; set; }
    public int HeartbeatIntervalMinutes { get; set; } = 5;
}

public class UpdateProgressRequest
{
    [Required, StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;
    public AgentUpdateStatus Status { get; set; }
    [Range(0, 100)]
    public int Progress { get; set; }  // 0-100
    [StringLength(1000)]
    public string? Message { get; set; }
    [StringLength(50000)]
    public string? ErrorLogs { get; set; }  // Error logs from failed updates
}
