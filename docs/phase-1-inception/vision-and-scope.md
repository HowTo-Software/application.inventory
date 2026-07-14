# Vision & Scope

> **Status:** 🟢 Complete · **Owner:** MKSWORKS · **Last updated:** 2026-07-14

## 1. Problem Statement

IT teams track 200+ computers and loaner devices in spreadsheets that quickly go
stale. Loaner devices go missing, current assignments are unknown, and
maintenance history is scattered. Manual audits are slow, error-prone, and never
current — costing staff time and lost hardware. IT administrators, technicians,
and help-desk staff feel this pain daily.

## 2. Vision Statement

For IT teams who manage large fleets of shared and loaner computers, the Computer
Inventory Management System is a self-hosted platform that provides a single,
real-time source of truth for device location, status, assignment, and
maintenance. Unlike spreadsheets and manual audits, it auto-discovers hardware
and reports status through lightweight agents — with no manual reconciliation.

## 3. Goals & Objectives

| # | Goal | Success Metric |
|---|------|----------------|
| G1 | Replace manual tracking with automated agent-based discovery | ≥ 95% of loaner devices auto-reporting via agent |
| G2 | Deliver accurate, real-time device status | Heartbeat freshness ≤ 5 min on active devices |
| G3 | Reduce lost or unaccounted devices | < 2% of devices with unknown location |
| G4 | Enable fast staff onboarding | < 30 min to first productive use |

## 4. Stakeholders

| Stakeholder | Role / Interest | Influence |
|-------------|-----------------|-----------|
| IT Administrators | Own and operate the system; manage users and agents | High |
| IT Technicians | Day-to-day inventory, assignments, maintenance | High |
| Help Desk / Managers | Read-only lookup of status, location, assignment | Medium |
| Loaner Recipients (end users) | Receive and return loaner devices | Low |
| MKSWORKS (dev / owner) | Build, deploy, and maintain the system | High |

## 5. Target Users & Personas

- **IT Administrator:** full control; manages users, configures agents, deletes
  and audits records. Frustrated by stale data and manual audits.
- **Technician:** day-to-day operations; views/edits inventory, logs
  maintenance, assigns devices. Needs quick, reliable device lookup.
- **Viewer (Help Desk / Manager):** read-only status and location lookup. Wants
  answers without touching data.
- **Loaner Agent (system actor):** Windows Service that auto-registers hardware
  and reports heartbeats and check-in/out with no human interaction.

## 6. Scope

### In Scope
- JWT-authenticated REST API with role-based access (Admin / Technician / Viewer).
- Computer inventory tracking (serial, hostname, IP, location, status, assignment).
- Windows Service thin-client agent: auto-registration, hardware detection, heartbeat.
- Automatic loaner check-in/out with idle-timeout auto-checkout.
- Blazor WebAssembly dashboard to search, filter, and edit inventory.
- User management with departments, maintenance history, and full audit trail.
- Docker-based deployment (API, Web, SQL Server 2025).

### Out of Scope
- Non-Windows agents (macOS / Linux) and mobile-native apps.
- Software-license and asset-lifecycle management beyond hardware inventory.
- Remote control / remote support tooling (handled separately, e.g., RustDesk).
- Procurement, purchasing, and financial / depreciation tracking.

### Future Considerations
- macOS / Linux loaner agents.
- SSO / Active Directory integration for staff login.
- Alerts for offline or overdue devices.
- Expanded reporting and CSV export.

## 7. Assumptions & Constraints

- **Assumptions:** loaner PCs run Windows and permit service install (script/GPO);
  agents have HTTPS connectivity to the API; self-hosted Docker infrastructure is
  available.
- **Constraints:** agents are Windows-only (.NET 8 Windows Service); fixed stack
  of .NET 8, SQL Server 2025, Blazor WASM, and Docker; self-hosted (no external
  cloud dependency); secrets via environment variables and TLS in transit.

## 8. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Endpoint security / AV blocks the agent | M | M | Code-sign agent; document GPO allowlist |
| Network outage yields stale device status | M | M | Agent retries and queues reports offline |
| Incomplete agent adoption across fleet | M | H | Mass-deploy via GPO; monitor coverage |
| SQL data corruption or loss | L | H | Regular backups, CHECKDB, restore procedures |

## 9. Success Criteria

- All **Must** requirements delivered, CI green, no open critical/high defects.
- ≥ 95% of loaner devices auto-reporting with status freshness ≤ 5 min.
- < 2% of devices with unknown location within one quarter of rollout.
- Docker Compose brings up API, Web, and SQL Server with a seeded admin login;
  the agent auto-registers on a clean loaner PC.
