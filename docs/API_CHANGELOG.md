# Computer Inventory API — Changelog & Versioning Notes

> Maintained since: December 2025
> Last updated: March 11, 2026

---

## Table of Contents

- [Versioning Strategy](#versioning-strategy)
- [API Overview](#api-overview)
- [Changelog](#changelog)
  - [v1.5.0 — March 10, 2026](#v150--march-10-2026)
  - [v1.4.0 — March 9–10, 2026](#v140--march-910-2026)
  - [v1.3.0 — February 17, 2026](#v130--february-17-2026)
  - [v1.2.0 — January 6, 2026](#v120--january-6-2026)
  - [v1.1.0 — December 31, 2025](#v110--december-31-2025)
  - [v1.0.0 — December 30, 2025](#v100--december-30-2025)
- [Endpoint Reference](#endpoint-reference)
- [Authentication](#authentication)
- [Database Migrations](#database-migrations)

---

## Versioning Strategy

| Aspect | Detail |
|--------|--------|
| **API prefix** | All endpoints use `/api/` prefix |
| **Swagger version** | `v1` (single version, no URI-based versioning) |
| **Server version header** | `X-Server-Version: 1.0.0` (via `RequestTimingMiddleware`) |
| **Framework** | ASP.NET Core 8.0 (.NET SDK 9.0.308) |
| **Database** | SQL Server 2025, migrations applied manually via numbered SQL scripts |
| **Breaking changes** | Documented with ⚠️ icon below |

The API currently follows a single-version strategy. All endpoints live under `/api/{controller}` with no version segment. Swagger UI is available at `/swagger` in all environments.

Version history is tracked internally via `api/version/history` and recorded automatically during deployments via `POST /api/version/record`.

---

## API Overview

| Metric | Value |
|--------|-------|
| **Total endpoints** | ~129 |
| **Controllers** | 18 |
| **Auth mechanisms** | JWT Bearer (cookie + header), API Key (`X-API-Key`) |
| **Real-time** | SignalR hub at `/hubs/update-progress` |
| **Health checks** | `/health/live`, `/health/ready`, `/api/health/detailed` |

---

## Changelog

### v1.5.0 — March 10, 2026

**Package Versioning & Distribution System**

A major new feature introducing centralized package lifecycle management for agent updates, replacing the legacy filesystem-based update mechanism.

#### New: PackagesController (`api/packages`) — 14 endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/packages` | AdminOnly | List all package versions |
| GET | `/api/packages/{id}` | AdminOnly | Get package with distribution rules |
| POST | `/api/packages/upload` | AdminOnly | Upload new package (500 MB limit) |
| PUT | `/api/packages/{id}` | AdminOnly | Update package metadata |
| DELETE | `/api/packages/{id}` | AdminOnly | Delete package (Draft/Archived only) |
| POST | `/api/packages/{id}/publish` | AdminOnly | Publish a draft package |
| POST | `/api/packages/{id}/activate` | AdminOnly | Activate (becomes distributed version) |
| POST | `/api/packages/{id}/deprecate` | AdminOnly | Deprecate package |
| POST | `/api/packages/{id}/rollback` | AdminOnly | Rollback to previous active version |
| POST | `/api/packages/{id}/rules` | AdminOnly | Add distribution rule |
| DELETE | `/api/packages/{id}/rules/{ruleId}` | AdminOnly | Remove distribution rule |
| GET | `/api/packages/resolve` | API Key | Resolve applicable package for agent |
| GET | `/api/packages/{id}/download` | None | Download package zip (range requests) |
| GET | `/api/packages/distribution-summary` | AdminOnly | Fleet distribution summary |

- **Package lifecycle**: Draft → Published → Active → Deprecated → Archived
- **Distribution rules**: AllComputers, BySerialNumber, ByDepartment, ByLocation
- **Migration**: `009_AddPackageVersioning.sql` — `package_versions` and `package_distribution_rules` tables

#### New: Refresh Token Authentication

- `POST /api/auth/refresh` — Silent JWT renewal using httpOnly `refresh_token` cookie
- `POST /api/auth/logout` — Revokes refresh token family and clears cookies
- Refresh tokens use SHA-256 hashing and family-based rotation for replay detection
- **Migration**: `010_AddRefreshTokens.sql` — `refresh_tokens` table with token family support

#### New: Per-Computer Heartbeat Interval

- Heartbeat endpoint now returns `heartbeatIntervalMinutes` from per-computer configuration
- **Migration**: `008_AddHeartbeatIntervalPerComputer.sql` — `heartbeat_interval_minutes` column (default: 5)

#### New: AuditLogController (`api/auditlog`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/auditlog` | AdminOnly | Paged audit log with filters (action, search, date range) |

#### New: Department & Location Management Controllers

| Controller | Endpoints | Auth | Operations |
|------------|-----------|------|------------|
| DepartmentsController (`api/departments`) | 5 | AdminOnly | Full CRUD with usage counts, delete protection |
| LocationsController (`api/locations`) | 5 | AdminOnly | Full CRUD with usage counts, delete protection |

#### Changed: AgentController

- `GET /api/agent/check-update` — Now uses `PackageService` for version resolution with legacy `update.json` fallback
- Agent heartbeat returns per-computer heartbeat interval from database

#### Changed: AuthController

- Added `POST /api/auth/refresh` and `POST /api/auth/logout` endpoints
- Login now issues refresh token alongside JWT access token

---

### v1.4.0 — March 9–10, 2026

**Admin Dashboard, Setup Wizard, and Enhanced Computer Management**

#### New: DashboardController (`api/dashboard`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/dashboard` | AdminOnly | Aggregated dashboard (cached 15s): inventory stats, agent health, maintenance metrics, cache utilization, environment info |

#### New: SetupController (`api/setup`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/setup/status` | Anonymous | Check if initial setup is required |
| POST | `/api/setup/initialize` | Anonymous | Create initial admin account (first-run only, single use) |

#### Changed: ComputersController — New Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/computers/export` | TechnicianOrAdmin | Export all computers as CSV |
| POST | `/api/computers/import` | TechnicianOrAdmin | Import computers from CSV |
| POST | `/api/computers/bulk-status` | TechnicianOrAdmin | Batch status update (max 500) |
| POST | `/api/computers/bulk-delete` | AdminOnly | Batch delete (max 500) |
| PATCH | `/api/computers/{id}/status` | TechnicianOrAdmin | Update status only |
| PATCH | `/api/computers/{id}/assign` | TechnicianOrAdmin | Assign/unassign user text |
| POST | `/api/computers/{id}/force-reinstall` | Admin (role) | Full agent reinstall |
| POST | `/api/computers/{id}/request-checkin` | TechnicianOrAdmin | Request immediate check-in |

#### Changed: UsersController — New Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| PATCH | `/api/users/{id}/activate` | AdminOnly | Activate user account |
| PATCH | `/api/users/{id}/deactivate` | AdminOnly | Deactivate user account |
| PATCH | `/api/users/{id}/reset-password` | AdminOnly | Admin password reset |

#### Changed: MaintenanceController

- Added `GET /api/maintenance/paged` — Paginated maintenance records (`?page=&pageSize=`)
- Added `PATCH /api/maintenance/{id}/complete` — Mark maintenance complete

#### Changed: VersionController

- Added `POST /api/version/history` — Manually add version history entries
- Added `PUT /api/version/history/{id}` — Edit version history entries
- Added `DELETE /api/version/history/{id}` — Remove version history entries
- Separated history by type: `GET /api/version/history/server`, `GET /api/version/history/thinclient`

#### Changed: SystemDetailsController

- Added `GET /api/systemdetails/report/summaries` — Lightweight report summaries for all computers

#### Changed: MapController

- Added `POST /api/map/refresh/{computerId}` — Refresh geolocation for a single computer

---

### v1.3.0 — February 17, 2026

**Settings Endpoint**

#### New: SettingsController (`api/settings`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/settings/server-url` | Anonymous | Returns the configured server URL for agent bootstrapping |

---

### v1.2.0 — January 6, 2026

**Data Integrity, Performance, and Assignment Redesign**

#### ⚠️ Breaking: AssignedTo Field Change

- **Migration**: `005_ChangeAssignedToManualInput.sql`
- `AssignedUserId` (INT, FK to `Users`) replaced by `AssignedTo` (NVARCHAR(200), free-text)
- Existing assigned-user display names were migrated to the new column
- **Impact**: Any client code referencing `AssignedUserId` must switch to `AssignedTo` (string)

#### New: Optimistic Concurrency Control

- **Migration**: `007_AddRowVersionConcurrency.sql`
- `RowVersion` (SQL Server `rowversion`) added to `Computers` table
- `PUT /api/computers/{id}` now returns `409 Conflict` when concurrent edits detected

#### Performance: Database Indexes

- **Migration**: `006_DatabaseOptimization.sql`
- Added nonclustered indexes on `Computers`: LastSeen, IsOnline, StatusId, DepartmentId, LocationId, HasAgent, UpdateStatus, ForceUpdate, Hostname, PublicIpAddress, CreatedAt
- Added indexes on `ComputerHistories`: ComputerId+ChangeDate, ChangeType
- Significant improvement to list, filter, and search query performance

#### Changed: AgentController

- Added `GET /api/agent/download` — Download agent installer ZIP
- Added `GET /api/agent/download/install-script` — Pre-configured PowerShell install script
- Error logs stored on `Computers.UpdateErrorLogs` (Migration `004_AddUpdateErrorLogs.sql`)

#### Changed: ComputersController

- Added `GET /api/computers/{id}/error-logs` — Download update error logs
- Added `GET /api/computers/{id}/error-analysis` — Structured error log analysis
- Added `DELETE /api/computers/{id}/error-logs` — Clear error logs

#### New: HealthController (`api/health`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/health` | None | Basic health check |
| GET | `/api/health/ping` | None | Latency test |
| GET | `/api/health/detailed` | None | Full component health report |
| GET | `/api/health/live` | None | Kubernetes liveness probe |
| GET | `/api/health/ready` | None | Kubernetes readiness probe |

---

### v1.1.0 — December 31, 2025

**Geolocation & Map Support**

#### New: MapController (`api/map`)

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/map/locations` | Authorize | All computer geolocations (cached 30s) |
| GET | `/api/map/last-refresh` | Authorize | Last geolocation refresh timestamp |
| POST | `/api/map/refresh` | TechnicianOrAdmin | Refresh all geolocations |
| PUT | `/api/map/location-source/{computerId}` | TechnicianOrAdmin | Set GPS/IP/Auto preference |

- GPS coordinates from agent heartbeat alongside IP-based geolocation
- Dual coordinate storage: GPS (latitude/longitude/accuracy) and IP-based
- Per-computer location source preference (GPS, IP, Auto)
- **Migration**: `002_AddGpsLocationSupport.sql` — `LocationSource`, `GpsAccuracyMeters` on `ComputerGeoLocations`
- **Migration**: `003_AddLocationSourcePreference.sql` — Dual coordinate columns, `PreferredLocationSource` on `Computers`

---

### v1.0.0 — December 30, 2025

**Initial Release**

Core inventory management API with agent communication, user management, and maintenance tracking.

#### Controllers (Initial)

| Controller | Route | Endpoints | Description |
|------------|-------|-----------|-------------|
| AgentController | `api/agent` | 5 | Agent registration, heartbeat, check-in/out, update progress |
| AuthController | `api/auth` | 4 | Login, register, validate, user profile |
| ComputersController | `api/computers` | 12 | CRUD, search, filter by status/location/department, stats |
| LookupsController | `api/lookups` | 11 | Reference data (types, statuses, departments, locations, etc.) |
| MaintenanceController | `api/maintenance` | 8 | Maintenance record CRUD with status/schedule filtering |
| RestorePointController | `api/restorepoint` | 7 | System restore point snapshots (created by ThinClient) |
| SystemDetailsController | `api/systemdetails` | 3 | Hardware/software inventory submission and retrieval |
| UsersController | `api/users` | 6 | User CRUD with role filtering |
| VersionController | `api/version` | 3 | Server/thin client version info and history |

#### Core Features

- **Authentication**: JWT Bearer tokens via httpOnly cookies with role-based authorization (Admin, Technician, User)
- **Agent Communication**: API-key authenticated endpoints for agent registration, heartbeat, and update tracking
- **Real-time Updates**: SignalR hub (`/hubs/update-progress`) for live agent update status
- **Update Tracking**: Agent update lifecycle (Pending → Downloading → Installing → Completed/Failed)
- **Restore Points**: Automated system state snapshots captured on thin client reboot
- **Background Services**: `DatabaseMaintenanceService` for offline detection, record cleanup, and retention enforcement
- **Caching**: In-memory cache with LRU eviction for frequently-queried endpoints
- **Rate Limiting**: Custom middleware with per-category limits (auth: 20/min, agent: 1000/min, default: 100/min)
- **Migration**: `001_AddUpdateProgressFields.sql` — `UpdateStatus`, `UpdateProgress`, `UpdateStatusMessage`, `UpdateStartedAt` on `Computers`

#### Configuration

| Setting | Default |
|---------|---------|
| JWT expiration | 60 minutes |
| Refresh token expiration | 7 days |
| Agent heartbeat interval | 5 minutes |
| Offline threshold | 5 minutes |
| System details retention | 30 days |
| Restore point retention | 90 days |
| History retention | 365 days |

---

## Endpoint Reference

### Authentication Endpoints — `api/auth`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/auth/login` | Anonymous | Login, returns JWT in httpOnly cookies |
| POST | `/api/auth/register` | Anonymous | Register new user |
| POST | `/api/auth/refresh` | Anonymous | Refresh JWT using refresh token cookie |
| POST | `/api/auth/logout` | Anonymous | Revoke refresh tokens, clear cookies |
| POST | `/api/auth/validate` | Authorize | Validate current token |
| GET | `/api/auth/me` | Authorize | Get current user profile |
| POST | `/api/auth/change-password` | Authorize | Change password |

### Agent Endpoints — `api/agent`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/agent/register` | API Key | Register/update computer |
| POST | `/api/agent/heartbeat` | API Key | Periodic heartbeat |
| POST | `/api/agent/checkin` | API Key | Check-in/out |
| POST | `/api/agent/checkout` | API Key | Alias for check-out |
| POST | `/api/agent/update-progress` | API Key | Report update status |
| GET | `/api/agent/status/{serialNumber}` | API Key | Get computer status |
| GET | `/api/agent/download` | Anonymous | Download agent installer |
| GET | `/api/agent/download/install-script` | Anonymous | Get install script |
| GET | `/api/agent/check-update` | API Key | Check for available updates |

### Computer Endpoints — `api/computers`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/computers` | Authorize | List all computers |
| GET | `/api/computers/{id}` | Authorize | Get by ID |
| GET | `/api/computers/{id}/error-logs` | TechnicianOrAdmin | Download error logs |
| GET | `/api/computers/{id}/error-analysis` | TechnicianOrAdmin | Analyze error logs |
| DELETE | `/api/computers/{id}/error-logs` | TechnicianOrAdmin | Clear error logs |
| GET | `/api/computers/serial/{serialNumber}` | Authorize | Get by serial number |
| GET | `/api/computers/status/{status}` | Authorize | Filter by status |
| GET | `/api/computers/location/{location}` | Authorize | Filter by location |
| GET | `/api/computers/department/{department}` | Authorize | Filter by department |
| GET | `/api/computers/search` | Authorize | Search by term |
| GET | `/api/computers/stats` | Authorize | Inventory statistics |
| POST | `/api/computers` | TechnicianOrAdmin | Create computer |
| PUT | `/api/computers/{id}` | TechnicianOrAdmin | Update computer |
| DELETE | `/api/computers/{id}` | AdminOnly | Delete computer |
| GET | `/api/computers/export` | TechnicianOrAdmin | Export CSV |
| POST | `/api/computers/import` | TechnicianOrAdmin | Import CSV |
| POST | `/api/computers/bulk-status` | TechnicianOrAdmin | Batch status update |
| POST | `/api/computers/bulk-delete` | AdminOnly | Batch delete |
| PATCH | `/api/computers/{id}/status` | TechnicianOrAdmin | Update status |
| PATCH | `/api/computers/{id}/assign` | TechnicianOrAdmin | Assign/unassign |
| POST | `/api/computers/{id}/force-update` | TechnicianOrAdmin | Force agent update |
| POST | `/api/computers/{id}/cancel-update` | TechnicianOrAdmin | Cancel update |
| POST | `/api/computers/{id}/force-reinstall` | Admin | Full reinstall |
| POST | `/api/computers/{id}/request-checkin` | TechnicianOrAdmin | Request check-in |

### Dashboard — `api/dashboard`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/dashboard` | AdminOnly | Aggregated admin dashboard |

### Department Management — `api/departments`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/departments` | AdminOnly | List with usage counts |
| GET | `/api/departments/{id}` | AdminOnly | Get by ID |
| POST | `/api/departments` | AdminOnly | Create |
| PUT | `/api/departments/{id}` | AdminOnly | Update |
| DELETE | `/api/departments/{id}` | AdminOnly | Delete (if unused) |

### Location Management — `api/locations`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/locations` | AdminOnly | List with usage counts |
| GET | `/api/locations/{id}` | AdminOnly | Get by ID |
| POST | `/api/locations` | AdminOnly | Create |
| PUT | `/api/locations/{id}` | AdminOnly | Update |
| DELETE | `/api/locations/{id}` | AdminOnly | Delete (if unused) |

### Lookups — `api/lookups`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/lookups/computer-types` | Authorize | Computer types |
| GET | `/api/lookups/computer-statuses` | Authorize | Computer statuses |
| GET | `/api/lookups/maintenance-types` | Authorize | Maintenance types |
| GET | `/api/lookups/maintenance-statuses` | Authorize | Maintenance statuses |
| GET | `/api/lookups/user-roles` | Authorize | User roles |
| GET | `/api/lookups/version-types` | Authorize | Version types |
| GET | `/api/lookups/departments` | Authorize | Departments |
| GET | `/api/lookups/locations` | Authorize | Locations |
| GET | `/api/lookups/manufacturers` | Authorize | Manufacturers |
| GET | `/api/lookups/models` | Authorize | Models (optional `?manufacturerId=`) |
| GET | `/api/lookups/operating-systems` | Authorize | Operating systems |

### Maintenance — `api/maintenance`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/maintenance` | Authorize | List all |
| GET | `/api/maintenance/paged` | Authorize | Paged list |
| GET | `/api/maintenance/{id}` | Authorize | Get by ID |
| GET | `/api/maintenance/computer/{computerId}` | Authorize | Get by computer |
| GET | `/api/maintenance/status/{status}` | Authorize | Filter by status |
| GET | `/api/maintenance/scheduled` | Authorize | Scheduled range |
| POST | `/api/maintenance` | TechnicianOrAdmin | Create |
| PUT | `/api/maintenance/{id}` | TechnicianOrAdmin | Update |
| DELETE | `/api/maintenance/{id}` | AdminOnly | Delete |
| PATCH | `/api/maintenance/{id}/complete` | TechnicianOrAdmin | Mark complete |

### Map — `api/map`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/map/locations` | Authorize | All geolocations |
| GET | `/api/map/last-refresh` | Authorize | Last refresh time |
| POST | `/api/map/refresh` | TechnicianOrAdmin | Refresh all |
| POST | `/api/map/refresh/{computerId}` | TechnicianOrAdmin | Refresh single |
| PUT | `/api/map/location-source/{computerId}` | TechnicianOrAdmin | Set source preference |

### Packages — `api/packages`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/packages` | AdminOnly | List all packages |
| GET | `/api/packages/{id}` | AdminOnly | Get with rules |
| POST | `/api/packages/upload` | AdminOnly | Upload package |
| PUT | `/api/packages/{id}` | AdminOnly | Update metadata |
| DELETE | `/api/packages/{id}` | AdminOnly | Delete (Draft/Archived) |
| POST | `/api/packages/{id}/publish` | AdminOnly | Publish |
| POST | `/api/packages/{id}/activate` | AdminOnly | Activate |
| POST | `/api/packages/{id}/deprecate` | AdminOnly | Deprecate |
| POST | `/api/packages/{id}/rollback` | AdminOnly | Rollback |
| POST | `/api/packages/{id}/rules` | AdminOnly | Add rule |
| DELETE | `/api/packages/{id}/rules/{ruleId}` | AdminOnly | Remove rule |
| GET | `/api/packages/resolve` | API Key | Resolve for agent |
| GET | `/api/packages/{id}/download` | None | Download ZIP |
| GET | `/api/packages/distribution-summary` | AdminOnly | Fleet summary |

### Restore Points — `api/restorepoint`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/restorepoint` | None | Create snapshot |
| GET | `/api/restorepoint/computer/{serialNumber}` | None | List by serial |
| GET | `/api/restorepoint/{restorePointId}` | None | Get by ID |
| GET | `/api/restorepoint/computer/{serialNumber}/latest` | None | Get latest |
| GET | `/api/restorepoint/summary` | None | Summary stats |
| DELETE | `/api/restorepoint/{restorePointId}` | None | Delete |
| DELETE | `/api/restorepoint/computer/{serialNumber}/cleanup` | None | Cleanup old |

### Settings — `api/settings`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/settings/server-url` | Anonymous | Get server URL |

### Setup — `api/setup`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/setup/status` | Anonymous | Check setup status |
| POST | `/api/setup/initialize` | Anonymous | Initial admin setup |

### System Details — `api/systemdetails`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/systemdetails/submit` | Anonymous | Submit hardware details |
| GET | `/api/systemdetails/computer/{computerId}` | Authorize | Get latest |
| GET | `/api/systemdetails/computer/{computerId}/history` | Authorize | Get history |
| GET | `/api/systemdetails/report/summaries` | Authorize | Report summaries |

### Users — `api/users`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/users` | AdminOnly | List all |
| GET | `/api/users/{id}` | Authorize | Get by ID |
| GET | `/api/users/role/{role}` | AdminOnly | Filter by role |
| POST | `/api/users` | AdminOnly | Create |
| PUT | `/api/users/{id}` | AdminOnly | Update |
| DELETE | `/api/users/{id}` | AdminOnly | Delete |
| PATCH | `/api/users/{id}/activate` | AdminOnly | Activate |
| PATCH | `/api/users/{id}/deactivate` | AdminOnly | Deactivate |
| PATCH | `/api/users/{id}/reset-password` | AdminOnly | Reset password |

### Versioning — `api/version`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/version/server/current` | Authorize | Current server version |
| GET | `/api/version/thinclient/current` | Authorize | Current thin client version |
| GET | `/api/version/history` | Authorize | Full version history |
| GET | `/api/version/history/server` | Authorize | Server history |
| GET | `/api/version/history/thinclient` | Authorize | Thin client history |
| POST | `/api/version/history` | AdminOnly | Add entry |
| PUT | `/api/version/history/{id}` | AdminOnly | Update entry |
| DELETE | `/api/version/history/{id}` | AdminOnly | Delete entry |
| POST | `/api/version/record` | Anonymous/API Key | Auto-record deployment |

### Health — `api/health`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/health` | None | Basic check |
| GET | `/api/health/ping` | None | Latency test |
| GET | `/api/health/detailed` | None | Detailed status |
| GET | `/api/health/live` | None | Liveness probe |
| GET | `/api/health/ready` | None | Readiness probe |

### Audit Log — `api/auditlog`

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/auditlog` | AdminOnly | Paged audit log |

### SignalR Hub

| Hub | Path | Auth | Description |
|-----|------|------|-------------|
| UpdateProgressHub | `/hubs/update-progress` | Authorize (JWT query string) | Real-time agent update status broadcasts |

---

## Authentication

### JWT Bearer

- Issued via `POST /api/auth/login` as httpOnly cookies (`access_token`, `refresh_token`)
- Expiration: 60 minutes (access), 7 days (refresh)
- Issuer: `ComputerInventoryApi`, Audience: `ComputerInventoryClient`
- Refresh via `POST /api/auth/refresh` with automatic token rotation
- SignalR accepts JWT via `access_token` query parameter

### API Key

- Sent via `X-API-Key` header
- Used by agent endpoints (`api/agent/*`) and package resolution
- Configured in `appsettings.json` under `ApiSettings:ApiKey`

### Authorization Policies

| Policy | Roles |
|--------|-------|
| `AdminOnly` | Admin |
| `TechnicianOrAdmin` | Technician, Admin |
| `AllUsers` | User, Technician, Admin |

---

## Database Migrations

| # | File | Date | Summary |
|---|------|------|---------|
| 001 | `001_AddUpdateProgressFields.sql` | Dec 30, 2025 | Update tracking fields on Computers |
| 002 | `002_AddGpsLocationSupport.sql` | Dec 31, 2025 | GPS location source and accuracy fields |
| 003 | `003_AddLocationSourcePreference.sql` | Dec 31, 2025 | Dual GPS/IP coordinates, source preference |
| 004 | `004_AddUpdateErrorLogs.sql` | Jan 5, 2026 | Error log storage on Computers |
| 005 | `005_ChangeAssignedToManualInput.sql` | Jan 6, 2026 | ⚠️ AssignedUserId → AssignedTo (breaking) |
| 006 | `006_DatabaseOptimization.sql` | Jan 6, 2026 | Performance indexes on Computers, History |
| 007 | `007_AddRowVersionConcurrency.sql` | Jan 6, 2026 | Optimistic concurrency on Computers |
| 008 | `008_AddHeartbeatIntervalPerComputer.sql` | Mar 10, 2026 | Per-computer heartbeat interval |
| 009 | `009_AddPackageVersioning.sql` | Mar 10, 2026 | Package versions and distribution rules |
| 010 | `010_AddRefreshTokens.sql` | Mar 10, 2026 | Refresh token table with family rotation |
