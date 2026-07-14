# Test Cases

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Notable test cases for ComputerInventory, traced to the
[user stories](../phase-1-inception/user-stories.md) (US-#) and
[SRS](../phase-1-inception/software-requirements-specification.md) (FR-#).
Automated cases run in `ComputerInventory.Tests` (xUnit + Testcontainers.MsSql);
the referenced class is noted under **Automation**. Manual/E2E cases are marked
accordingly. IDs are stable; keep the [matrix](#traceability-matrix) in sync.

## Test Case Template

### TC-{{ID}}: {{Title}}

| Field | Value |
|-------|-------|
| **Requirement** | {{FR-1 / US-1}} |
| **Priority** | High / Medium / Low |
| **Type** | Functional / Regression / Security / … |
| **Preconditions** | {{state before test}} |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | {{do this}} | {{see this}} |
| 2 | {{do this}} | {{see this}} |

**Postconditions:** {{state after test}}

---

## Authentication & Access Control

### TC-001: Staff can log in with valid credentials

| Field | Value |
|-------|-------|
| **Requirement** | US-1 / FR-1 |
| **Priority** | High |
| **Type** | Functional |
| **Preconditions** | A registered account exists (e.g. `admin`) |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Navigate to `/login` | Login form is displayed |
| 2 | Enter valid username + password | Fields accept input |
| 3 | Submit `POST /api/auth/login` | `200`, signed JWT returned; `access_token` httpOnly cookie set; dashboard loads |

**Postconditions:** User is authenticated with a valid session.

---

### TC-002: Login is rejected for invalid credentials

| Field | Value |
|-------|-------|
| **Requirement** | US-1 / FR-1 |
| **Priority** | High |
| **Type** | Security / Negative |
| **Preconditions** | Login endpoint reachable |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | `POST /api/auth/login` with a wrong password | `401 Unauthorized`; no token issued |
| 2 | Inspect response body | Generic message; no user-existence disclosure |

**Postconditions:** No session created.

---

### TC-003: Repeated failed logins trigger lockout

| Field | Value |
|-------|-------|
| **Requirement** | US-1 / FR-1, NFR-Security |
| **Priority** | High |
| **Type** | Security |
| **Preconditions** | `LoginRateLimiter` enabled |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Submit invalid credentials past the configured threshold | Further attempts return `429`/lockout for the window |
| 2 | Wait for the window to elapse, then log in valid | Login succeeds |

**Postconditions:** Brute-force attempts throttled without locking legitimate users permanently.

---

### TC-004: Viewer is blocked from privileged actions

| Field | Value |
|-------|-------|
| **Requirement** | US-2 / FR-2 |
| **Priority** | High |
| **Type** | Security |
| **Preconditions** | A Viewer token is available |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Call `DELETE /api/computers/{id}` with a Viewer token | `403 Forbidden` |
| 2 | Call an Admin-only endpoint (e.g. `POST /api/users`) | `403 Forbidden` |

**Postconditions:** No state change; authorization enforced by policy (`AdminOnly`/`TechnicianOrAdmin`).

---

### TC-005: Admin performs privileged action successfully

| Field | Value |
|-------|-------|
| **Requirement** | US-2 / FR-2 |
| **Priority** | High |
| **Type** | Functional |
| **Preconditions** | An Admin token is available |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Call an Admin-only endpoint with an Admin token | Request is authorized (`2xx`) |

**Postconditions:** Privileged action completes.

---

### TC-006: Register with a valid invite code

| Field | Value |
|-------|-------|
| **Requirement** | US-3 / FR-14 |
| **Priority** | Medium |
| **Type** | Functional |
| **Preconditions** | Admin has issued a scoped invite code with role + department |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | `POST /api/auth/register` with the invite code | `201`; account created |
| 2 | Read the new user | Assigned role and department match the invite |

**Postconditions:** New user can log in with the assigned scope.

---

### TC-007: Expired or unknown invite code is rejected

| Field | Value |
|-------|-------|
| **Requirement** | US-3 / FR-14 |
| **Priority** | Medium |
| **Type** | Negative |
| **Preconditions** | An expired/invalid code |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | `POST /api/auth/register` with an expired/unknown code | Rejected (`400`/`403`); no account created |

**Postconditions:** No user created.

---

## Inventory Management

### TC-008: Create a computer with a unique serial

| Field | Value |
|-------|-------|
| **Requirement** | US-4 / FR-3 |
| **Priority** | High |
| **Type** | Functional |
| **Automation** | `ComputerRepositoryTests` |
| **Preconditions** | Technician authenticated; serial not in use |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Submit a computer with a unique serial | `201`; record persisted and returned with an ID |
| 2 | Read it back by ID/serial | Fields and navigation properties (model, status, type) populated |

**Postconditions:** Computer exists and is queryable.

---

### TC-009: Duplicate serial is rejected

| Field | Value |
|-------|-------|
| **Requirement** | US-4 / FR-3 |
| **Priority** | High |
| **Type** | Negative |
| **Preconditions** | A computer with serial `SN-001` already exists |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Create a new record reusing `SN-001` | Save rejected with a validation error; no duplicate created |

**Postconditions:** Serial uniqueness preserved.

---

### TC-010: Filter inventory by status

| Field | Value |
|-------|-------|
| **Requirement** | US-5 / FR-4 |
| **Priority** | High |
| **Type** | Functional |
| **Automation** | `ComputerRepositoryTests.GetAllAsync_FilterByStatus` |
| **Preconditions** | Devices with varied status exist |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Request the list filtered by `status = Unassigned` | Only matching devices returned |
| 2 | Request page 2 with the same filter | Pages do not overlap; total count consistent |

**Postconditions:** Filtered, paginated result returned.

---

### TC-011: Search by hostname or serial

| Field | Value |
|-------|-------|
| **Requirement** | US-5 / FR-4 |
| **Priority** | High |
| **Type** | Functional |
| **Automation** | `ComputerRepositoryTests` (search) |
| **Preconditions** | A known hostname/serial exists |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Search for a known hostname or serial | The matching device is returned |
| 2 | Search for a nonexistent term | Empty result, no error |

**Postconditions:** Search returns exact/partial matches.

---

### TC-012: Dashboard summary loads within budget

| Field | Value |
|-------|-------|
| **Requirement** | US-6 / FR-15, NFR-Performance |
| **Priority** | High |
| **Type** | Functional / Performance |
| **Preconditions** | 200+ device records with varied status |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Load the dashboard | Online/offline and checked-in/out counts shown |
| 2 | Measure list latency | Returns within 500 ms p95 for 200+ devices |

**Postconditions:** Fleet status visible at a glance.

---

### TC-013: Export filtered inventory to CSV

| Field | Value |
|-------|-------|
| **Requirement** | US-7 / FR-16 |
| **Priority** | Low |
| **Type** | Functional |
| **Preconditions** | An active filter on the inventory list |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Request CSV export with the filter applied | CSV downloads containing only filtered rows |
| 2 | Open the CSV | Header row + expected columns; row count matches the filter |

**Postconditions:** Offline report available.

---

## Agent Auto-Discovery & Monitoring

### TC-014: Agent auto-registers on first run

| Field | Value |
|-------|-------|
| **Requirement** | US-8 / FR-5, FR-6 |
| **Priority** | High |
| **Type** | Integration |
| **Preconditions** | Agent installed with a valid `X-API-Key` |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Agent reads hardware via WMI and `POST /api/agent/register` | Record created, or matched by serial, and returned |
| 2 | Read the record | Serial, CPU, RAM, storage, and network fields are non-empty |

**Postconditions:** Device appears in inventory without manual entry.

---

### TC-015: Invalid agent API key is rejected

| Field | Value |
|-------|-------|
| **Requirement** | US-8 / FR-5 |
| **Priority** | High |
| **Type** | Security / Negative |
| **Preconditions** | Agent endpoints reachable |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | `POST /api/agent/register` with a missing/invalid `X-API-Key` | `401 Unauthorized`; no record created |

**Postconditions:** Only authenticated agents can register.

---

### TC-016: Heartbeat updates online status

| Field | Value |
|-------|-------|
| **Requirement** | US-9 / FR-7 |
| **Priority** | High |
| **Type** | Functional |
| **Preconditions** | A registered agent with a valid session |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Agent sends `POST /api/agent/heartbeat` (~5 min interval) | Last-seen updates; status = online |

**Postconditions:** `computer_agent_statuses` reflects a fresh last-seen time.

---

### TC-017: Offline detection past threshold

| Field | Value |
|-------|-------|
| **Requirement** | US-10 / FR-8 |
| **Priority** | High |
| **Type** | Functional |
| **Automation** | `DatabaseMaintenanceRepositoryTests` (offline sweep) |
| **Preconditions** | A device with no heartbeat beyond the configured threshold |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | `DatabaseMaintenanceService` offline sweep runs | Stale device status set to offline |

**Postconditions:** Stale status is visible in the fleet view.

---

### TC-018: Offline queue flushes on recovery

| Field | Value |
|-------|-------|
| **Requirement** | US-11 / FR-18, NFR-6 |
| **Priority** | High |
| **Type** | Integration / Resilience |
| **Preconditions** | Agent running; API temporarily unreachable |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Trigger heartbeats/reports while the API is down | Items are queued locally; no data lost |
| 2 | Restore API connectivity | Queue is flushed in order and cleared |

**Postconditions:** No telemetry lost across an outage.

---

## Loaner Lifecycle

### TC-019: Check a loaner out and back in

| Field | Value |
|-------|-------|
| **Requirement** | US-12 / FR-9 |
| **Priority** | High |
| **Type** | Functional |
| **Preconditions** | An available loaner; Technician authenticated |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Check the device out to a user | Event stored (actor, timestamp); status = checked-out |
| 2 | Check the device back in | Event stored; status = checked-in |

**Postconditions:** Full custody history recorded and audited.

---

### TC-020: Checking out an unavailable loaner is blocked

| Field | Value |
|-------|-------|
| **Requirement** | US-12 / FR-9 |
| **Priority** | Medium |
| **Type** | Negative |
| **Preconditions** | A device already checked out |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Attempt to check out the already-checked-out device | Blocked with a conflict message; state unchanged |

**Postconditions:** Custody remains consistent.

---

### TC-021: Idle device is auto-checked-out

| Field | Value |
|-------|-------|
| **Requirement** | US-13 / FR-10 |
| **Priority** | High |
| **Type** | Functional |
| **Preconditions** | A device idle longer than the configured timeout |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Auto-checkout job evaluates the idle device | Device is checked out automatically; event logged |

**Postconditions:** Status reflects reality without manual steps.

---

## Assignment, Maintenance & Audit

### TC-022: Assign device ownership

| Field | Value |
|-------|-------|
| **Requirement** | US-14 / FR-11 |
| **Priority** | Medium |
| **Type** | Functional |
| **Preconditions** | A target device and a user/department |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Assign the device to a user in a department | Ownership recorded |
| 2 | Open the device detail | Owner and department shown |

**Postconditions:** Ownership is clear and queryable.

---

### TC-023: Add a maintenance entry

| Field | Value |
|-------|-------|
| **Requirement** | US-15 / FR-12 |
| **Priority** | Medium |
| **Type** | Functional |
| **Automation** | `MaintenanceRepositoryTests` |
| **Preconditions** | A device exists |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Add a maintenance entry with date + detail | Entry persisted |
| 2 | View the device's maintenance history | Entry appears with date and detail |

**Postconditions:** Service history preserved per device.

---

### TC-024: Inventory changes are audited

| Field | Value |
|-------|-------|
| **Requirement** | US-16 / FR-13 |
| **Priority** | High |
| **Type** | Functional / Security |
| **Preconditions** | Any inventory record |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Create, edit, and delete a record | Each change writes an audit entry |
| 2 | Read the audit log | Actor, timestamp, and change detail captured |

**Postconditions:** Complete who/what/when trail available.

---

### TC-025: Offline / overdue device is flagged

| Field | Value |
|-------|-------|
| **Requirement** | US-17 / FR-17 |
| **Priority** | Low |
| **Type** | Functional |
| **Preconditions** | A device offline longer than the threshold |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Alert job runs | Device is flagged as needing attention |

**Postconditions:** Admin can act before hardware is lost.

---

## Agent Auto-Update

### TC-026: Agent resolves and applies an active update

| Field | Value |
|-------|-------|
| **Requirement** | FR-Agent-Update (see [technical-design](../phase-2-design/technical-design.md)) |
| **Priority** | High |
| **Type** | Integration / Regression |
| **Preconditions** | A `PackageVersion` is **Active** on the agent's channel; distribution rule matches the device |

**Steps**

| # | Action | Expected Result |
|---|--------|-----------------|
| 1 | Agent calls `POST /api/agent/check-update` | Server resolves the target version per channel + rule |
| 2 | Agent downloads via `download-update` | SHA-256 of the package matches the manifest |
| 3 | Updater runs (SYSTEM): stop svc → backup → copy → start svc | Service restarts on the new version; `update-progress` ends `Completed` |
| 4 | Force a copy failure | Updater rolls back to the backup; status = `Failed`; prior version runs |

**Postconditions:** Agent is updated atomically or safely rolled back.

---

## Traceability Matrix

Status legend: ✅ Automated & passing · 🟡 Manual / verified · ⏳ Not run / planned.

| Requirement | Test Case(s) | Status |
|-------------|--------------|--------|
| US-1 / FR-1 | TC-001, TC-002, TC-003 | 🟡 Manual |
| US-2 / FR-2 | TC-004, TC-005 | 🟡 Manual |
| US-3 / FR-14 | TC-006, TC-007 | ⏳ Not run |
| US-4 / FR-3 | TC-008, TC-009 | ✅ Automated |
| US-5 / FR-4 | TC-010, TC-011 | ✅ Automated |
| US-6 / FR-15 | TC-012 | ⏳ Not run |
| US-7 / FR-16 | TC-013 | ⏳ Not run |
| US-8 / FR-5, FR-6 | TC-014, TC-015 | 🟡 Manual |
| US-9 / FR-7 | TC-016 | 🟡 Manual |
| US-10 / FR-8 | TC-017 | ✅ Automated |
| US-11 / FR-18 | TC-018 | 🟡 Manual |
| US-12 / FR-9 | TC-019, TC-020 | 🟡 Manual |
| US-13 / FR-10 | TC-021 | ⏳ Not run |
| US-14 / FR-11 | TC-022 | 🟡 Manual |
| US-15 / FR-12 | TC-023 | ✅ Automated |
| US-16 / FR-13 | TC-024 | 🟡 Manual |
| US-17 / FR-17 | TC-025 | ⏳ Not run |
| FR-Agent-Update | TC-026 | 🟡 Manual |
