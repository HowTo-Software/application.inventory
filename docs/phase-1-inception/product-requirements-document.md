# Product Requirements Document (PRD)

> **Status:** 🟡 Draft · **Owner:** MKSWORKS · **Last updated:** 2026-07-14

## 1. Summary

The Computer Inventory Management System is a self-hosted platform for tracking
200+ organizational computers and loaner devices with multi-user, role-based
access. It pairs an ASP.NET Core 8 REST API and a Blazor WebAssembly dashboard
with lightweight Windows Service agents that auto-register hardware, send
periodic heartbeats, and automate loaner check-in/out — giving IT staff a
single, real-time source of truth for device location, status, assignment, and
maintenance history.

## 2. Background & Context

IT teams currently track computers in spreadsheets that quickly go stale: loaner
devices go missing, current assignments are unknown, and maintenance history is
scattered. Manual audits across 200+ machines are slow and error-prone. This
system replaces manual tracking with automated agent-based discovery and a live
dashboard, eliminating the reconciliation effort and reducing lost hardware.
Link to [Vision & Scope](vision-and-scope.md).

## 3. Objectives & Key Results (OKRs)

| Objective | Key Result | Target |
|-----------|-----------|--------|
| Replace manual inventory tracking | Loaner devices auto-reporting via agent | ≥ 95% |
| Deliver accurate real-time status | Heartbeat freshness on active devices | ≤ 5 min old |
| Reduce lost / unaccounted devices | Devices with unknown location | < 2% |
| Enable fast staff onboarding | Time to first productive use | < 30 min |

## 4. Personas & Use Cases

- **IT Administrator** — full control: manages users, configures agents, deletes
  and audits records.
- **Technician** — day-to-day operations: views/edits inventory, logs
  maintenance, assigns devices to users.
- **Viewer (Help Desk / Manager)** — read-only lookup of device status,
  location, and assignment.
- **Loaner Agent (system actor)** — Windows Service that auto-registers
  hardware and reports heartbeats and check-in/out.

Link to detailed [User Stories](user-stories.md).

## 5. Requirements

Prioritized with [MoSCoW](https://en.wikipedia.org/wiki/MoSCoW_method):
**M**ust / **S**hould / **C**ould / **W**on't.

| ID | Requirement | Priority | Notes |
|----|-------------|----------|-------|
| PR-1 | JWT-authenticated REST API with role-based access (Admin/Technician/Viewer) | Must | |
| PR-2 | Track computers: serial, hostname, IP, location, status, assigned user | Must | |
| PR-3 | Thin-client agent auto-registers hardware and sends periodic heartbeats | Must | Windows Service |
| PR-4 | Automatic loaner check-in/out with idle-timeout auto-checkout | Must | |
| PR-5 | Blazor WebAssembly dashboard to search, filter, and edit inventory | Must | |
| PR-6 | Audit trail of all inventory changes | Must | |
| PR-7 | User management with department assignment | Should | |
| PR-8 | Maintenance scheduling and history | Should | |
| PR-9 | Docker-based deployment (API, Web, SQL Server) | Should | |
| PR-10 | Inventory reporting / CSV export | Could | |
| PR-11 | Alerts for offline or overdue devices | Could | |
| PR-12 | Non-Windows agents and mobile-native apps | Won't | This release |

## 6. User Experience

- **Key flows:** login → dashboard → search/filter device → view/edit detail →
  assign or check-out; agent install → auto-register → live status.
- Dashboard defaults to a filterable device table with clear status indicators
  (online/offline, checked-in/out).
- **Accessibility:** keyboard-navigable; WCAG 2.1 AA color contrast.
- **Content:** plain-language status labels; destructive actions (delete)
  require confirmation and are Admin-only.

## 7. Non-Functional Requirements

- **Performance:** dashboard list p95 < 500 ms for 200+ devices; agent heartbeat
  every 5 min.
- **Scalability:** support 200+ concurrent agents and 500+ records without redesign.
- **Availability:** server target 99.5% during business hours.
- **Security:** JWT auth, role-based authorization, agent API key, secrets via
  environment variables, TLS in transit.
- **Reliability:** agent retries and queues reports when the server is unreachable.

See the [SRS](software-requirements-specification.md) for detail.

## 8. Metrics & Instrumentation

- **Adoption:** % of loaner devices running the agent; number of active accounts.
- **Engagement:** dashboard sessions per week; heartbeats received per day.
- **Quality:** % devices with fresh (≤ 5 min) status; API error rate;
  auto-checkout accuracy.

## 9. Release Criteria

- All **Must** requirements implemented and covered by passing automated tests (CI green).
- Agent installs via script/GPO and auto-registers on a clean loaner PC.
- Docker Compose brings up API, Web, and SQL Server with a seeded admin login.
- No open critical/high defects; deployment guide and user guide published.

## 10. Open Questions

| Question | Owner | Due |
|----------|-------|-----|
| Should agents support macOS/Linux loaners? | MKSWORKS | TBD |
| What is the retention policy for audit and heartbeat history? | MKSWORKS | TBD |
| Is SSO/AD integration required for staff login? | MKSWORKS | TBD |

## 11. Out of Scope

- Non-Windows agents (macOS/Linux) and mobile-native apps.
- Software license and asset-lifecycle management beyond hardware inventory.
- Remote control / remote support tooling (handled separately, e.g., RustDesk).
- Procurement, purchasing, and financial/depreciation tracking.
