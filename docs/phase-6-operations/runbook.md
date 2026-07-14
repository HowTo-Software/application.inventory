# Operations Runbook

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Operational procedures for running **Computer Inventory** in production. Keep this
practical and copy-pasteable. All server commands run on the single Linux host in
`~/computer-inventory` unless noted.

## 1. Service Overview

| Field | Value |
|-------|-------|
| Service | Computer Inventory (device management) |
| Owner / team | Engineering |
| On-call | Engineering, best-effort (no paging service) |
| Repositories | github.com/MKSWORKS/inventory |
| Dashboards | `GET /api/health/detailed`, `GET /api/dashboard` (see [Monitoring](monitoring.md)) |
| SLObjectives | 99.9% availability (`/health/ready`), p95 < 500 ms |

## 2. Architecture at a Glance

See [Architecture](../phase-2-design/architecture.md). Single-host Docker Compose:

| Container | Port | Role |
|-----------|------|------|
| `INV-MKS-API` | 5010→8080 | ASP.NET Core API (`inv-mks-api-keys` volume) |
| `INV-MKS-Web` | 5011→8080 | Blazor WASM via nginx |
| `INV-MKS-Redis` | 6379 | Cache + Data-Protection keys (optional) |

**Critical dependency:** SQL Server `device_management` is **external** (`:1433`) —
it is the only stateful/durable component. Redis is ephemeral; API/Web images are
rebuilt on deploy. Network `INV-MKS-Default`; secrets in `~/computer-inventory/.env`.

## 3. Routine Operations

### Start / Stop / Restart

```bash
cd ~/computer-inventory
docker compose ps                 # status
docker compose up -d              # start all
docker compose restart api        # restart one service
docker compose stop               # stop (keep containers)
docker compose down               # stop + remove containers (volumes kept)
docker compose logs -f api        # tail structured JSON logs
```

### Scaling

Vertical only. **Do not run multiple API replicas:** the SignalR
`UpdateProgressHub` has no Redis backplane and Data-Protection keys are
per-instance — scale-out would break real-time updates and JWT/cookie validation.
Increase host CPU/RAM instead.

### Deploy / Rollback

```powershell
# From a dev workstation at the repo root:
./deploy-server.ps1 -ServerHost <ip> [-ExternalUrl https://<public-url>]
```

Deploy SCPs the source, preserves `.env`, and runs `docker compose up -d --build`
on the host; DB migrations apply automatically at API startup. **Rollback:**
check out the previous `main` SHA and re-run `deploy-server.ps1` (no image
registry/tags; images rebuild on the host). See the
[Deployment Guide](../phase-5-deployment/deployment-guide.md).

## 4. Health & Monitoring

- **Liveness:** `GET /health/live` (200 while the process runs).
- **Readiness:** `GET /health/ready` (503 if db / disk / memory / agent-package unhealthy).
- **Detail / latency:** `GET /api/health/detailed`, `GET /api/health/ping`.
- **Dashboards / alerts:** see [Monitoring](monitoring.md).

```bash
curl -f http://localhost:5010/health/ready || echo "NOT READY"
curl -s http://localhost:5010/api/health/detailed | jq .
```

## 5. Common Incidents (Playbooks)

### 🔴 API 5xx / high error rate

| | |
|---|---|
| **Detection** | `/health/ready` 503; 5xx spike in `api-.log` |
| **Likely causes** | DB unreachable, bad deploy, unhandled exception, config/secret missing |
| **Diagnosis** | `docker compose logs --tail=200 api`; `curl .../api/health/detailed`; check `CorrelationId` in logs |
| **Mitigation** | Fix `.env` + `docker compose up -d api`; else roll back to previous `main` SHA and redeploy |
| **Escalation** | Engineering owner if unresolved in 30 min |

### 🔴 Database unreachable / connection saturation

| | |
|---|---|
| **Detection** | `database` health check `Unhealthy`; EF `EnableRetryOnFailure` retries in logs |
| **Diagnosis** | From host: `sqlcmd -S .\INSTANCE -E -Q "SELECT 1"`; verify `DB_CONNECTION_STRING`, network to `:1433`, SQL up |
| **Mitigation** | Restore SQL connectivity; `docker compose restart api` to reset the pool |

### 🔴 SQL Server corruption (Msg 824 torn page)

| | |
|---|---|
| **Detection** | Msg 824 in SQL/API logs; CHECKDB errors |
| **Diagnosis** | Force a true on-disk read — **stale buffer cache mimics corruption**: `DBCC DROPCLEANBUFFERS; DBCC CHECKDB(device_management) WITH TABLOCK, NO_INFOMSGS, ALL_ERRORMSGS;` |
| **Mitigation** | If errors persist, restore from a **verified-clean** backup ([DR](disaster-recovery.md)); do not `REPAIR_ALLOW_DATA_LOSS` on system-catalog corruption |

### 🔴 Disk full on API host

| | |
|---|---|
| **Detection** | `disk-space` health check (`< 1 GB` = Unhealthy, `> 90%` = Degraded) |
| **Likely causes** | Accumulated agent package `.zip`s on the API filesystem; log growth |
| **Diagnosis** | `df -h`; inspect API package dir and `logs/` |
| **Mitigation** | Archive/deprecate old `PackageVersion`s; Serilog auto-prunes (30 files). Free space, then re-check |

### 🔴 Redis down

| | |
|---|---|
| **Detection** | Redis connection errors in logs |
| **Impact** | Degrades gracefully — API falls back to in-memory cache/rate-limiting, but Data-Protection keys are lost on API restart (forces re-login) |
| **Mitigation** | `docker compose restart redis`; verify `inv-mks-redis-data` volume mounted |

### 🔴 Agent updates failing en masse

| | |
|---|---|
| **Detection** | Many agents at `update_status = Failed(99)`; `agent-package` health degraded |
| **Diagnosis** | Check the Active `PackageVersion` per channel; verify SHA-256 and package on API FS |
| **Mitigation** | Deprecate the bad version (agents fail-safe roll back via Updater); publish a fixed build to `beta`/`canary` before `stable` |

## 6. Incident Response Process

1. **Acknowledge** the alert.
2. **Assess** severity (below) and declare an incident if needed.
3. **Communicate** status to stakeholders.
4. **Mitigate** first, root-cause later.
5. **Resolve** and confirm recovery (`/health/ready` = 200).
6. **Post-mortem** for Sev1/Sev2 (blameless).

| Severity | Definition | Response |
|----------|------------|----------|
| Sev1 | Full outage / data loss | Immediate, all-hands |
| Sev2 | Major degradation | Urgent |
| Sev3 | Minor / partial | Business hours |

## 7. Maintenance Tasks

| Task | Frequency | Procedure |
|------|-----------|-----------|
| Dependency updates (NuGet) | Monthly | Update packages, run tests, redeploy (no Dependabot) |
| Code-signing cert renewal | Before expiry | Reissue thin-client cert (`thin-client/Create-CodeSigningCert.ps1`) |
| JWT secret rotation | Quarterly / on incident | Rotate `JWT_SECRET_KEY` in `.env`, redeploy (invalidates tokens) |
| Backup verification | Weekly | Restore + `DBCC CHECKDB` on a throwaway DB — see [Disaster Recovery](disaster-recovery.md) |
| Log / retention cleanup | Automatic | Serilog (30 files API / 7 days agent); `DatabaseMaintenanceService` prunes system-details 30d, restore-points 90d, history 365d |
| Agent package cleanup | As needed | Archive superseded `PackageVersion`s to reclaim API disk |

## 8. Contacts & Escalation

| Role | Contact | When |
|------|---------|------|
| Primary on-call | Engineering (best-effort) | First responder |
| Secondary | Engineering owner | Escalation |
| Vendor support | SQL Server host / infra provider | Third-party / DB outage |

## 9. Post-Mortem Template

- **Summary / impact / timeline**
- **Root cause**
- **What went well / what didn't**
- **Action items** (owners + due dates)
