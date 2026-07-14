# API Specification

> **Status:** 🟢 Active · **Owner:** IT Support · **Last updated:** 2026-07-14
>
> Human-friendly companion to the Computer Inventory API. The machine-readable
> OpenAPI document is served by the running API at `/swagger/v1/swagger.json`,
> with interactive docs at `/swagger`. For the exhaustive, version-by-version
> route list, see [API_CHANGELOG.md](../API_CHANGELOG.md).

## 1. Overview

- **Name:** Computer Inventory API (`v1`)
- **Base path:** host-relative `/api` (e.g. `https://inventory.example.com/api`)
- **Protocol:** REST / JSON over HTTPS — ASP.NET Core 8.0
- **Versioning:** single version. All routes are `/api/{controller}` with no
  version segment; the running build is advertised via the `X-Server-Version`
  response header and `GET /api/version/server/current`.
- **Real-time:** SignalR hub at `/hubs/update-progress` (see §7).
- **Scale:** 19 controllers exposing ~130 endpoints.

## 2. Authentication

Two schemes, selected per endpoint.

**JWT Bearer (web/UI users).** Obtain a token via `POST /api/auth/login`. On
success the API sets two httpOnly, Secure cookies — `access_token` (60 min) and
`refresh_token` (7 days) — and also returns the token in the body. Clients may
instead send the token explicitly:

```http
Authorization: Bearer <access_token>
```

- Issuer `ComputerInventoryApi`, Audience `ComputerInventoryClient` (HMAC-SHA256).
- Refresh via `POST /api/auth/refresh` (rotates the refresh token); revoke via `POST /api/auth/logout`.
- SignalR clients pass the token as an `access_token` query-string parameter.

**API Key (agents).** Agent and package-resolution endpoints authenticate with a
shared key sent in the `X-API-Key` header (configured under `AgentSettings:ApiKey`).

**Roles & policies.** Roles are `Viewer`, `Technician`, `Admin`. Endpoints are
guarded by policies:

| Policy | Grants access to |
|--------|------------------|
| `AdminOnly` | Admin |
| `TechnicianOrAdmin` | Technician, Admin |
| `AllUsers` | Viewer, Technician, Admin |

## 3. Conventions

- **Content type:** `application/json` (errors use `application/problem+json`).
- **JSON:** camelCase property names, `null`s omitted, enums serialized as strings, case-insensitive on input.
- **Timestamps:** ISO 8601 / UTC (round-trip `o` format).
- **Pagination:** offset-based via `page` and `pageSize` query params on paged
  endpoints; responses use the envelope:

  ```json
  { "items": [], "totalCount": 0, "page": 1, "pageSize": 20, "totalPages": 0 }
  ```

- **Compression:** Brotli/Gzip negotiated via `Accept-Encoding`.
- **Correlation:** every error response carries a `correlationId` for support tracing.
- **Request size:** 30 MB global limit; package upload allows up to 500 MB.
- **Caching:** hot read endpoints (computer list/stats, dashboard, map) are cached 15–30 s server-side.

## 4. Endpoints

Endpoints are grouped by controller under `/api`. The table below is the map;
the full per-route reference (method, auth, description) lives in
[API_CHANGELOG.md](../API_CHANGELOG.md) and Swagger.

| Group | Base route | Purpose |
|-------|-----------|---------|
| Auth | `/api/auth` | Login, refresh, logout, profile, password |
| Agent | `/api/agent` | Registration, heartbeat, check-in/out, update reporting, installer download |
| Computers | `/api/computers` | Inventory CRUD, search/filter, stats, CSV import/export, bulk & update actions |
| Ownership | `/api/computers/{id}/ownership` | Per-computer access grants |
| System Details | `/api/systemdetails` | Hardware/software inventory submission & history |
| Restore Points | `/api/restorepoint` | Thin-client restore-point snapshots |
| Maintenance | `/api/maintenance` | Maintenance records & scheduling |
| Users | `/api/users` | User administration |
| Departments / Locations | `/api/departments`, `/api/locations` | Org reference data |
| Lookups | `/api/lookups` | Read-only reference lists |
| Map | `/api/map` | Geolocation data & refresh |
| Packages | `/api/packages` | Agent package versioning & distribution |
| Dashboard | `/api/dashboard` | Aggregated admin metrics |
| Audit Log | `/api/auditlog` | Paged audit trail |
| Settings / Setup | `/api/settings`, `/api/setup` | Bootstrap config & first-run setup |
| Version | `/api/version` | Server/thin-client version info & history |
| Health | `/api/health` | Liveness, readiness, detailed status |

### Example — `POST /api/auth/login`

**Request**

```json
{ "username": "jsmith", "password": "••••••••" }
```

**Response `200 OK`** (also sets `access_token` / `refresh_token` cookies)

```json
{
  "success": true,
  "token": "<jwt>",
  "refreshToken": "<token>",
  "user": {
    "id": 1, "username": "jsmith", "email": "jsmith@example.com",
    "firstName": "Jane", "lastName": "Smith",
    "department": "IT", "role": "Admin"
  }
}
```

### Example — `GET /api/computers`

Lists inventory (auth required). Filter via sibling routes
(`/status/{status}`, `/location/{location}`, `/department/{department}`,
`/search?term=`); returns an array of computer objects.

### Example — `POST /api/agent/heartbeat`

Agent check-in authenticated with `X-API-Key`; updates last-seen/online state
and IP/GPS geolocation, and returns any pending update or check-in flags.

## 5. Error Format

Errors follow RFC 7807 Problem Details with content type
`application/problem+json`:

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "The requested resource was not found.",
  "instance": "/api/computers/999",
  "correlationId": "0HN…",
  "timestamp": "2026-07-14T12:00:00.000Z"
}
```

Validation failures (FluentValidation / model binding) return `400` with the
standard ASP.NET Core `errors` dictionary. Raw database messages are never
leaked; stack traces appear only in Development.

| HTTP | Title | When |
|------|-------|------|
| 400 | Invalid Argument / Validation | Bad input or referential constraint |
| 401 | Unauthorized | Missing/invalid token or API key |
| 403 | Forbidden | Authenticated but role not permitted |
| 404 | Resource Not Found | Entity does not exist |
| 409 | Concurrency Conflict / Duplicate | `RowVersion` mismatch or unique-key clash |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Unexpected failure |
| 503 | Database Unavailable | SQL Server unreachable |

## 6. Rate Limiting

Enabled globally (health checks exempt). Limits are per-minute, keyed by client
IP + identity (agents keyed per IP by API key). Authenticated web users receive
a 2× multiplier.

| Category | Match | Limit (req/min) |
|----------|-------|-----------------|
| Agent | `/api/agent/*` (valid `X-API-Key`) | 1000 |
| Auth | `/auth/login`, `/auth/register` | 10 |
| Query | `/search`, `/stats` | 60 |
| Default | everything else | 100 |

Responses include `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and
`X-RateLimit-Reset`. A `429` adds `Retry-After` (seconds) plus a Problem Details body.

## 7. Webhooks / Events

No outbound webhooks. Real-time updates are pushed over SignalR:

- **Hub:** `/hubs/update-progress` (`UpdateProgressHub`)
- **Auth:** JWT via `access_token` query-string parameter
- **Delivery:** broadcasts the agent update lifecycle
  (Pending → Downloading → Installing → Completed/Failed) with progress percentage
  to connected dashboard clients. Best-effort; no delivery guarantee or replay.

## 8. Changelog

Detailed, version-by-version API changes are tracked in
[API_CHANGELOG.md](../API_CHANGELOG.md) (current: **v1.5.0**). Broader project
changes live in the main [CHANGELOG](../../CHANGELOG.md).
