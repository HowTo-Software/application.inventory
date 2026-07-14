# Disaster Recovery & Business Continuity

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Scope: the self-hosted Computer Inventory stack — three Docker containers
(`INV-MKS-API`, `INV-MKS-Web`, `INV-MKS-Redis`) on a single Linux host plus an
**external SQL Server** (`device_management`). See
[Deployment Guide](../phase-5-deployment/deployment-guide.md) and
[Architecture](../phase-2-design/architecture.md).

## 1. Objectives

| Metric | Definition | Target |
|--------|------------|--------|
| **RTO** (Recovery Time Objective) | Max acceptable downtime | **4 hours** (rebuild host + `deploy-server.ps1` ≈ 30 min; DB restore dominates) |
| **RPO** (Recovery Point Objective) | Max acceptable data loss | **15 minutes** (SQL transaction-log backups) |

Only `device_management` is stateful and hard to reconstruct; everything else
(images, config, keys) is rebuildable from Git + secrets within the RTO.

## 2. Backup Strategy

| Asset | Method | Frequency | Retention | Location |
|-------|--------|-----------|-----------|----------|
| **SQL `device_management`** (all app data) | Native `BACKUP DATABASE` / `BACKUP LOG` (full + differential + t-log) | Full daily · diff 6h · **t-log 15 min** | 30 days | Off-host / offsite share |
| **Secrets & config** (`server/.env`) | Encrypted copy | On change | All versions | Secret/password manager |
| **Data-protection keys** (`inv-mks-api-keys` volume) | `docker … tar` export (see §4) | Weekly + on change | 30 days | Off-host, encrypted |
| **Agent packages** (`{PackagesPath}/*/agent-update.zip`) | File copy | On publish | Match DB | Off-host (⚠️ not on a named volume — see §6) |
| **Source + SQL migrations** | Git push | Continuous | Indefinite | `github.com/MKSWORKS/inventory` |
| **Redis** (cache + DP-key cache) | None — ephemeral, rebuilt on restart | — | — | — |

- Backups are **encrypted** at rest and in transit.
- Backups are stored on a **separate host/offsite** from the app + DB servers.
- SQL Server runs in **FULL recovery model** so t-log backups (RPO) are possible.

## 3. Backup Verification

> A backup you haven't restored is only a hypothesis.

- **Monthly** automated restore test: `RESTORE` the latest full into a throwaway
  database, then `DBCC CHECKDB(...) WITH NO_INFOMSGS, ALL_ERRORMSGS` — a backup
  can capture pre-existing corruption, so verify before trusting it.
- Verify `.bak`/`.trn` integrity with `RESTORE VERIFYONLY` + SHA-256 checksums.
- Record results in the [Runbook](runbook.md#7-maintenance-tasks) maintenance log.

## 4. Recovery Procedures

### Scenario: Database corruption / loss

1. Declare incident (see [Runbook](runbook.md#6-incident-response-process)).
2. Identify the latest healthy backup within RPO (verify with `CHECKDB`).
3. Restore (point-in-time) on the SQL Server host:
   ```sql
   RESTORE DATABASE device_management FROM DISK = '<full.bak>'  WITH NORECOVERY, REPLACE;
   RESTORE DATABASE device_management FROM DISK = '<diff.bak>'  WITH NORECOVERY;
   RESTORE LOG      device_management FROM DISK = '<log.trn>'   WITH RECOVERY, STOPAT = '<yyyy-mm-ddThh:mm:ss>';
   DBCC CHECKDB(device_management) WITH NO_INFOMSGS, ALL_ERRORMSGS;
   ```
4. Restart the API (`docker compose restart api`) so migrations re-verify, then
   confirm `curl -fsS http://<server>:5010/health/ready` returns 200.
5. Validate key data and monitor per [Monitoring](monitoring.md).

### Scenario: Host / infrastructure outage

1. Provision a new Linux host with Docker Engine + Compose.
2. Restore `server/.env` (secrets) and the `inv-mks-api-keys` volume:
   ```bash
   docker run --rm -v inv-mks-api-keys:/keys -v "$PWD":/backup alpine \
     tar xzf /backup/inv-mks-api-keys.tgz -C /keys
   ```
3. Redeploy from the workstation — images rebuild and migrations auto-apply:
   ```powershell
   git clone https://github.com/MKSWORKS/inventory; cd inventory
   .\deploy-server.ps1 -ServerHost <new-ip> -DbServer "<sql-host>,1433" `
     -DbName device_management -DbUser <user> -DbPassword <pw> -AgentApiKey <key>
   ```
4. Re-upload agent packages (not volume-backed); point clients/DNS at the new host.
5. Verify (§Smoke test in the Deployment Guide) and communicate.

> If keys are lost, users simply re-authenticate (JWT/cookies re-issued) — not a
> data-loss event; Redis rebuilds itself on restart.

### Scenario: Accidental data deletion

1. Most deletes are **soft-deletes** (`is_deleted` / `deleted_at`) — first try to
   recover in place: `UPDATE <table> SET is_deleted = 0, deleted_at = NULL WHERE …`.
2. If rows were hard-deleted, restore point-in-time to a throwaway DB (§Database
   scenario) and copy the affected rows back.
3. Reconcile and confirm application health.

## 5. Roles & Communication

| Role | Responsibility |
|------|----------------|
| Incident Commander | Coordinates recovery, declares severity |
| Technical Lead | Executes restore/redeploy steps |
| Comms Lead | Updates IT stakeholders / affected users |

Communication channels: internal email + team chat; escalation to the
Engineering owner for Sev1 (data loss / full outage).

## 6. Dependencies

| Dependency | Role in recovery | Notes |
|------------|------------------|-------|
| External **SQL Server** | Primary data store | Hard dependency; must be reachable at `<host>,1433` |
| **Docker Engine + Compose** | Runs the app tier | Rebuilds images from source on the host |
| **GitHub** (`MKSWORKS/inventory`) | Source, migrations, CI | Required to redeploy |
| **RustDesk relay** (SSLH `:21118`) | Remote-support connectivity | Non-critical to core recovery |

> **Known gap:** agent update `.zip` files live on the API container filesystem
> (`PackageService`), **not** a named Docker volume, so they are lost on host
> loss/redeploy. DB metadata survives (external SQL); re-upload packages after
> recovery. Tracked in [TODO](../TODO.md) ("database backup & DR scripts").

## 7. Testing & Review

- **DR drill frequency:** quarterly (full restore of `device_management` to a
  scratch DB + `CHECKDB`, and a host-rebuild dry run).
- Review and update this plan after every drill or major architecture change.

| Date | Type | Result | Follow-ups |
|------|------|--------|-----------|
| 2026-07-14 | Plan authored | Baseline established | Automate SQL backup + package-volume persistence |
