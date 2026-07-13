# Thin Client Deployment

This folder contains deployment scripts and configuration for the loaner computer thin client agent.

## Overview

The thin client is a Windows Service that runs on loaner computers and:
- Automatically registers the computer with the inventory server
- Sends periodic heartbeats to report online status
- Detects hardware changes and reports them
- Supports automatic check-out after idle timeout
- Reports current user information

## Installation

### Prerequisites
- Windows 10/11 or Windows Server 2016+
- .NET 8.0 Runtime (included in self-contained deployment)
- Network access to the inventory server

### Manual Installation

1. Copy the deployment package to the target computer
2. Run PowerShell as Administrator
3. Execute: `.\install.ps1`

### Silent Installation (for deployment tools)

```powershell
.\install.ps1 -ServerUrl "https://inventory-server:5000" -ApiKey "your-api-key" -Silent
```

### Uninstallation

```powershell
.\uninstall.ps1
```

## Configuration

Edit `appsettings.json` before installation or use command-line parameters:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://inventory-server:5000",
    "FallbackUrls": [
      "http://10.241.24.60:5010"
    ],
    "ApiKey": "YOUR_API_KEY_HERE"
  },
  "AgentSettings": {
    "CheckInIntervalMinutes": 15,
    "HeartbeatIntervalMinutes": 5,
    "ReportHardwareChanges": true,
    "EnableAutoCheckOut": true,
    "IdleTimeoutMinutes": 30
  }
}
```

### Multi-Network Support

The agent supports automatic URL failover for clients that roam between networks:

- **BaseUrl**: Primary server URL (e.g., public/external address)
- **FallbackUrls**: Array of fallback URLs to try if primary fails (e.g., local network address)

When deploying with `deploy-server.ps1`, use the `-ExternalUrl` parameter to configure both:

```powershell
.\deploy-server.ps1 -ServerHost 10.241.24.60 -ExternalUrl "inventory.company.com:5010"
```

This configures:
- Primary URL: `http://inventory.company.com:5010` (for external clients)
- Fallback URL: `http://10.241.24.60:5010` (for local network clients)

The agent automatically switches between URLs based on connectivity.

## Deployment via Group Policy

1. Build the MSI package: `.\build-msi.ps1`
2. Deploy via GPO Software Installation
3. Configure settings via GPO Registry preferences

## Troubleshooting

### View Service Status
```powershell
Get-Service "ComputerInventoryAgent"
```

### View Logs
```powershell
Get-EventLog -LogName Application -Source "ComputerInventoryAgent" -Newest 50
```

### Test Connectivity
```powershell
Test-NetConnection -ComputerName inventory-server -Port 5000
```
