# User Stories & Use Cases

> **Status:** 🟢 Complete · **Owner:** MKSWORKS · **Last updated:** 2026-07-14

## Story Format

> **As a** {{persona}}, **I want** {{goal}}, **so that** {{benefit}}.

Each story has testable **acceptance criteria** in Given/When/Then (Gherkin)
form and traces to requirements in the [PRD](product-requirements-document.md)
(PR-#) and [SRS](software-requirements-specification.md) (FR-#).

**Personas:** **Admin** (IT Administrator), **Technician** (IT operations),
**Viewer** (Help Desk / Manager, read-only), **Agent** (loaner thin-client
system actor).

---

## Epic A: Authentication & Access Control

### US-1: Staff login

> As a **staff member**, I want to log in with my credentials, so that I get a
> token that authorizes my actions.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-1, PR-1

**Acceptance Criteria**

```gherkin
Scenario: Successful login
  Given a registered account with valid credentials
  When  I submit them to the login endpoint
  Then  a signed JWT is returned and I reach the dashboard

Scenario: Invalid credentials
  Given wrong credentials
  When  I attempt to log in
  Then  the API returns 401 and no token is issued
```

---

### US-2: Role-based access

> As an **Admin**, I want actions gated by role, so that users only do what their
> role permits.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-2, PR-1

**Acceptance Criteria**

```gherkin
Scenario: Viewer blocked from deleting
  Given a Viewer token
  When  a delete is attempted
  Then  the API returns 403

Scenario: Admin performs privileged action
  Given an Admin token
  When  a user or delete action is requested
  Then  it is authorized and succeeds
```

**Notes:** Roles — Admin, Technician, Viewer.

---

### US-3: Invite-based user management

> As an **Admin**, I want to invite users with a scoped code and department, so
> that onboarding is controlled.

**Priority:** Should · **Estimate:** 5 · **Traces:** FR-14, PR-7

**Acceptance Criteria**

```gherkin
Scenario: Register with valid invite code
  Given an Admin-issued invite code
  When  a new user registers with it
  Then  the account is created with the assigned role and department

Scenario: Expired or invalid code
  Given an expired or unknown invite code
  When  registration is attempted
  Then  it is rejected
```

---

## Epic B: Inventory Management

### US-4: Manage computer records

> As a **Technician**, I want to create, view, edit, and delete computer records,
> so that inventory stays accurate.

**Priority:** Must · **Estimate:** 5 · **Traces:** FR-3, PR-2

**Acceptance Criteria**

```gherkin
Scenario: Create a computer
  Given a Technician on the inventory page
  When  a computer with a unique serial is saved
  Then  it is persisted and returned with an ID

Scenario: Duplicate serial rejected
  Given a serial that already exists
  When  a new record uses it
  Then  the save is rejected with a validation error
```

---

### US-5: Search and filter inventory

> As a **Viewer**, I want to search and filter devices, so that I can find one
> quickly.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-4, PR-5

**Acceptance Criteria**

```gherkin
Scenario: Filter by status
  Given 200+ device records
  When  I filter by status = online
  Then  only online devices are listed

Scenario: Search by hostname or serial
  Given a known hostname or serial
  When  I search for it
  Then  the matching device is shown
```

---

### US-6: Status dashboard

> As a **Viewer**, I want a dashboard of device counts and status, so that I see
> the fleet at a glance.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-15, PR-5

**Acceptance Criteria**

```gherkin
Scenario: Dashboard summary
  Given devices with varied status
  When  the dashboard loads
  Then  online/offline and checked-in/out counts are shown
  And   the list returns within 500 ms p95 for 200+ devices
```

---

### US-7: Export inventory to CSV

> As a **Technician**, I want to export the filtered inventory to CSV, so that I
> can report and audit offline.

**Priority:** Could · **Estimate:** 2 · **Traces:** FR-16, PR-10

**Acceptance Criteria**

```gherkin
Scenario: Export filtered set
  Given an active filter on the inventory list
  When  I request CSV export
  Then  a CSV containing only the filtered rows is downloaded
```

---

## Epic C: Agent Auto-Discovery & Monitoring

### US-8: Agent auto-registration

> As a **Loaner Agent**, I want to auto-register the host's hardware on install,
> so that the device appears without manual entry.

**Priority:** Must · **Estimate:** 5 · **Traces:** FR-5, FR-6, PR-3

**Acceptance Criteria**

```gherkin
Scenario: Register on first run
  Given a valid agent API key
  When  the agent posts detected hardware
  Then  a record is created, or matched by serial, and returned

Scenario: Hardware fields populated
  Given the agent registers a device
  When  the record is read
  Then  serial, CPU, RAM, storage, and network fields are non-empty
```

---

### US-9: Heartbeat and online status

> As a **Loaner Agent**, I want to send periodic heartbeats, so that the device
> shows as online with a fresh last-seen time.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-7, PR-3

**Acceptance Criteria**

```gherkin
Scenario: Heartbeat updates status
  Given a registered agent
  When  a heartbeat is received (~5 min interval)
  Then  last-seen updates and status = online
```

---

### US-10: Offline detection

> As a **Technician**, I want devices marked offline when heartbeats stop, so
> that stale status is visible.

**Priority:** Must · **Estimate:** 2 · **Traces:** FR-8, PR-2

**Acceptance Criteria**

```gherkin
Scenario: No heartbeat past threshold
  Given no heartbeat for longer than the configured threshold
  When  status is evaluated
  Then  the device status = offline
```

---

### US-11: Offline queue and retry

> As a **Loaner Agent**, I want to queue reports when the server is unreachable,
> so that no data is lost.

**Priority:** Must · **Estimate:** 5 · **Traces:** FR-18, NFR-6

**Acceptance Criteria**

```gherkin
Scenario: Queue during outage
  Given the API is unreachable
  When  heartbeats or reports fail
  Then  they are queued locally

Scenario: Resend on recovery
  Given queued reports
  When  the API becomes reachable
  Then  the queue is flushed in order and cleared
```

---

## Epic D: Loaner Lifecycle

### US-12: Loaner check-out and check-in

> As a **Technician**, I want to check a loaner out to a user and back in, so
> that custody is tracked.

**Priority:** Must · **Estimate:** 5 · **Traces:** FR-9, PR-4

**Acceptance Criteria**

```gherkin
Scenario: Check out a loaner
  Given an available loaner
  When  it is checked out to a user
  Then  an event with actor and timestamp is stored and status = checked-out

Scenario: Check in a loaner
  Given a checked-out loaner
  When  it is checked in
  Then  an event is stored and status = checked-in
```

---

### US-13: Idle auto-checkout

> As an **Admin**, I want loaners auto-checked-out after an idle timeout, so that
> status reflects reality without manual steps.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-10, PR-4

**Acceptance Criteria**

```gherkin
Scenario: Idle beyond timeout
  Given a device idle longer than the configured timeout
  When  the auto-checkout job evaluates it
  Then  the device is checked out automatically and the event is logged
```

---

## Epic E: Assignment, Maintenance & Audit

### US-14: Assign device ownership

> As a **Technician**, I want to assign a device to a user and department, so
> that ownership is clear.

**Priority:** Should · **Estimate:** 3 · **Traces:** FR-11, PR-7

**Acceptance Criteria**

```gherkin
Scenario: Assign to user and department
  Given a Technician and a target device
  When  it is assigned to a user in a department
  Then  the ownership is recorded and shown on the device detail
```

---

### US-15: Maintenance scheduling and history

> As a **Technician**, I want to schedule and log maintenance, so that service
> history is preserved per device.

**Priority:** Should · **Estimate:** 5 · **Traces:** FR-12, PR-8

**Acceptance Criteria**

```gherkin
Scenario: Add maintenance entry
  Given a device
  When  a maintenance entry is added
  Then  it appears in the device's maintenance history with date and detail
```

---

### US-16: Audit trail

> As an **Admin**, I want every inventory change logged, so that I can see who
> changed what and when.

**Priority:** Must · **Estimate:** 3 · **Traces:** FR-13, PR-6

**Acceptance Criteria**

```gherkin
Scenario: Change is audited
  Given any inventory record
  When  it is created, edited, or deleted
  Then  an audit entry captures actor, timestamp, and change detail
```

---

### US-17: Offline / overdue alerts

> As an **Admin**, I want alerts for offline or overdue devices, so that I can
> act before hardware is lost.

**Priority:** Could · **Estimate:** 3 · **Traces:** FR-17, PR-11

**Acceptance Criteria**

```gherkin
Scenario: Flag offline device
  Given a device offline longer than the threshold
  When  the alert job runs
  Then  the device is flagged as needing attention
```

---

## Use Case Detail

### UC-1: Loaner check-out / check-in

| Field | Description |
|-------|-------------|
| **Actor** | Technician (primary); Loaner Agent (auto-checkout) |
| **Preconditions** | Technician authenticated; device registered and available |
| **Trigger** | Technician checks a loaner out to a user |
| **Main Flow** | 1. Technician selects a device. 2. Chooses a user/department. 3. Confirms check-out. 4. System stores event (actor, timestamp) and sets status = checked-out. 5. On return, Technician checks it in; system logs the event and sets status = checked-in. |
| **Alternate Flows** | A1: Idle timeout exceeded → system auto-checks-out the device (US-13). |
| **Exceptions** | E1: Device already checked out → block with conflict message. E2: Server unreachable → agent queues the event and resends on recovery (US-11). |
| **Postconditions** | Custody state and full event history are recorded and audited. |

---

### UC-2: Agent auto-registration & heartbeat

| Field | Description |
|-------|-------------|
| **Actor** | Loaner Agent (Windows Service) |
| **Preconditions** | Agent installed with a valid API key; network reachable |
| **Trigger** | Agent service starts on the loaner PC |
| **Main Flow** | 1. Agent reads hardware via WMI (serial, CPU, RAM, storage, network). 2. Posts to the API with the API key. 3. System creates or matches a record by serial. 4. Agent sends a heartbeat every ~5 min; last-seen and online status update. |
| **Alternate Flows** | A1: Serial already exists → update the existing record instead of creating one. |
| **Exceptions** | E1: API unreachable → queue and retry (US-11). E2: Invalid API key → request rejected (401). |
| **Postconditions** | Device is registered with fresh hardware data and live status. |

## Backlog Summary

| ID | Story | Epic | Priority | Status |
|------|-------|------|----------|--------|
| US-1 | Staff login | A · Auth & Access | Must | To Do |
| US-2 | Role-based access | A · Auth & Access | Must | To Do |
| US-3 | Invite-based user management | A · Auth & Access | Should | To Do |
| US-4 | Manage computer records | B · Inventory | Must | To Do |
| US-5 | Search and filter inventory | B · Inventory | Must | To Do |
| US-6 | Status dashboard | B · Inventory | Must | To Do |
| US-7 | Export inventory to CSV | B · Inventory | Could | To Do |
| US-8 | Agent auto-registration | C · Agent & Monitoring | Must | To Do |
| US-9 | Heartbeat and online status | C · Agent & Monitoring | Must | To Do |
| US-10 | Offline detection | C · Agent & Monitoring | Must | To Do |
| US-11 | Offline queue and retry | C · Agent & Monitoring | Must | To Do |
| US-12 | Loaner check-out and check-in | D · Loaner Lifecycle | Must | To Do |
| US-13 | Idle auto-checkout | D · Loaner Lifecycle | Must | To Do |
| US-14 | Assign device ownership | E · Assignment & Audit | Should | To Do |
| US-15 | Maintenance scheduling and history | E · Assignment & Audit | Should | To Do |
| US-16 | Audit trail | E · Assignment & Audit | Must | To Do |
| US-17 | Offline / overdue alerts | E · Assignment & Audit | Could | To Do |
