# Diagrams

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14
>
> Prefer **text-based diagrams** (Mermaid) so they live in version control and
> diff cleanly. Export images only when needed for external audiences.

## System Context

```mermaid
flowchart LR
    admin([Admin / Technician])
    subgraph server[Inventory Server]
      web[Web App<br/>Blazor WebAssembly]
      api[API<br/>ASP.NET Core]
      redis[(Redis<br/>cache + data-protection keys)]
    end
    db[(SQL Server<br/>device_management)]
    agent[Thin-Client Agent<br/>Windows service]
    relay[RustDesk Relay<br/>+ SSLH]
    geo[(IP Geolocation API)]

    admin -->|browser| web
    web -->|HTTPS/JSON + JWT| api
    api --> db
    api --> redis
    agent -->|HTTPS + X-API-Key<br/>heartbeat & telemetry| api
    api -->|resolve public IP| geo
    agent -.->|remote desktop| relay
    admin -.->|remote control| relay
```

## Sequence — Agent Check-in & Telemetry

```mermaid
sequenceDiagram
    participant Ag as Thin-Client Agent
    participant API as API
    participant DB as SQL Server
    participant Geo as Geo-IP

    Ag->>API: POST /api/agent/register (serial, API key)
    API->>DB: upsert computer + agent status
    loop every heartbeat_interval_minutes
        Ag->>API: POST /api/agent/heartbeat (version, user, IP)
        API->>DB: update last_seen, is_online
        opt public IP stale
            API->>Geo: resolve location
            Geo-->>API: city / country / lat-long
            API->>DB: upsert computer_geo_locations
        end
        API-->>Ag: flags (forceUpdate, forceReinstall, checkin)
        opt telemetry due
            Ag->>API: POST /api/systemdetails/submit
            Ag->>API: POST /api/restorepoint (snapshot)
            API->>DB: persist system_details / restore_point
        end
        opt forceUpdate
            Ag->>API: GET /api/agent/check-update
            API-->>Ag: AgentUpdateInfo | 404
            Ag->>Ag: download, verify (SHA-256), apply via Updater
            Ag->>API: POST /api/agent/update-progress
        end
    end
```

## Component Diagram

Clean-architecture layers; `Contracts` (DTOs) is shared by the Web app and agent.

```mermaid
flowchart TB
    subgraph clients[Clients]
      Web[ComputerInventory.Web<br/>Blazor WASM]
      Agent[ComputerInventory.ThinClient<br/>+ .Updater]
    end
    subgraph apihost[ComputerInventory.Api]
      Ctrl[Controllers]
      Svc[Services<br/>Package, GeoLocation, Auth, Health]
    end
    subgraph domain[ComputerInventory.Core]
      Ent[Entities]
      Intf[Interfaces]
    end
    Infra[ComputerInventory.Infrastructure<br/>Repositories + ApplicationDbContext]
    Contracts[ComputerInventory.Contracts<br/>DTOs]
    DB[(SQL Server<br/>stored procedures)]
    Redis[(Redis)]

    Web -->|HTTPS/JSON| Ctrl
    Agent -->|HTTPS/JSON| Ctrl
    Ctrl --> Svc --> Intf
    Infra -. implements .-> Intf
    Svc --> Infra --> DB
    Svc --> Redis
    Infra --> Ent
    Web --> Contracts
    Agent --> Contracts
    Ctrl --> Contracts
```

## State Machine — Package Version Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Draft: upload
    Draft --> Published: publish
    Published --> Active: activate
    Active --> Deprecated: deprecate / superseded
    Deprecated --> Active: rollback
    Deprecated --> Archived: archive
    Active --> Archived: archive
    Archived --> [*]
    note right of Active
        Exactly one Active package
        per (package_type, channel)
    end note
```

## Deployment Diagram

```mermaid
flowchart TB
    browser[Admin Browser]
    subgraph host[Docker Host · network INV-MKS-Default]
      webc[INV-MKS-Web<br/>nginx · :5011 to 8080]
      apic[INV-MKS-API<br/>:5010 to 8080]
      redisc[(INV-MKS-Redis<br/>:6379)]
    end
    sql[(SQL Server<br/>external · 1433)]
    subgraph relaysrv[Linux Relay]
      sslh[SSLH · :21118]
      rd[RustDesk hbbs/hbbr]
    end
    subgraph managed[Managed Computers]
      agent[Thin-Client Agent]
    end

    browser -->|:5011| webc
    webc -->|:5010| apic
    apic --> redisc
    apic -->|:1433| sql
    agent -->|HTTPS heartbeat| sslh
    sslh -->|HTTP| apic
    sslh -->|other protocols| rd
    agent -.->|remote desktop| rd
```

## Diagram Index

| Diagram | Purpose | Source |
|---------|---------|--------|
| System Context | Actors, apps, and external dependencies | this file |
| Sequence — Agent Check-in | Register → heartbeat → telemetry → update | this file |
| Component | Clean-architecture layers and project references | this file |
| State Machine — Package Lifecycle | Package version status transitions | this file |
| Deployment | Docker services, relay, and agent topology | this file |
| Auto-Update flow | Package resolve/download/apply detail | [technical-design.md](technical-design.md) |
| ER diagram | Database entities and relationships | [data-model.md](data-model.md) |
