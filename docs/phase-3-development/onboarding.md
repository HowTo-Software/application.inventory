# Developer Onboarding Guide

> **Status:** 🟢 Complete · **Owner:** Engineering · **Last updated:** 2026-07-14

Welcome to the **Computer Inventory Management System**! This guide gets you from
zero to a running dev environment and your first contribution.

## 1. Prerequisites

| Tool | Version | Install |
|------|---------|---------|
| .NET SDK | 8.0 & 9.0 (9.0.x pinned by [`global.json`](../../global.json), builds `net8.0`) | https://dotnet.microsoft.com/download |
| Docker Desktop | latest | https://www.docker.com/products/docker-desktop |
| Git | latest | https://git-scm.com |
| IDE | Visual Studio 2022 or VS Code | https://visualstudio.microsoft.com |

## 2. Get the Code

```bash
git clone https://github.com/MKSWORKS/inventory.git
cd inventory
```

## 3. Install Dependencies

```powershell
dotnet restore ComputerInventory.sln
```

## 4. Configure Environment

Copy the sample env file and set the dev database password:

```powershell
Copy-Item server/.env.example .env
# Edit .env: set SA_PASSWORD (used by the dev SQL Server container)
```

Provide the API's secrets via user-secrets (never commit them):

```powershell
cd src/ComputerInventory.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,14330;Database=device_management;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=True;"
dotnet user-secrets set "JwtSettings:SecretKey" "<random 64-char secret>"
```

| Variable | Required | Description |
|----------|----------|-------------|
| `SA_PASSWORD` | Yes | SA password for the dev SQL Server container |
| `ConnectionStrings:DefaultConnection` | Yes | API → SQL Server (host port `14330` in dev) |
| `JwtSettings:SecretKey` | Yes | JWT signing key, ≥ 32 chars |
| `AgentSettings:ApiKey` | For agents | Shared key thin-client agents authenticate with |

> ⚠️ Never commit secrets. Use `dotnet user-secrets` locally and a secret manager for shared credentials.

## 5. Run Locally

```powershell
# Start dev dependencies: SQL Server 2025 (:14330) + Redis (:6379)
docker-compose -f docker-compose.dev.yml up -d

# API — https://localhost:5000 (Swagger at /swagger)
cd src/ComputerInventory.Api
dotnet run

# Web UI — https://localhost:5001 (run in a second terminal)
cd src/ComputerInventory.Web
dotnet run
```

Sign in with the default admin account: **admin** / **Admin123!**

## 6. Run the Tests

```powershell
dotnet test ComputerInventory.sln
```

Integration tests boot a SQL Server container via Testcontainers, so **Docker
Desktop must be running**. See the [Test Plan](../phase-4-testing/test-plan.md)
for the full strategy.

## 7. Project Structure

```
src/
  ComputerInventory.Api/                 REST API (ASP.NET Core 8, controllers + services)
  ComputerInventory.Core/                Domain entities & interfaces
  ComputerInventory.Infrastructure/      EF Core, repositories, stored-procedure access
  ComputerInventory.Contracts/           Shared DTOs (API + Web + agent)
  ComputerInventory.Web/                 Blazor WebAssembly UI
  ComputerInventory.ThinClient/          Loaner-PC agent (Windows Service)
  ComputerInventory.ThinClient.Updater/  Standalone self-update executable
  ComputerInventory.Tests/               xUnit unit + integration tests
server/                                  Deployment configs & DB migrations
docs/                                    Documentation (you are here)
```

## 8. Make Your First Change

1. Read the [Coding Standards](coding-standards.md).
2. Follow the [Git Workflow](git-workflow.md) to branch and commit.
3. Open a PR per [CONTRIBUTING](../../CONTRIBUTING.md).

## 9. Troubleshooting

| Problem | Fix |
|---------|-----|
| API can't connect to SQL Server | Confirm the dev DB is up (`docker-compose -f docker-compose.dev.yml ps`) and the connection string uses `localhost,14330`. |
| Integration tests won't start | Start Docker Desktop — Testcontainers needs it. |
| SDK not found / wrong version | Install the SDK pinned in `global.json` (9.0.x); verify with `dotnet --info`. |
| JWT/secret error on startup | Set `JwtSettings:SecretKey` (≥ 32 chars) via user-secrets. |
| Port 5000/5001 already in use | Stop the conflicting process or override `ASPNETCORE_URLS`. |

## 10. Who to Ask

- **Team chat:** MKSWORKS Engineering channel.
- **Maintainers / issues:** [github.com/MKSWORKS/inventory/issues](https://github.com/MKSWORKS/inventory/issues).
