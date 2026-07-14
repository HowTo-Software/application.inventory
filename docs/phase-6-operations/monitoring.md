# Monitoring & Observability

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

## 1. Philosophy

Observability rests on three pillars: **metrics**, **logs**, and **traces**.
This is a self-hosted, single-host Docker Compose deployment (API + Web + Redis;
SQL Server external), so observability is **built into the application** —
Serilog structured logging, ASP.NET Core health checks, and in-process
`System.Diagnostics.Metrics` — rather than delivered by an external stack.
Instrument for the questions you'll need to answer during an incident.

## 2. Tooling

| Signal | Tool | Location |
|--------|------|----------|
| Metrics | .NET `System.Diagnostics.Metrics` — `Meter` `ComputerInventory.Api.RequestTiming` (in-process; no exporter wired yet) | `RequestTimingMiddleware` |
| Logs | Serilog — Console + rolling JSON file (`CompactJsonFormatter`) | API `logs/api-.log`; Agent `%ProgramData%\…\agent-.log`; `docker compose logs` |
| Traces | Correlation ID (`X-Correlation-Id`) via `CorrelationIdMiddleware` + `TraceId`; echoed as `X-Request-Id` | response headers / log enrichment |
| Dashboards | `GET /api/health/detailed` (components) + `GET /api/dashboard` (KPIs) | Blazor Web UI |
| Alerting | Serilog `ERROR` on sustained degradation + Docker container health/restart | logs + Docker |

> No Prometheus/Grafana/OTel exporter, APM, or paging service is deployed today.
> The metrics `Meter` is instrumented and export-ready (`// can be extended for
> Prometheus/AppInsights`) if an external stack is later added.

## 3. Key Metrics (The Golden Signals)

| Signal | Metric | Target / SLO |
|--------|--------|--------------|
| **Latency** | `http.server.request.duration` (ms histogram); `X-Response-Time` header; slow-request log > **2000 ms** | p95 < 500 ms |
| **Traffic** | `http.server.request.count` counter, tagged `method` / `route` / `status_code` | baseline |
| **Errors** | 5xx rate from `GlobalExceptionMiddleware` / response status | < 1% |
| **Saturation** | working-set MB, disk % used, DB query time (via health checks) | see §7 thresholds |

## 4. Service Level Objectives (SLOs)

| SLO | Target | Window | Error Budget |
|-----|--------|--------|--------------|
| Availability (`/health/ready` = 200) | 99.9% | 30 days | ~43 min |
| Latency (p95) | < 500 ms | 30 days | — |

## 5. Logging Standards

- **Structured** Serilog: API writes Console (text) + daily rolling file as
  compact JSON; enrichers `FromLogContext`, `WithMachineName`, `WithThreadId`.
- Request logging via `UseSerilogRequestLogging`, enriched with `ClientIP`,
  `CorrelationId`, `UserAgent`, and `AuthenticatedUser`.
- Levels: `ERROR`, `WARN`, `INFO`, `DEBUG`. Default `Information`; overrides
  `Microsoft.AspNetCore` / `EntityFrameworkCore` / `System` = `Warning`.
- **Never log secrets or PII** — JWTs, `X-Api-Key`, and passwords are excluded.
- Retention: **API** 30 files, daily rolling, 100 MB/file cap; **Agent** 7 days.

## 6. Alerts

Alert on **symptoms** (user impact), not just causes. Every alert must be
**actionable** and link to a runbook playbook.

| Alert | Condition | Severity | Runbook |
|-------|-----------|----------|---------|
| Readiness failing | `/health/ready` returns 503 (db / disk / memory / agent-package) | Sev1 | [Runbook](runbook.md#5-common-incidents-playbooks) |
| Container down | Docker health check fails / restart loop | Sev1 | [Runbook](runbook.md) |
| High error rate | 5xx spike in logs > 1% for 5m | Sev2 | [Runbook](runbook.md#5-common-incidents-playbooks) |
| Sustained latency degradation | ≥ 10 requests > 2000 ms in 5m → Serilog `ERROR` | Sev3 | [Runbook](runbook.md) |

## 7. Dashboards

Data sources (no dedicated dashboard tool ships):

- **Service / component health** → `GET /api/health/detailed` — per-check status,
  assembly version, and uptime. Backing checks and thresholds:

  | Check | Tag | Degraded | Unhealthy |
  |-------|-----|----------|-----------|
  | `database` | `ready` | query > 1000 ms | connection fails |
  | `disk-space` | `ready` | > 90% used | < 1 GB free |
  | `memory` | `ready` | > 1024 MB working set | check throws |
  | `agent-package` | `ready` | agent dir missing | check throws |

- **Business KPIs** → `GET /api/dashboard` — computer counts, agent online/offline,
  and update-adoption stats (rendered in the Blazor UI).
- **Raw logs** → `docker compose logs` / `api-.log` JSON (ingest into ELK / Loki /
  Seq if an external stack is later added).

## 8. On-Call

- **Rotation:** single small team — best-effort (no PagerDuty/Opsgenie).
- **Escalation policy:** alerts surface in container logs; escalate to the
  Engineering owner. Probe endpoints: `/health/live` (liveness, 200 while the
  process runs), `/health/ready` (readiness), `/api/health/ping` (latency).
- **Alert fatigue review:** weekly, alongside the runbook review.
