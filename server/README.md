# Server Deployment Configuration

This folder contains configuration files for deploying the Computer Inventory server infrastructure.

## Quick Start

1. Copy `.env.example` to `.env` and update the values
2. Run `docker-compose up -d` from the root directory

## Components

- **SQL Server 2025**: Database server
- **API**: ASP.NET Core Web API
- **Web**: Blazor WebAssembly frontend

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| SA_PASSWORD | SQL Server SA password | YourStrong@Passw0rd |
| JWT_SECRET_KEY | Secret key for JWT tokens | (required) |
| AGENT_API_KEY | API key for thin client agents | (optional) |

## Ports

| Service | Port |
|---------|------|
| SQL Server | 1433 |
| API | 5000 |
| Web | 5001 |

## SSL/TLS Configuration

For production, configure SSL certificates in the `certs` folder and update the docker-compose.yml.
