# Computer Inventory Management System

A comprehensive computer inventory management system for tracking 200+ devices with multi-user support, built with .NET 8, SQL Server 2025, and Docker.

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

## License

MIT License
