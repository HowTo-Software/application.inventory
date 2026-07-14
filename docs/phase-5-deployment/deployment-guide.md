# Deployment Guide

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

## 1. Overview

Self-hosted **Docker Compose** stack on a single Linux host, deployed over SSH
from a Windows workstation with [deploy-server.ps1](../../deploy-server.ps1).
Three containers — API (ASP.NET Core), Web (Blazor WASM behind nginx), and Redis
— share the `INV-MKS-Default` bridge network. **SQL Server is external** (not
containerized). Images are built on the server from source; there is no registry.

## 2. Environments

| Environment | Host / URL | Source | Deploy | Purpose |
|-------------|-----------|--------|--------|---------|
| Local dev | `https://localhost:5000` (API) · `:5001` (Web) | working tree | `docker-compose.dev.yml` | Dev / integration |
| Production | `http://<server>:5010` (API) · `:5011` (Web) | `main` | `deploy-server.ps1` (manual) | Live |

No separate staging environment is provisioned.

## 3. Prerequisites

- **Workstation:** Windows + PowerShell, OpenSSH (`ssh`/`scp`), the repo cloned, .NET SDK 8/9 (`global.json` pins 9.0).
- **Server:** Linux with Docker Engine + Compose plugin, SSH access (sudo for cleanup).
- **Database:** a reachable external SQL Server (`,1433`) and an empty/existing `device_management` database.
- **Secrets:** values for `server/.env` (see §6) — DB credentials, JWT key, agent API key.

## 4. Build Artifact

Images are built **on the server** from the Dockerfiles (no registry push):

```bash
# On the server (invoked by setup.sh during deploy)
docker compose up -d --build      # builds INV-MKS-API, INV-MKS-Web; pulls redis:7-alpine
```

- API: [src/ComputerInventory.Api/Dockerfile](../../src/ComputerInventory.Api/Dockerfile) (`aspnet:8.0` runtime, `sdk:8.0` build).
- Web: `src/ComputerInventory.Web/Dockerfile` (nginx-served Blazor WASM).
- **Agent update package** (versioned artifact) is built on the workstation:

```powershell
.\build-update-package.ps1 -Version 1.0.1.0 -ReleaseNotes "…"   # -> publish-update\*.zip
```

## 5. Deploy

### Automated (preferred)

One command from the workstation cleans the old install (preserving `.env`),
generates config, SCPs files, and runs `setup.sh` (`docker compose up -d --build`):

```powershell
.\deploy-server.ps1 -ServerHost <ip> -DbServer "<host>,1433" `
    -DbName device_management -DbUser <user> -DbPassword <pw> -AgentApiKey <key>
```

Copies [server/migrations/](../../server/migrations/) so the API applies them at
startup (§7). CI does **not** trigger deploys — see [CI/CD](ci-cd.md).

### Manual (on the server)

```bash
cd ~/computer-inventory && docker compose up -d --build && docker compose ps
```

## 6. Configuration & Secrets

App config is injected as environment variables from `server/.env` (copied from
[server/.env.example](../../server/.env.example)) — **never committed**. Redis
(`redis:6379`) and data-protection keys (`inv-mks-api-keys:/app/keys`) are internal.

| Variable | Environment | Source |
|----------|-------------|--------|
| `DB_CONNECTION_STRING` | prod | `.env` → `ConnectionStrings__DefaultConnection` |
| `JWT_SECRET_KEY` | prod | `.env` (random ≥64 chars) → `JwtSettings__SecretKey` |
| `AGENT_API_KEY` | prod | `.env` → `AgentSettings__ApiKey` |
| `AGENT_SERVER_URL` | prod | `.env` → `AgentSettings__ServerUrl` |

## 7. Database Migrations

Plain `.sql` scripts in `server/migrations/` (`001…048+`) are baked into the API
image at `/app/migrations` and applied **automatically at API startup** by
`DatabaseMigrationRunner`: it creates a `__migration_history` table and runs
pending scripts in filename order (idempotent — applied scripts are skipped).

Migrations run **before** the API serves traffic. They are **forward-only** (no
down scripts); keep changes backward-compatible and rely on the external SQL
backup for schema rollback.

## 8. Verification / Smoke Test

```bash
curl -fsS http://<server>:5010/health/ready     # expect 200
docker compose ps                                # all containers Up
```

- [ ] `/health/live` and `/health/ready` return 200
- [ ] Web loads at `http://<server>:5011`; sign in with the seeded admin (change the password immediately)
- [ ] No error spike in [monitoring](../phase-6-operations/monitoring.md)

## 9. Rollback

No image tags/registry, so rollback = **redeploy the previous `main` commit**:

```powershell
git checkout <previous-good-sha>; .\deploy-server.ps1 -ServerHost <ip> …
```

External SQL data persists across redeploys; the keys volume is recreated (users
re-login). Schema is forward-only — restore from a SQL backup if a migration must
be undone. **Trigger:** failed smoke test or error spike; **authorized by:** Engineering owner.

## 10. Deployment Strategies

- **Recreate (current)** — `compose down` → `up`; brief downtime on a single host.
- **Rolling / Blue-Green / Canary** — not implemented at the infra layer.
- **Agent updates are canary-capable** via channels `stable`/`beta`/`canary` and distribution rules (see [technical-design](../phase-2-design/technical-design.md)).

## 11. Post-Deployment

- Update [Release Notes](release-notes-template.md) and the [CHANGELOG](../../CHANGELOG.md).
- Monitor per [monitoring](../phase-6-operations/monitoring.md) for the first ~15 minutes.
