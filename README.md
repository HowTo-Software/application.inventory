# Computer Inventory Management System

> A comprehensive system for tracking 200+ computers and loaner devices with multi-user, role-based access — built with .NET 8, SQL Server 2025, Blazor WebAssembly, and Docker.

[![CI](https://github.com/MKSWORKS/inventory/actions/workflows/ci.yml/badge.svg)](https://github.com/MKSWORKS/inventory/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Proprietary-red.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![SQL Server](https://img.shields.io/badge/SQL%20Server-2025-CC2927.svg)](https://www.microsoft.com/sql-server)
[![Docker](https://img.shields.io/badge/Docker-ready-2496ED.svg)](https://www.docker.com/)

---

## Overview

The Computer Inventory Management System provides end-to-end tracking of desktops, laptops, and loaner PCs across an organization. A central ASP.NET Core API backs a Blazor WebAssembly dashboard for staff, while lightweight Windows Service agents on loaner machines auto-register hardware, send periodic heartbeats, and handle check-in/check-out automatically.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Web Browser                               │
│                    (Blazor WebAssembly)                         │
└─────────────────────────┬───────────────────────────────────────┘
                          │ HTTPS
┌─────────────────────────▼───────────────────────────────────────┐
│                      API Server                                  │
│                   (ASP.NET Core 8)                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ Auth (JWT)  │  │ Inventory   │  │ Agent Endpoints         │  │
│  │ Controller  │  │ Controllers │  │ (for Thin Clients)      │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└─────────────────────────┬───────────────────────────────────────┘
                          │
┌─────────────────────────▼───────────────────────────────────────┐
│                    SQL Server 2025                               │
│                      (Docker)                                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    Loaner Computers                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Thin Client Agent                            │   │
│  │         (Windows Service - .NET 8)                        │   │
│  │  • Auto-registration  • Heartbeat  • Hardware detection   │   │
│  │  • Check-in/out       • Idle timeout auto-checkout        │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
Computer_Inventory/
├── src/
│   ├── ComputerInventory.Api/          # REST API (ASP.NET Core)
│   ├── ComputerInventory.Core/         # Domain entities & interfaces
│   ├── ComputerInventory.Infrastructure/# Data access (EF Core)
│   ├── ComputerInventory.Web/          # Web UI (Blazor WASM)
│   └── ComputerInventory.ThinClient/   # Loaner PC agent (Windows Service)
├── server/                             # Server deployment configs
├── thin-client/                        # Thin client deployment scripts
├── docker-compose.yml                  # Production deployment
└── docker-compose.dev.yml              # Development (SQL Server only)
```

## Features

### Server Components
- **REST API** with JWT authentication
- **Role-based access control** (Admin, Technician, Viewer)
- **Computer inventory tracking** (serial, hostname, IP, location, status)
- **User management** with department assignment
- **Maintenance scheduling** and history tracking
- **Audit trail** for all changes

### Thin Client Agent
- **Automatic registration** with the server
- **Hardware detection** (serial, CPU, RAM, storage, network)
- **Periodic heartbeat** to report online status
- **Check-in/check-out** tracking
- **Auto check-out** after idle timeout
- **Runs as Windows Service**

## Quick Start

### Prerequisites
- Docker Desktop
- .NET 8 SDK
- Visual Studio 2022 or VS Code

### 1. Start Development Database

```powershell
docker-compose -f docker-compose.dev.yml up -d
```

### 2. Run the API

```powershell
cd src/ComputerInventory.Api
dotnet run
```

API will be available at: `https://localhost:5000`
Swagger UI: `https://localhost:5000/swagger`

### 3. Run the Web UI

```powershell
cd src/ComputerInventory.Web
dotnet run
```

Web UI will be available at: `https://localhost:5001`

### Default Login
- **Username:** admin
- **Password:** Admin123!

## Production Deployment

### Full Stack with Docker

```powershell
docker-compose up -d
```

This starts:
- SQL Server 2025 on port 1433
- API on port 5000
- Web UI on port 5001

### Environment Variables

Copy `server/.env.example` to `.env` and configure:

```env
SA_PASSWORD=YourSecurePassword!
JWT_SECRET_KEY=YourJWTSecretKeyAtLeast32Chars!
AGENT_API_KEY=YourAgentApiKey
```

## Thin Client Deployment

### Build the Agent

```powershell
cd thin-client
.\build-package.ps1
```

### Install on Loaner Computers

```powershell
# Copy deploy/ComputerInventoryAgent.zip to target
# Extract and run as Administrator:
.\install.ps1 -ServerUrl "https://inventory-server:5000" -ApiKey "your-api-key"
```

### Group Policy Deployment
See [thin-client/README.md](thin-client/README.md) for GPO deployment instructions.

## API Endpoints

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/auth/login | User login |
| POST | /api/auth/register | Register new user (Admin only) |

### Computers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | /api/computers | List all computers |
| GET | /api/computers/{id} | Get computer details |
| POST | /api/computers | Add new computer |
| PUT | /api/computers/{id} | Update computer |
| DELETE | /api/computers/{id} | Delete computer (Admin) |
| PATCH | /api/computers/{id}/status | Update status |
| PATCH | /api/computers/{id}/assign | Assign to user |

### Agent (Thin Client)
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/agent/register | Register/update computer |
| POST | /api/agent/heartbeat | Send heartbeat |
| POST | /api/agent/checkin | Check-in/check-out |

## User Roles

| Role | Permissions |
|------|------------|
| **Admin** | Full access, user management |
| **Technician** | View/edit inventory, maintenance |
| **Viewer** | Read-only access |

## Configuration

### API Configuration (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ComputerInventory;..."
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key",
    "Issuer": "ComputerInventoryApi",
    "Audience": "ComputerInventoryClient",
    "ExpirationInMinutes": 60
  },
  "AgentSettings": {
    "ApiKey": "optional-api-key",
    "HeartbeatIntervalMinutes": 5,
    "CheckInIntervalMinutes": 15,
    "IdleTimeoutMinutes": 30
  }
}
```

### Thin Client Configuration

```json
{
  "ApiSettings": {
    "BaseUrl": "https://inventory-server:5000",
    "ApiKey": "your-api-key"
  },
  "AgentSettings": {
    "HeartbeatIntervalMinutes": 5,
    "CheckInIntervalMinutes": 15,
    "IdleTimeoutMinutes": 30,
    "EnableAutoCheckOut": true
  }
}
```

## Development

### Database Migrations

```powershell
cd src/ComputerInventory.Api
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Running Tests

```powershell
dotnet test
```

## Documentation

Additional documentation lives in the [`docs/`](docs/) folder:

| Document | Description |
|----------|-------------|
| [API Changelog](docs/API_CHANGELOG.md) | Notable API changes and version history |
| [Database Schema (DDL)](docs/ComputerInventory_DDL.sql) | Full SQL Server schema definition |
| [Stored Procedures](docs/STORED_PROCEDURES.md) | Reference for database stored procedures |
| [Package Cleanup Strategy](docs/PACKAGE_CLEANUP_STRATEGY.md) | Update-package retention and cleanup design |
| [Service Tracker Prebuilt Images](docs/SERVICE_TRACKER_PREBUILT_IMAGES_FEATURE.md) | Prebuilt image feature notes |
| [Roadmap / TODO](docs/TODO.md) | Planned work and open items |

Database migrations are tracked in [`server/migrations/`](server/migrations/).

## Contributing

Contributions are welcome! Before opening a pull request:

1. Open an issue using the [issue templates](.github/ISSUE_TEMPLATE/) to discuss the change.
2. Follow the checklist in the [pull request template](.github/pull_request_template.md).
3. Ensure `dotnet build` and `dotnet test` pass locally.

## Security

This is proprietary software. Please report vulnerabilities privately — see the [Security Policy](.github/SECURITY.md). Never commit secrets such as `.env` files, connection strings, JWT signing keys, or `.pfx` / `.p12` certificates.

## Changelog

See the [API Changelog](docs/API_CHANGELOG.md) for a history of notable API changes.

## License

This project is **proprietary software**. Copyright (c) 2026 MKS. All rights reserved.
Unauthorized copying, distribution, or modification is prohibited without written permission. See [LICENSE](LICENSE) for full terms.
