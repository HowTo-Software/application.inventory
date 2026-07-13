# Computer Inventory — Master TODO

> Last updated: March 13, 2026

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ⬜ | Not started |
| 🟡 | In progress |
| ✅ | Complete |

---
## 1. Security
- ⬜ Add Let's Encrypt integration for automated SSL/TLS certificate provisioning and renewal
- ⬜ Add CAPTCHA support (login, registration, public-facing forms) to prevent automated abuse
- ⬜ Integrate CrowdSec for collaborative intrusion detection and IP-reputation-based blocking

## 2. Testing
- ⬜ Create unit test project (`ComputerInventory.Tests`)
- ⬜ Add unit tests for core business logic (entities, services)
- ⬜ Add integration tests for API endpoints
- ⬜ Add Blazor UI smoke/component tests
- ⬜ Add test data seeders for local development

## 3. Features — Thin Client Agent
- ⬜ Implement automatic rollback on failed update (backup exists but isn't restored automatically)

## 4. Features — API / Backend
- ⬜ Move update packages from filesystem to blob storage

## 5. Infrastructure & Deployment
- ⬜ Set up CI/CD pipeline (GitHub Actions or Azure DevOps)
  - ⬜ Build & compile step
  - ⬜ Run tests
  - ⬜ Build Docker images
  - ⬜ SAST / security scan
  - ⬜ Automated deploy to staging → production
- ⬜ Create a dedicated `docker-compose.prod.yml` with externalized secrets
- ⬜ Add SSL/TLS certificate automation
- ⬜ Add database backup & disaster recovery scripts
- ⬜ Add monitoring / alerting (health checks, uptime)
- ⬜ Automate database migration application (currently manual SQL scripts)

## 6. Documentation & Knowledge
- ⬜ Document deployment runbook (step-by-step production deploy)
- ⬜ Document backup & restore procedure
- ⬜ Add architecture decision records (ADRs) for major design choices

## 11. Backend Audit — Services

### 11.3. Concurrency

- ✅ `GeoLocationService._lastRequest` read/write not atomic — race condition can exceed ip-api rate limit
- ✅ `RateLimitingMiddleware._cleanupTimer` (static) never disposed — resource leak on graceful shutdown
- ✅ `InMemoryCacheService._cleanupTimer` not disposed in finalizer path

### 11.4. Configuration

- ✅ `UpdateInfoCacheService` hardcodes `Path.Combine(Directory.GetCurrentDirectory(), "agent", "update")` — should use `IConfiguration`
- ✅ `GeoLocationService` hardcodes API endpoint URL — should be configurable for provider switching
- ✅ `DatabaseMaintenanceService` retention defaults (30 days, 100 max) have no range validation
- ✅ `Program.cs` validates only connection string — no startup validation for JWT settings, agent settings, or geo-location config
- ✅ `BackgroundTaskQueue` capacity default (100) hardcoded — should come from configuration

### 11.5. Code Quality

- ✅ `AuthService` password-verification logic duplicated in `LoginAsync` and `ChangePasswordAsync` — extract private method
- ✅ `RequestLoggingMiddleware.MaskSensitiveData()` recompiles regex patterns on every call — pre-compile as `static readonly Regex`
- ✅ No structured logging package (Serilog, etc.) — request logging uses `StringBuilder` which breaks log aggregation
- ✅ Missing Polly retry policies for transient failures (DB, HTTP)
- ✅ Missing distributed cache (Redis) — in-memory cache lost on restart and not shared across instances

## 14. Thin Client & Updater Audit

> Audited: March 13, 2026 — `ComputerInventory.ThinClient` + `ComputerInventory.ThinClient.Updater`

### 14.2. Security

- ⬜ **Remote code execution via `irm | iex` in reinstall** — both Updater's `ExecuteReinstall()` and ThinClient's `TriggerLastResortReinstallAsync()` execute `irm '<url>' | iex` (Invoke-RestMethod piped to Invoke-Expression) running as SYSTEM; if server is compromised or URL is HTTP, attacker gets full SYSTEM-level code execution on all agents — validate TLS cert, sign install script & verify signature, enforce HTTPS-only, download-then-verify-checksum before executing
- ⬜ **API key written to disk in plaintext** — `UpdateService.SaveUpdaterConfigAsync()` writes `_apiKey` in cleartext to `updater-config.json` in temp directory; any process on the machine can read it during the update window — use DPAPI or restrict file ACLs to SYSTEM only
- ⬜ **`updater-config.json` contains secrets** — Updater reads `ApiKey`, `AgentSessionId`, `ServerUrl` from file in temp directory accessible to all users — restrict permissions or encrypt sensitive fields

### 14.3. Enhancements

- ⬜ **No automatic rollback on failed update** — Updater creates backup directory (`{installPath}_backup_...`) but never restores it on failure; partial file copy leaves agent in broken state; the `catch` block attempts restart but doesn't restore backup (also tracked in §3)
- ⬜ **`GetCurrentUser()` WMI queries run every heartbeat** — `SystemInfoService` issues `Win32_LogonSession` + `ASSOCIATORS OF` queries on every call (default 5 min); cache logged-in user and only re-query on session change events or at a longer interval
- ⬜ **GPS via PowerShell process is heavyweight** — `LocationService` spawns a full PowerShell process with `System.Device.Location` assembly; consider using `Windows.Devices.Geolocation` WinRT API via C#/WinRT interop directly
- ⬜ **No integrity check on preserved `appsettings.json`** — Updater preserves existing `appsettings.json` (skips copy); if file is corrupted the agent starts with broken config indefinitely with no self-healing path
- ⬜ **`ServerConnectionManager.CreateClient()` adds headers to `DefaultRequestHeaders`** — `X-API-Key` and `X-Agent-Session-Id` added to `DefaultRequestHeaders` on factory-created clients; anti-pattern — use per-request headers instead
- ⬜ **No agent self-health-check or watchdog** — if `ExecuteAsync` loop crashes with unhandled exception (e.g. `OutOfMemoryException`), `BackgroundService` stops silently; Scheduled Task may or may not restart — add secondary watchdog process or self-check
- ⬜ **Hardcoded `C:\ProgramData\ComputerInventory` in Updater** — log directory, config paths, and other locations hardcoded; use `Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)` for consistency
- ⬜ **`DetectHardwareChangesAsync()` false positives** — `StorageGB` compared as exact `int`; disk size can fluctuate slightly between queries due to rounding, causing unnecessary server updates
- ⬜ **`IsPortInUse()` in Updater runs `netstat -an`** — string-searches entire output; slow on machines with many TCP connections — use `System.Net.NetworkInformation.IPGlobalProperties.GetActiveTcpListeners()` instead
- ⬜ **`LocalCacheService` grows unbounded** — no max-size or max-age eviction for cached data; if server is unreachable for extended periods, cache files (especially restore points with JSON blobs) consume significant disk space


## 12. Stored Procedure Conversion

> Convert all EF Core LINQ / inline database queries to stored procedures for performance, maintainability, and security.

### 12.1. Infrastructure Setup

- ✅ Create migration script (`029_AddStoredProcedures.sql`) in `server/migrations/` with all stored procedure definitions
- ✅ Add helper/extension for calling stored procedures via EF Core (`FromSqlRaw` / `ExecuteSqlRawAsync`) — standardize calling convention

### 12.2. Bulk Maintenance Operations — `DatabaseMaintenanceService.cs`

- ✅ `sp_mark_offline_computers` — replace `ExecuteUpdateAsync` (~L95) that sets `IsOnline = 0` for stale computers; params: `@offline_threshold_minutes`
- ✅ `sp_cleanup_old_system_details` — replace bulk delete (~L122); params: `@retention_days`
- ✅ `sp_cleanup_old_restore_points` — replace bulk delete (~L144); params: `@retention_days`
- ✅ `sp_cleanup_old_history_records` — replace bulk delete (~L166); params: `@retention_days`
- ✅ `sp_mark_stuck_updates_as_failed` — replace bulk update (~L173) that times out stuck updates; params: `@timeout_hours`
- ✅ `sp_cleanup_excess_records` — replace load-all anti-pattern (~L198) with `ROW_NUMBER() OVER (PARTITION BY ComputerId ORDER BY CollectedAt DESC)`; params: `@table_name`, `@max_records_per_computer`

### 12.3. Reports — `SystemDetailsController.cs`

- ✅ `sp_get_system_details_report` — replace two-step query (~L303): find latest ID per computer + 16-table join into single optimized procedure

### 12.4. Search & Geo-Location

- ✅ `sp_search_computers` — replace multi-column LIKE search in `ComputerRepository.cs` (~L99); params: `@search_term`
- ✅ `sp_get_computer_ids_needing_geo_lookup` — replace correlated subquery in `GeoLocationRepository.cs` (~L167); params: `@cutoff_hours`
- ✅ `sp_get_all_computer_locations` — replace large projection with dual GPS/IP coordinate resolution in `GeoLocationService.cs` (~L139)
- ✅ `sp_upsert_geo_location` — replace multi-save upsert in `GeoLocationRepository.UpsertAsync()` with `MERGE`; params: `@computer_id`, `@lat`, `@lon`, etc.

### 12.5. Restore Points — `RestorePointController.cs`

- ✅ `sp_get_restore_point_summary` — replace GroupBy aggregation (~L211)
- ✅ `sp_cleanup_old_restore_points_by_computer` — replace three-step keep-latest-N logic (~L282); params: `@computer_id`, `@keep_count`

### 12.6. Computer Repository — `ComputerRepository.cs`

- ✅ `sp_get_computer_by_serial_number` — replace compiled query (~L13); params: `@serial_number`
- ✅ `sp_get_total_computer_count` — replace compiled count query (~L17)
- ✅ `sp_get_status_counts` — replace GroupBy (~L157)
- ✅ `sp_delete_computer_direct` — replace `ExecuteDeleteAsync` (~L143); params: `@computer_id`
- ✅ `sp_get_all_computers` — replace `GetAllAsync()` with includes
- ✅ `sp_get_computer_by_id` — replace `GetByIdAsync()` with includes; params: `@id`
- ✅ `sp_get_computers_by_status` — params: `@status_id`
- ✅ `sp_get_computers_by_location` — params: `@location_id`
- ✅ `sp_get_computers_by_department` — params: `@department_id`

### 12.7. Lookups — `LookupsController.cs`

- ✅ `sp_get_computer_types`
- ✅ `sp_get_computer_statuses`
- ✅ `sp_get_departments`
- ✅ `sp_get_locations`
- ✅ `sp_get_manufacturers`
- ✅ `sp_get_computer_models` — params: `@manufacturer_id` (optional filter)
- ✅ `sp_get_operating_systems`
- ✅ `sp_get_location_types`
- ✅ `sp_get_department_types`
- ✅ `sp_get_manufacturer_types`

### 12.8. Reference Data — `ReferenceDataResolver.cs`

- ✅ `sp_find_or_create_manufacturer` — replace check-then-insert with `MERGE` / `INSERT ... WHERE NOT EXISTS`; params: `@name`
- ✅ `sp_find_or_create_computer_model` — params: `@manufacturer_id`, `@model_name`
- ✅ `sp_find_or_create_operating_system` — params: `@name`, `@version`, `@build_number`
- ✅ `sp_find_or_create_department` — params: `@name`
- ✅ `sp_find_or_create_location` — params: `@name`

### 12.9. Other Controllers & Services

- ✅ `sp_add_computer_history` — replace direct `_context.ComputerHistories.AddAsync()` in `AgentController.cs` (~L257, L375, L582, L628); params: `@computer_id`, `@change_type`, `@details`, `@changed_by`
- ✅ `sp_set_location_source_preference` — replace `FindAsync` + manual update in `MapController.cs` (~L155); params: `@computer_id`, `@preference`
- ✅ `sp_get_or_create_country_id` — replace race-condition-prone check-then-insert in `GeoLocationRepository`; params: `@country_code`, `@country_name`

### 12.10. Maintenance, Users, Version & Health

- ✅ `sp_get_all_maintenance_records` — replace `MaintenanceRepository.GetAllAsync()` with its 3 includes
- ✅ `sp_get_maintenance_by_computer_id` — params: `@computer_id`
- ✅ `sp_get_maintenance_by_status` — params: `@status_id`
- ✅ `sp_get_scheduled_maintenance` — params: `@start_date`, `@end_date`
- ✅ `sp_get_all_users` — replace `UserRepository.GetAllAsync()`
- ✅ `sp_get_user_by_username` — params: `@username`
- ✅ `sp_get_user_by_email` — params: `@email`
- ✅ `sp_check_user_exists` — combine `UsernameExistsAsync` + `EmailExistsAsync` into one call; params: `@username`, `@email`
- ✅ `sp_get_version_history` / `sp_get_server_history` / `sp_get_thin_client_history` — replace direct `_context` queries in `VersionController.cs`
- ✅ `sp_health_check` — replace `CanConnectAsync()` + `CountAsync()` in `HealthChecks.cs`

### 12.11. Post-Conversion Tasks

- ✅ Update `DbContext` — register stored procedure result types (`[Keyless]` entity types for report/custom shapes)
- ✅ Update all repositories — change from LINQ to `FromSqlRaw("EXEC sp_Name @param", ...)` / `ExecuteSqlRawAsync`
- ✅ Update controllers — remove direct `_context` usage and route through updated repositories
- ✅ Add integration tests — verify each stored procedure returns identical results to original LINQ query
- ✅ Update migration runner — ensure `008_AddStoredProcedures.sql` is applied alongside existing migrations
- ✅ Document all stored procedures — add reference listing procedures, parameters, and purpose to `Docs/`

---

### What's Done Well
- Auth architecture: httpOnly cookies, server-side validation, session expiration handler
- Global error boundary with auto-recovery on navigation
- Toast system with correlation IDs and ARIA live announcements
- SignalR integration for real-time agent update progress
- `ApiClient` with Polly retry pipeline and exponential backoff
- Skip-nav link and `tabindex="-1"` on main content
- SRI hashes on all CDN resources
- Role-based UI gating with `AuthorizeView Roles="Admin"`
- Dedicated `EmptyState` component with per-entity illustrations
- Full set of custom error pages (401, 403, 404, 408, 418, 500, 502, 503)

---

