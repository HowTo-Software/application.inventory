# Compliance & Licensing

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

**Context:** The Computer Inventory Management System is a **proprietary, self-hosted**
internal IT asset-tracking tool (see [LICENSE](../../LICENSE)). It is deployed on the
customer's own infrastructure (Docker Compose, external SQL Server). The **deploying
organization is the data controller**; MKS is the software vendor, not a data processor
of production records. It processes no payment or health data.

## 1. Applicable Regulations

| Regulation / Standard | Applies? | Why | Owner |
|-----------------------|----------|-----|-------|
| GDPR (EU privacy) | ⚠️ Conditional | Staff account PII (name, email) and device IP/geolocation are personal data; applies if deployed for EU staff. Deploying org is controller. | Deploying org |
| CCPA/CPRA (California) | ⚠️ Conditional | CA employee + device data; typically the B2B/employee context. | Deploying org |
| HIPAA (US health) | ❌ No | No PHI collected or processed. | — |
| PCI DSS (payments) | ❌ No | No cardholder data; product handles no payments. | — |
| SOC 2 | ❌ No | Self-hosted, not offered as a SaaS; align to the deploying org's controls. | Engineering |
| ISO 27001 | ⚪ Optional | Maps into the deploying org's ISMS if one exists. | Deploying org |
| WCAG (accessibility) | ✅ Yes | Blazor WASM staff dashboard; target 2.1 AA (not yet formally audited). | Engineering |

## 2. Data Privacy

- **Legal basis:** legitimate interest / contract (IT asset management & security);
  audit logging under legal obligation where applicable. No consent flows — all users
  are provisioned staff accounts (Admin/Technician/Viewer).
- **Data subject rights:** handled by the deploying org. Records are soft-deleted
  (`is_deleted` / `deleted_at`); hard erasure via cleanup stored procedures.
- **Retention & deletion:** enforced by `DatabaseMaintenanceService` + `sp_cleanup_*`
  procedures — see [Data Model](../phase-2-design/data-model.md).
- **Sub-processors / external data flows:** the app ships default integrations that
  send data off-host — **ip-api.com** (device IP → geolocation lookup),
  **ipinfo.io** (agent self-reports public IP), **tile.openstreetmap.org** (map tiles,
  no PII). A DPA (or self-hosted replacement) is required before production use if
  personal data is in scope.

| Data Category | Purpose | Legal Basis | Retention |
|---------------|---------|-------------|-----------|
| Staff account PII (username, email, name, department, role) | Authentication, RBAC, audit attribution | Legitimate interest / contract | Life of account; soft-deleted |
| Credentials (BCrypt password hash) | Authentication | Legitimate interest | Life of account |
| Device inventory (serial, hostname, MAC, IP, hardware + 16 system-detail facets) | Asset management | Legitimate interest | Live for asset life; `system_details` pruned at 30 days |
| Device geolocation (IP → lat/long via ip-api.com) | Asset location / loss recovery | Legitimate interest | `computer_geo_locations`, count-capped per device |
| Audit logs & histories (who/what/when) | Security & accountability | Legal obligation / legitimate interest | `computer_histories` 365 days |
| Restore points / maintenance records | Operations | Legitimate interest | Restore points 90 days |

## 3. Third-Party / Open-Source Licenses

**Policy:** permissive licenses (MIT / Apache-2.0 / BSD) are pre-approved; copyleft
(GPL / LGPL / AGPL) requires review. The current dependency tree is **100% permissive —
no copyleft present**, consistent with proprietary distribution.

| Dependency | License | Compatible? | Notes |
|------------|---------|-------------|-------|
| Microsoft.* / System.* / EF Core / ASP.NET Core / SignalR | MIT | ✅ | Runtime, auth, data access |
| BCrypt.Net-Next | MIT | ✅ | Password hashing |
| Serilog (+ sinks, Formatting.Compact) | Apache-2.0 | ✅ | Structured logging |
| FluentValidation.AspNetCore | Apache-2.0 | ✅ | Input validation |
| Polly / Polly.Core | BSD-3-Clause | ✅ | Resilience/retry |
| MailKit | MIT | ✅ | Email notifications |
| Swashbuckle.AspNetCore | MIT | ✅ | Swagger/OpenAPI |
| Blazored.LocalStorage | MIT | ✅ | JWT storage (Web) |
| EFCore.NamingConventions | Apache-2.0 | ✅ | snake_case mapping |
| xunit, Testcontainers.MsSql, coverlet | Apache-2.0 / MIT | ✅ | Test-only (not shipped) |

> **Service ToS note:** the default **ip-api.com** endpoint is the free tier, which is
> licensed for **non-commercial use only**. Commercial production deployments must move
> to a paid/self-hosted geolocation provider (`GeoLocation:BaseUrl` is configurable).

## 4. Accessibility

- **Target:** WCAG 2.1 Level AA for the Blazor WebAssembly dashboard (internal staff tool).
- **Testing approach:** manual review to date; no automated accessibility checks in CI.
  A formal AA audit is an open item (§7).

## 5. Audit & Evidence

- **Evidence stored in-repo:** [Threat Model](threat-model.md),
  [SECURITY.md](../../SECURITY.md), and CI run history
  (see [CI/CD](../phase-5-deployment/ci-cd.md)).
- **Runtime evidence:** `audit_log` + `computer_histories` tables and Serilog structured
  logs (`logs/api-.log`, 30-day retention) provide the access/change trail.
- **Cadence:** reviewed per release and at least annually.

## 6. Responsibilities

| Area | Owner |
|------|-------|
| Privacy / DPO | Deploying organization (controller); Engineering for product features |
| Security | Engineering |
| Legal / licensing | MKS (vendor) + deploying organization |

## 7. Open Items

| Item | Owner | Due |
|------|-------|-----|
| Replace ip-api.com free tier (non-commercial ToS) with a licensed/self-hosted geo provider | Engineering | Before commercial prod use |
| Add automated SBOM / license & dependency scanning (no Dependabot/CodeQL today) | Engineering | Next hardening cycle |
| Complete formal WCAG 2.1 AA accessibility audit | Engineering | Next release |
| Confirm GDPR/CCPA applicability + DPAs for external IP-geolocation flows (ip-api.com, ipinfo.io) | Deploying org | Pre-production |
| Rotate default admin credential (`admin` / `admin@inventory.local`) on first deploy | Deploying org | At deployment |
