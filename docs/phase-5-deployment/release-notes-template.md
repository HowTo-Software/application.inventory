# Release Notes — v1.0.0

> **Release date:** 2026-07-14 · **Type:** Major
>
> First general-availability release of Computer Inventory. For the full
> technical log, see the [CHANGELOG](../../CHANGELOG.md).

## ✨ Highlights

The first production release of Computer Inventory — a self-hosted platform for
tracking Windows devices, collecting hardware and software telemetry through a
managed thin-client agent, and administering everything from a browser, with
built-in remote access and over-the-air agent updates.

## 🚀 New Features

- **Device inventory** — Central registry of computers with 16 categories of
  hardware, OS, and network details collected automatically by the agent.
- **Thin-client agent** — Windows service that registers devices, submits
  telemetry, and reports live status using an API key + agent session.
- **Over-the-air agent updates** — Package distribution with stable/beta/canary
  channels, targeting rules (serial, department, location), SHA-256 verification,
  and automatic rollback via a SYSTEM-context updater.
- **Geolocation & map** — Per-device location history on an interactive Leaflet map.
- **Remote access** — Integrated RustDesk relay to reach devices through a single port.
- **Maintenance & restore tracking** — Maintenance records, restore points, and a
  full audit log per device.
- **Web console** — Blazor WebAssembly UI with JWT auth and role-based access
  (Admin / Technician / Viewer).

## 🔧 Improvements

- Optional Redis-backed caching, distributed rate limiting, and data-protection
  key persistence, with automatic in-memory fallback.
- Structured Serilog logging with correlation IDs and request timing.
- Brotli/Gzip response compression and security headers on all API responses.
- Health probes (`/health/live`, `/health/ready`) and automatic, forward-only
  database migrations applied at API startup.

## 🐛 Bug Fixes

- None — this is the first public release.

## ⚠️ Breaking Changes

- None — this is the initial release; there is no prior version to migrate from.

## 📦 Upgrade Instructions

Fresh install via Docker Compose (three containers: API, Web, Redis; SQL Server
is external):

```bash
cp server/.env.example server/.env   # set DB_CONNECTION_STRING, JWT_SECRET_KEY, AGENT_API_KEY, ...
docker compose up -d --build
```

Or deploy over SSH from a workstation with `./deploy-server.ps1`. Database
migrations run automatically at startup — no manual step required.

Post-install checklist:

- [ ] Populate `server/.env` from `server/.env.example`.
- [ ] Sign in and change the default admin password (`admin` / `admin@inventory.local`).
- [ ] Distribute the thin-client agent configured with `AGENT_API_KEY` and `AGENT_SERVER_URL`.

## 🔐 Security

- JWT bearer auth (Authorization header or httpOnly cookie), BCrypt password
  hashing, and brute-force login lockout.
- Role-based and per-device authorization; agent endpoints gated by API key.
- Security headers, HTTPS redirection, and CORS restricted to the web client.

## 🙏 Contributors

Thanks to everyone who contributed to this release: the @MKSWORKS engineering team.

---

**Full changelog:** [CHANGELOG](../../CHANGELOG.md) · **Download:** https://github.com/MKSWORKS/inventory/releases/tag/v1.0.0
