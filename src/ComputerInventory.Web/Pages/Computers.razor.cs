using ComputerInventory.Contracts;
using ComputerInventory.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace ComputerInventory.Web.Pages;

public partial class Computers : IAsyncDisposable
{
    [Inject] private IComputerService ComputerService { get; set; } = default!;
    [Inject] private IVersionService VersionService { get; set; } = default!;
    [Inject] private ILookupService LookupService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private UpdateProgressHubService UpdateHub { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// Pre-computed display values for a computer row, avoiding per-render recalculation.
    /// </summary>
    private sealed class ComputerDisplayValues
    {
        public string StatusBadgeClass { get; init; } = "bg-secondary";
        public string? LastSeenTimeAgo { get; init; }
        public string? LastUpdatedTimeAgo { get; init; }
        public VersionStatusType VersionStatus { get; init; }
        public bool IsUpdateAvailable { get; init; }
    }

    private Dictionary<int, ComputerDisplayValues> _displayCache = new();

    private void BuildDisplayCache()
    {
        var cache = new Dictionary<int, ComputerDisplayValues>();
        if (computers == null) { _displayCache = cache; return; }

        foreach (var c in computers)
        {
            cache[c.Id] = new ComputerDisplayValues
            {
                StatusBadgeClass = GetStatusBadgeClass(c.Status),
                LastSeenTimeAgo = c.AgentStatus.LastSeen.HasValue ? GetTimeAgo(c.AgentStatus.LastSeen.Value) : null,
                LastUpdatedTimeAgo = c.UpdateInfo.LastUpdatedAt.HasValue ? GetTimeAgo(c.UpdateInfo.LastUpdatedAt.Value) : null,
                VersionStatus = GetAgentVersionStatus(c),
                IsUpdateAvailable = IsUpdateAvailable(c),
            };
        }
        _displayCache = cache;
    }

    private ComputerDisplayValues GetDisplay(ComputerListDto c)
        => _displayCache.TryGetValue(c.Id, out var v) ? v : new ComputerDisplayValues();

    private List<ComputerListDto>? computers;
    private bool isLoading = true;
    private bool showModal = false;
    private bool isSaving = false;
    private bool isEditing = false;
    private int editingId = 0;
    private CreateComputerRequest? editingComputer;
    private string? toastMessage;
    private string toastClass = "bg-success";
    private string? latestThinClientVersion;
    private string? errorMessage;

    // Error analysis state
    private bool showErrorAnalysisModal = false;
    private bool isLoadingAnalysis = false;
    private bool isExecutingRemediation = false;
    private ErrorAnalysisResult? errorAnalysis;
    private string? errorAnalysisHostname;
    private int? errorAnalysisComputerId;

    // Search & filter state
    private string _searchInputText = "";
    private string searchText = "";
    private System.Timers.Timer? _searchDebounce;
    private string filterStatus = "";
    private string filterType = "";
    private string filterLocation = "";
    private string filterDepartment = "";
    private string filterConnection = "";

    // Sort state
    private string _sortColumn = "Hostname";
    private bool _sortAscending = true;

    // Pagination state
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _totalCount = 0;
    private int _totalPages = 0;

    private bool HasActiveFilters =>
        !string.IsNullOrEmpty(searchText) ||
        !string.IsNullOrEmpty(filterStatus) ||
        !string.IsNullOrEmpty(filterType) ||
        !string.IsNullOrEmpty(filterLocation) ||
        !string.IsNullOrEmpty(filterDepartment) ||
        !string.IsNullOrEmpty(filterConnection);

    private void SortBy(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }
        _currentPage = 1;
        _ = LoadComputers();
    }

    private string GetSortIcon(string column)
    {
        if (_sortColumn != column) return "bi-chevron-expand";
        return _sortAscending ? "bi-sort-up" : "bi-sort-down";
    }

    private async void OnFilterChanged()
    {
        _currentPage = 1;
        await LoadComputers();
        StateHasChanged();
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _searchInputText = e.Value?.ToString() ?? "";
        _searchDebounce?.Stop();
        _searchDebounce?.Dispose();
        _searchDebounce = new System.Timers.Timer(400);
        _searchDebounce.Elapsed += async (s, args) =>
        {
            _searchDebounce?.Stop();
            await InvokeAsync(async () =>
            {
                searchText = _searchInputText;
                _currentPage = 1;
                await LoadComputers();
                StateHasChanged();
            });
        };
        _searchDebounce.AutoReset = false;
        _searchDebounce.Start();
    }

    private async Task ClearSearch()
    {
        _searchInputText = "";
        searchText = "";
        _searchDebounce?.Stop();
        _currentPage = 1;
        await LoadComputers();
    }

    private async Task ClearFilters()
    {
        _searchInputText = "";
        searchText = "";
        _searchDebounce?.Stop();
        filterStatus = "";
        filterType = "";
        filterLocation = "";
        filterDepartment = "";
        filterConnection = "";
        _currentPage = 1;
        await LoadComputers();
    }

    // Lookup data
    private List<LookupItemDto> _computerTypes = new();
    private List<LookupItemDto> _computerStatuses = new();
    private List<LookupItemDto> _departments = new();
    private List<LookupItemDto> _locations = new();
    private List<LookupItemDto> _manufacturers = new();
    private List<ModelLookupItemDto> _models = new();
    private bool _modalLookupsLoaded = false;

    // Auto-refresh state
    private System.Threading.Timer? _refreshTimer;
    private System.Threading.Timer? _countdownTimer;
    private DateTime? lastRefresh;
    private bool isRefreshing = false;
    private bool hasPendingUpdates = false;
    private int pendingUpdateCount = 0;
    private int nextRefreshIn = 0;
    private const int REFRESH_INTERVAL_PENDING = 10;  // 10 seconds when updates pending
    private const int REFRESH_INTERVAL_NORMAL = 60;   // 60 seconds normal refresh

    // Bulk operations state
    private HashSet<int> selectedIds = new();
    private string bulkStatusValue = "";
    private bool isBulkOperating = false;
    private bool showImportModal = false;
    private bool showBulkDeleteConfirm = false;
    private bool showDeleteConfirm = false;
    private int deleteTargetId;
    private string deleteTargetName = "";
    private string importCsvContent = "";
    private bool isImporting = false;
    private BulkOperationResult? importResult;

    protected override async Task OnInitializedAsync()
    {
        // Load filter lookups and version info in parallel (manufacturers/models deferred to modal open)
        var typesTask = LookupService.GetComputerTypesAsync();
        var statusesTask = LookupService.GetComputerStatusesAsync();
        var departmentsTask = LookupService.GetDepartmentsAsync();
        var locationsTask = LookupService.GetLocationsAsync();
        var versionTask = VersionService.GetCurrentThinClientVersionAsync();

        await Task.WhenAll(typesTask, statusesTask, departmentsTask, locationsTask, versionTask);

        _computerTypes = typesTask.Result;
        _computerStatuses = statusesTask.Result;
        _departments = departmentsTask.Result;
        _locations = locationsTask.Result;
        latestThinClientVersion = versionTask.Result.Success ? versionTask.Result.Data?.Version : null;

        await LoadComputers();
        StartAutoRefresh();

        // Connect to SignalR for real-time update progress
        UpdateHub.OnUpdateProgress += HandleUpdateProgress;
        await UpdateHub.StartAsync();
    }

    private void StartAutoRefresh()
    {
        // Main refresh timer
        _refreshTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await RefreshDataSilently();
            });
        }, null, TimeSpan.FromSeconds(GetRefreshInterval()), TimeSpan.FromSeconds(GetRefreshInterval()));

        // Countdown timer (updates every second)
        nextRefreshIn = GetRefreshInterval();
        _countdownTimer = new System.Threading.Timer(_ =>
        {
            InvokeAsync(() =>
            {
                if (nextRefreshIn > 0)
                {
                    nextRefreshIn--;
                }
                else
                {
                    nextRefreshIn = GetRefreshInterval();
                }
                StateHasChanged();
            });
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private int GetRefreshInterval()
    {
        return hasPendingUpdates ? REFRESH_INTERVAL_PENDING : REFRESH_INTERVAL_NORMAL;
    }

    private async Task RefreshDataSilently()
    {
        if (isRefreshing) return;

        isRefreshing = true;
        try
        {
            var oldPendingIds = computers?.Where(c => c.UpdateInfo.ForceUpdate).Select(c => c.Id).ToHashSet() ?? new HashSet<int>();

            var result = await ComputerService.GetPagedAsync(
                _currentPage, _pageSize, 
                string.IsNullOrWhiteSpace(searchText) ? null : searchText,
                string.IsNullOrEmpty(filterStatus) ? null : filterStatus,
                string.IsNullOrEmpty(filterType) ? null : filterType,
                string.IsNullOrEmpty(filterLocation) ? null : filterLocation,
                string.IsNullOrEmpty(filterDepartment) ? null : filterDepartment,
                string.IsNullOrEmpty(filterConnection) ? null : filterConnection,
                _sortColumn, _sortAscending ? "asc" : "desc");
            if (result.Success && result.Data != null)
            {
                computers = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _totalPages = result.Data.TotalPages;
                lastRefresh = DateTime.UtcNow;
                errorMessage = null;
                BuildDisplayCache();

                // Check for completed updates
                var completedUpdates = oldPendingIds
                    .Where(id => computers?.FirstOrDefault(c => c.Id == id) is { } comp && !comp.UpdateInfo.ForceUpdate)
                    .Select(id => computers?.FirstOrDefault(c => c.Id == id)?.Hostname)
                    .Where(h => h != null)
                    .ToList();

                if (completedUpdates.Any())
                {
                    toastClass = "bg-success";
                    toastMessage = $"Update completed: {string.Join(", ", completedUpdates)}";

                    // Auto-hide success toast
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(5000);
                        await InvokeAsync(() =>
                        {
                            if (toastMessage?.StartsWith("Update completed") == true)
                            {
                                toastMessage = null;
                                StateHasChanged();
                            }
                        });
                    });
                }

                // Update pending state
                UpdatePendingState();

                // Adjust timer interval based on pending updates
                AdjustRefreshInterval();
            }
            // Silently fail on background refresh - don't update errorMessage

            StateHasChanged();
        }
        catch
        {
            // Silently fail on background refresh
        }
        finally
        {
            isRefreshing = false;
        }
    }

    private void UpdatePendingState()
    {
        var pending = computers?.Where(c => c.UpdateInfo.ForceUpdate).ToList() ?? new List<ComputerListDto>();
        hasPendingUpdates = pending.Any();
        pendingUpdateCount = pending.Count;
    }

    private void AdjustRefreshInterval()
    {
        var newInterval = GetRefreshInterval();
        nextRefreshIn = newInterval;
        _refreshTimer?.Change(TimeSpan.FromSeconds(newInterval), TimeSpan.FromSeconds(newInterval));
    }

    private async Task ManualRefresh()
    {
        await RefreshDataSilently();
        nextRefreshIn = GetRefreshInterval();
    }

    private async Task LoadComputers()
    {
        isLoading = true;
        try
        {
            var result = await ComputerService.GetPagedAsync(
                _currentPage, _pageSize,
                string.IsNullOrWhiteSpace(searchText) ? null : searchText,
                string.IsNullOrEmpty(filterStatus) ? null : filterStatus,
                string.IsNullOrEmpty(filterType) ? null : filterType,
                string.IsNullOrEmpty(filterLocation) ? null : filterLocation,
                string.IsNullOrEmpty(filterDepartment) ? null : filterDepartment,
                string.IsNullOrEmpty(filterConnection) ? null : filterConnection,
                _sortColumn, _sortAscending ? "asc" : "desc");
            if (result.Success && result.Data != null)
            {
                computers = result.Data.Items;
                _totalCount = result.Data.TotalCount;
                _totalPages = result.Data.TotalPages;
                lastRefresh = DateTime.UtcNow;
                errorMessage = null;
            }
            else
            {
                computers = new List<ComputerListDto>();
                _totalCount = 0;
                _totalPages = 0;
                errorMessage = result.ErrorMessage ?? "Failed to load computers";
            }
            BuildDisplayCache();
            UpdatePendingState();
        }
        catch (Exception ex)
        {
            computers = new List<ComputerListDto>();
            _totalCount = 0;
            _totalPages = 0;
            errorMessage = $"Error loading computers: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task ForceUpdateComputer(ComputerListDto computer)
    {
        var result = await ComputerService.ForceUpdateAsync(computer.Id);
        if (result.Success)
        {
            toastClass = "bg-success";
            toastMessage = $"{computer.Hostname}: {result.Data?.Message ?? "Update signal sent"}";
            await LoadComputers();
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = $"{computer.Hostname}: {result.ErrorMessage ?? "Failed to send update signal"}";
        }

        StateHasChanged();

        // Auto-hide toast after 5 seconds
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            await InvokeAsync(() =>
            {
                toastMessage = null;
                StateHasChanged();
            });
        });
    }

    private async Task EnsureModalLookupsLoadedAsync()
    {
        if (_modalLookupsLoaded) return;

        var manufacturersTask = LookupService.GetManufacturersAsync();
        var modelsTask = LookupService.GetModelsAsync();
        await Task.WhenAll(manufacturersTask, modelsTask);

        _manufacturers = manufacturersTask.Result;
        _models = modelsTask.Result;
        _modalLookupsLoaded = true;
    }

    private async Task ShowAddModal()
    {
        editingComputer = new CreateComputerRequest { Status = ComputerStatusNames.Available, Type = "Desktop" };
        isEditing = false;
        editingId = 0;
        await EnsureModalLookupsLoadedAsync();
        showModal = true;
    }

    private void NavigateToReports(int computerId)
    {
        NavigationManager.NavigateTo($"/reports?computerId={computerId}");
    }

    private async Task EditComputer(ComputerListDto computer)
    {
        // Fetch full detail to get fields not in the list DTO (Notes, etc.)
        var detail = await ComputerService.GetByIdAsync(computer.Id);
        var full = detail.Data;
        editingComputer = new CreateComputerRequest
        {
            SerialNumber = full?.SerialNumber ?? computer.SerialNumber,
            Hostname = full?.Hostname ?? computer.Hostname,
            Manufacturer = full?.Manufacturer ?? computer.Manufacturer,
            Model = full?.Model ?? computer.Model,
            Type = full?.Type ?? computer.Type,
            Status = full?.Status ?? computer.Status,
            Location = full?.Location ?? computer.Location,
            Department = full?.Department ?? computer.Department,
            Notes = full?.Notes,
            AssignedTo = full?.AssignedTo ?? computer.AssignedTo,
            HeartbeatIntervalMinutes = full?.AgentStatus.HeartbeatIntervalMinutes ?? computer.AgentStatus.HeartbeatIntervalMinutes
        };
        isEditing = true;
        editingId = computer.Id;
        await EnsureModalLookupsLoadedAsync();
        showModal = true;
    }

    private void CloseModal()
    {
        showModal = false;
        editingComputer = null;
        isEditing = false;
        editingId = 0;
    }

    private async Task SaveComputer()
    {
        if (editingComputer == null) return;

        isSaving = true;
        try
        {
            if (isEditing && editingId > 0)
            {
                var result = await ComputerService.UpdateAsync(editingId, editingComputer);
                if (!result.Success)
                {
                    toastClass = "bg-danger";
                    toastMessage = result.ErrorMessage ?? "Failed to update computer";
                    return;
                }
            }
            else
            {
                var result = await ComputerService.CreateAsync(editingComputer);
                if (!result.Success)
                {
                    toastClass = "bg-danger";
                    toastMessage = result.ErrorMessage ?? "Failed to create computer";
                    return;
                }
            }
            CloseModal();
            await LoadComputers();
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ShowDeleteConfirm(int id, string hostname)
    {
        deleteTargetId = id;
        deleteTargetName = string.IsNullOrEmpty(hostname) ? $"Computer #{id}" : hostname;
        showDeleteConfirm = true;
    }

    private async Task ConfirmDeleteComputer()
    {
        showDeleteConfirm = false;
        var result = await ComputerService.DeleteAsync(deleteTargetId);
        if (!result.Success)
        {
            toastClass = "bg-danger";
            toastMessage = result.ErrorMessage ?? "Failed to delete computer";
        }
        await LoadComputers();
    }

    // --- Bulk Operations ---

    private void ToggleSelection(int id)
    {
        if (!selectedIds.Remove(id))
            selectedIds.Add(id);
    }

    private void ToggleSelectAll(ChangeEventArgs e)
    {
        if (computers == null || computers.Count == 0) return;
        if (IsAllSelected())
            selectedIds.Clear();
        else
            selectedIds = computers.Select(c => c.Id).ToHashSet();
    }

    private bool IsAllSelected()
    {
        return computers != null && computers.Count > 0 && computers.All(c => selectedIds.Contains(c.Id));
    }

    private void ClearSelection()
    {
        selectedIds.Clear();
        bulkStatusValue = "";
    }

    private async Task BulkUpdateStatus()
    {
        if (string.IsNullOrEmpty(bulkStatusValue) || selectedIds.Count == 0) return;

        isBulkOperating = true;
        try
        {
            var result = await ComputerService.BulkUpdateStatusAsync(selectedIds.ToList(), bulkStatusValue);
            if (result.Success && result.Data != null)
            {
                toastClass = "bg-success";
                toastMessage = $"Status updated: {result.Data.SuccessCount} succeeded, {result.Data.FailureCount} failed.";
                ClearSelection();
                await LoadComputers();
            }
            else
            {
                toastClass = "bg-danger";
                toastMessage = result.ErrorMessage ?? "Bulk status update failed.";
            }
        }
        finally
        {
            isBulkOperating = false;
        }
        _ = AutoHideToast();
    }

    private void ShowBulkDeleteConfirm()
    {
        showBulkDeleteConfirm = true;
    }

    private async Task BulkDelete()
    {
        if (selectedIds.Count == 0) return;

        isBulkOperating = true;
        try
        {
            var result = await ComputerService.BulkDeleteAsync(selectedIds.ToList());
            if (result.Success && result.Data != null)
            {
                toastClass = "bg-success";
                toastMessage = $"Deleted: {result.Data.SuccessCount} succeeded, {result.Data.FailureCount} failed.";
                ClearSelection();
                await LoadComputers();
            }
            else
            {
                toastClass = "bg-danger";
                toastMessage = result.ErrorMessage ?? "Bulk delete failed.";
            }
        }
        finally
        {
            isBulkOperating = false;
            showBulkDeleteConfirm = false;
        }
        _ = AutoHideToast();
    }

    private void ShowImportModal()
    {
        importCsvContent = "";
        importResult = null;
        showImportModal = true;
    }

    private void CloseImportModal()
    {
        showImportModal = false;
        importCsvContent = "";
        importResult = null;
    }

    private async Task ExecuteImport()
    {
        if (string.IsNullOrWhiteSpace(importCsvContent)) return;

        isImporting = true;
        importResult = null;
        try
        {
            var result = await ComputerService.ImportCsvAsync(importCsvContent);
            if (result.Success && result.Data != null)
            {
                importResult = result.Data;
                if (result.Data.SuccessCount > 0)
                {
                    await LoadComputers();
                }
            }
            else
            {
                importResult = new BulkOperationResult
                {
                    FailureCount = 1,
                    Errors = new List<string> { result.ErrorMessage ?? "Import failed." }
                };
            }
        }
        catch (Exception ex)
        {
            importResult = new BulkOperationResult
            {
                FailureCount = 1,
                Errors = new List<string> { ex.Message }
            };
        }
        finally
        {
            isImporting = false;
        }
    }

    private string GetStatusBadgeClass(string status) => status switch
    {
        ComputerStatusNames.Available => "bg-success",
        ComputerStatusNames.Assigned => "bg-primary",
        ComputerStatusNames.InMaintenance => "bg-warning text-dark",
        ComputerStatusNames.Retired => "bg-secondary",
        ComputerStatusNames.Lost => "bg-danger",
        ComputerStatusNames.Stolen => "bg-danger",
        ComputerStatusNames.Disposed => "bg-dark",
        _ => "bg-secondary"
    };

    private string GetTimeAgo(DateTime dateTime)
    {
        var now = DateTime.UtcNow;
        var diff = now - dateTime;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dateTime.ToShortDateString();
    }

    private string GetElapsedTime(DateTime startTime)
    {
        var elapsed = DateTime.UtcNow - startTime;
        if (elapsed.TotalSeconds < 60) return $"Waiting {(int)elapsed.TotalSeconds}s";
        if (elapsed.TotalMinutes < 60) return $"Waiting {(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        return $"Waiting {(int)elapsed.TotalHours}h {elapsed.Minutes}m";
    }

    private string? GetNextHeartbeatTime(DateTime? lastSeen, int intervalMinutes = 5)
    {
        if (!lastSeen.HasValue) return null;

        var interval = Math.Max(1, intervalMinutes);
        var nextHeartbeat = lastSeen.Value.AddMinutes(interval);
        var now = DateTime.UtcNow;

        if (nextHeartbeat <= now)
        {
            // Already overdue, estimate next one
            var minutesSinceLast = (now - lastSeen.Value).TotalMinutes;
            var cyclesPassed = (int)(minutesSinceLast / interval);
            nextHeartbeat = lastSeen.Value.AddMinutes((cyclesPassed + 1) * interval);
        }

        var timeUntil = nextHeartbeat - now;
        if (timeUntil.TotalSeconds < 60) return $"~{(int)timeUntil.TotalSeconds}s";
        if (timeUntil.TotalMinutes < 60) return $"~{(int)timeUntil.TotalMinutes}m";
        return null; // Too far out to estimate
    }

    private async Task CancelUpdate(ComputerListDto computer)
    {
        var result = await ComputerService.CancelUpdateAsync(computer.Id);
        if (result.Success)
        {
            toastClass = "bg-info";
            toastMessage = $"{computer.Hostname}: {result.Data?.Message ?? "Update cancelled"}";
            await LoadComputers();
            AdjustRefreshInterval();
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = $"{computer.Hostname}: {result.ErrorMessage ?? "Failed to cancel update"}";
        }

        StateHasChanged();

        // Auto-hide toast
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            await InvokeAsync(() =>
            {
                toastMessage = null;
                StateHasChanged();
            });
        });
    }

    private async Task OverrideUpdate(ComputerListDto computer)
    {
        var result = await ComputerService.OverrideUpdateAsync(computer.Id, "Manual override — update and reinstall both failed");
        if (result.Success)
        {
            toastClass = "bg-warning";
            toastMessage = $"{computer.Hostname}: {result.Data?.Message ?? "Manual override applied"}";
            await LoadComputers();
            AdjustRefreshInterval();
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = $"{computer.Hostname}: {result.ErrorMessage ?? "Failed to override update"}";
        }

        StateHasChanged();
        _ = AutoHideToast();
    }

    private async Task RequestImmediateCheckin(ComputerListDto computer)
    {
        var result = await ComputerService.RequestImmediateCheckinAsync(computer.Id);
        if (result.Success)
        {
            toastClass = "bg-info";
            toastMessage = $"{computer.Hostname}: {result.Data?.Message ?? "Check-in request sent"}";
            // Set faster refresh to catch the immediate check-in
            AdjustRefreshInterval();
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = $"{computer.Hostname}: {result.ErrorMessage ?? "Failed to request check-in"}";
        }

        StateHasChanged();

        // Auto-hide toast
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            await InvokeAsync(() =>
            {
                toastMessage = null;
                StateHasChanged();
            });
        });
    }

    /// <summary>
    /// Get human-readable label for update status
    /// </summary>
    private string GetUpdateStatusLabel(int status)
    {
        return status switch
        {
            0 => "Ready",           // None
            1 => "Pending",         // Pending
            2 => "Checking",        // CheckingForUpdate
            3 => "Downloading",     // Downloading
            4 => "Verifying",       // Verifying
            5 => "Extracting",      // Extracting
            6 => "Preparing",       // PreparingUpdater
            7 => "Stopping",        // StoppingService
            8 => "Backing up",      // BackingUp
            9 => "Copying",         // CopyingFiles
            10 => "Starting",       // StartingService
            11 => "Complete",       // Completed
            99 => "Failed",         // Failed
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Truncate message for display
    /// </summary>
    private string? TruncateMessage(string? message, int maxLength)
    {
        if (string.IsNullOrEmpty(message)) return null;
        if (message.Length <= maxLength) return message;
        return message.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Enum to represent the agent version status for display purposes
    /// </summary>
    private enum VersionStatusType
    {
        Unknown,        // Cannot determine status (no latest version info or parse error)
        Current,        // Agent is running the latest version
        Outdated,       // Agent is running an older version, update available
        RecentlyUpdated // Agent was recently updated (within 24 hours)
    }

    /// <summary>
    /// Determine the version status of a computer's agent for display
    /// </summary>
    private VersionStatusType GetAgentVersionStatus(ComputerListDto computer)
    {
        // If no agent version reported, we can't determine status
        if (string.IsNullOrEmpty(computer.AgentStatus.AgentVersion))
            return VersionStatusType.Unknown;

        // Check if recently updated (within 24 hours) - this takes priority
        if (computer.UpdateInfo.LastUpdatedAt.HasValue &&
            computer.UpdateInfo.LastUpdatedAt.Value > DateTime.UtcNow.AddHours(-24) &&
            !string.IsNullOrEmpty(computer.UpdateInfo.PreviousAgentVersion))
        {
            return VersionStatusType.RecentlyUpdated;
        }

        // If we don't know the latest version, we can't compare
        if (string.IsNullOrEmpty(latestThinClientVersion))
            return VersionStatusType.Unknown;

        // Try to parse and compare versions
        if (Version.TryParse(computer.AgentStatus.AgentVersion, out var computerVersion) &&
            Version.TryParse(latestThinClientVersion, out var latestVersion))
        {
            if (computerVersion >= latestVersion)
                return VersionStatusType.Current;
            else
                return VersionStatusType.Outdated;
        }

        // Fallback: string comparison if parsing fails
        if (computer.AgentStatus.AgentVersion == latestThinClientVersion)
            return VersionStatusType.Current;
        else if (!string.IsNullOrEmpty(latestThinClientVersion))
            return VersionStatusType.Outdated;

        return VersionStatusType.Unknown;
    }

    /// <summary>
    /// Check if an update is available for a computer (its version is older than the latest)
    /// </summary>
    private bool IsUpdateAvailable(ComputerListDto computer)
    {
        // No update check possible if we don't know the latest version
        if (string.IsNullOrEmpty(latestThinClientVersion))
            return false;

        // No update check possible if the computer doesn't have an agent or we don't know its version
        if (string.IsNullOrEmpty(computer.AgentStatus.AgentVersion))
            return false;

        // Compare versions
        if (Version.TryParse(computer.AgentStatus.AgentVersion, out var computerVersion) &&
            Version.TryParse(latestThinClientVersion, out var latestVersion))
        {
            return computerVersion < latestVersion;
        }

        // Fallback: string comparison if parsing fails
        return computer.AgentStatus.AgentVersion != latestThinClientVersion;
    }

    /// <summary>
    /// Check if an update is already in progress or about to be (auto-update or force update)
    /// </summary>
    private bool IsUpdateInProgressOrPending(ComputerListDto computer)
    {
        // Check if there's an active update status (1=Pending to 10=StartingService)
        if (computer.UpdateInfo.UpdateStatus > 0 && computer.UpdateInfo.UpdateStatus < 11)
            return true;

        // Check if force update is already requested
        if (computer.UpdateInfo.ForceUpdate)
            return true;

        return false;
    }

    // --- Pagination ---

    private async Task GoToPage(int page)
    {
        if (page < 1 || page > _totalPages || page == _currentPage) return;
        _currentPage = page;
        selectedIds.Clear();
        await LoadComputers();
    }

    private async Task OnPageSizeChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newSize) && newSize > 0)
        {
            _pageSize = newSize;
            _currentPage = 1;
            selectedIds.Clear();
            await LoadComputers();
        }
    }

    private List<int> GetPageNumbers()
    {
        var pages = new List<int>();
        if (_totalPages <= 7)
        {
            for (var i = 1; i <= _totalPages; i++) pages.Add(i);
            return pages;
        }

        pages.Add(1);
        if (_currentPage > 3) pages.Add(-1); // ellipsis

        var start = Math.Max(2, _currentPage - 1);
        var end = Math.Min(_totalPages - 1, _currentPage + 1);
        for (var i = start; i <= end; i++) pages.Add(i);

        if (_currentPage < _totalPages - 2) pages.Add(-1); // ellipsis
        pages.Add(_totalPages);

        return pages;
    }

    public async ValueTask DisposeAsync()
    {
        _searchDebounce?.Dispose();
        _refreshTimer?.Dispose();
        _countdownTimer?.Dispose();
        UpdateHub.OnUpdateProgress -= HandleUpdateProgress;
        await UpdateHub.StopAsync();
    }

    private void HandleUpdateProgress(UpdateProgressMessage message)
    {
        InvokeAsync(() =>
        {
            if (computers == null) return;

            var computer = computers.FirstOrDefault(c => c.Id == message.ComputerId);
            if (computer == null) return;

            computer.UpdateInfo.UpdateStatus = message.UpdateStatus;
            computer.UpdateInfo.UpdateProgress = message.UpdateProgress;
            computer.UpdateInfo.UpdateStatusMessage = message.UpdateStatusMessage;
            computer.UpdateInfo.UpdateStartedAt = message.UpdateStartedAt;
            computer.UpdateInfo.HasUpdateErrorLogs = message.HasUpdateErrorLogs;
            computer.UpdateInfo.ForceUpdate = message.ForceUpdate;
            computer.UpdateInfo.ReinstallAttempted = message.ReinstallAttempted;

            // Detect completion/failure for toast
            if (message.UpdateStatus == (int)AgentUpdateStatus.Completed)
            {
                toastClass = "bg-success";
                toastMessage = $"Update completed: {message.Hostname}";
                _ = AutoHideToast();
            }
            else if (message.UpdateStatus == (int)AgentUpdateStatus.Failed)
            {
                toastClass = "bg-danger";
                toastMessage = $"Update failed: {message.Hostname}";
                _ = AutoHideToast();
            }

            BuildDisplayCache();
            UpdatePendingState();
            AdjustRefreshInterval();
            StateHasChanged();
        });
    }

    private async Task AutoHideToast()
    {
        await Task.Delay(5000);
        await InvokeAsync(() =>
        {
            toastMessage = null;
            StateHasChanged();
        });
    }

    private async Task ShowErrorAnalysis(ComputerListDto computer)
    {
        errorAnalysis = null;
        errorAnalysisHostname = computer.Hostname;
        errorAnalysisComputerId = computer.Id;
        showErrorAnalysisModal = true;
        isLoadingAnalysis = true;
        StateHasChanged();

        var result = await ComputerService.GetErrorAnalysisAsync(computer.Id);
        isLoadingAnalysis = false;
        if (result.Success)
        {
            errorAnalysis = result.Data;
        }
        StateHasChanged();
    }

    private async Task ExecuteRemediationAction(int computerId, string action)
    {
        isExecutingRemediation = true;
        StateHasChanged();

        try
        {
            switch (action)
            {
                case "force-update":
                    var updateResult = await ComputerService.ForceUpdateAsync(computerId);
                    toastClass = updateResult.Success ? "bg-success" : "bg-danger";
                    toastMessage = updateResult.Success
                        ? "Update retry initiated."
                        : updateResult.ErrorMessage ?? "Failed to initiate update.";
                    break;

                case "force-reinstall":
                    // Reinstall is a separate endpoint — call via ApiClient directly
                    toastClass = "bg-warning";
                    toastMessage = "Reinstall requested. Use the Actions menu to confirm reinstall for this computer.";
                    break;

                case "manual-override":
                    var overrideResult = await ComputerService.OverrideUpdateAsync(computerId, "Manual override via error analysis");
                    toastClass = overrideResult.Success ? "bg-warning" : "bg-danger";
                    toastMessage = overrideResult.Success
                        ? "Manual override applied. Update state cleared."
                        : overrideResult.ErrorMessage ?? "Failed to apply manual override.";
                    break;
            }

            showErrorAnalysisModal = false;
            _ = AutoHideToast();
            await LoadComputers();
        }
        finally
        {
            isExecutingRemediation = false;
            StateHasChanged();
        }
    }

    private static string GetSeverityAlertClass(string severity) => severity switch
    {
        "Critical" => "alert-danger",
        "High" => "alert-warning",
        "Medium" => "alert-info",
        "Low" => "alert-secondary",
        _ => "alert-info"
    };

    private static string GetSeverityIcon(string severity) => severity switch
    {
        "Critical" => "bi-x-octagon-fill",
        "High" => "bi-exclamation-triangle-fill",
        "Medium" => "bi-info-circle-fill",
        "Low" => "bi-check-circle-fill",
        _ => "bi-info-circle-fill"
    };

    private static string GetSeverityBadgeClass(string severity) => severity switch
    {
        "Critical" => "bg-danger",
        "High" => "bg-warning text-dark",
        "Medium" => "bg-info",
        "Low" => "bg-secondary",
        _ => "bg-secondary"
    };

    private static string GetCategoryBadgeClass(string category) => category switch
    {
        "Network" => "bg-primary",
        "FileSystem" => "bg-dark",
        "Permission" => "bg-danger",
        "Service" => "bg-warning text-dark",
        "Package" => "bg-info",
        "Configuration" => "bg-secondary",
        "Resource" => "bg-danger",
        _ => "bg-secondary"
    };

    private async Task CopyToClipboard(string text)
    {
        try
        {
            await JS.InvokeVoidAsync("copyToClipboard", text);
            toastClass = "bg-success";
            toastMessage = "Copied to clipboard";
        }
        catch (Exception)
        {
            toastClass = "bg-danger";
            toastMessage = "Failed to copy to clipboard";
        }
        StateHasChanged();
        _ = AutoHideToast();
    }

    private async Task DownloadErrorLogs(ComputerListDto computer)
    {
        var result = await ComputerService.DownloadErrorLogsAsync(computer.Id);
        if (result.Success && result.Data != null)
        {
            var fileName = $"{computer.Hostname}_error_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "text/plain", result.Data);
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = $"{computer.Hostname}: {result.ErrorMessage ?? "Failed to download error logs"}";
            StateHasChanged();
            _ = AutoHideToast();
        }
    }

    private async Task DownloadErrorLogsFromModal()
    {
        if (!errorAnalysisComputerId.HasValue) return;
        var result = await ComputerService.GetByIdAsync(errorAnalysisComputerId.Value);
        if (result.Success && result.Data != null)
        {
            // Create a minimal ComputerListDto for the download method
            var dto = new ComputerListDto { Id = result.Data.Id, Hostname = result.Data.Hostname };
            await DownloadErrorLogs(dto);
        }
    }

    private async Task DownloadExportCsv()
    {
        var result = await ComputerService.DownloadExportAsync();
        if (result.Success && result.Data != null)
        {
            var fileName = $"computers_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            await JS.InvokeVoidAsync("downloadFileFromBytes", fileName, "text/csv", result.Data);
        }
        else
        {
            toastClass = "bg-danger";
            toastMessage = result.ErrorMessage ?? "Failed to download export";
            StateHasChanged();
            _ = AutoHideToast();
        }
    }
}
