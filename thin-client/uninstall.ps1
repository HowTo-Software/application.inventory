#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Uninstalls the Computer Inventory Agent.

.DESCRIPTION
    This script removes the Computer Inventory thin client agent from a computer.
    It handles both Scheduled Task (new) and Windows Service (legacy) installations.

.PARAMETER KeepConfig
    Keep configuration files after uninstallation.

.PARAMETER PreserveConfig
    Save configuration to a backup location so it can be restored on next install.

.PARAMETER Silent
    Run in silent mode without prompts.

.EXAMPLE
    .\uninstall.ps1

.EXAMPLE
    .\uninstall.ps1 -PreserveConfig
    # Saves config to C:\ProgramData\ComputerInventory\agent-config-backup.json
#>

param(
    [switch]$KeepConfig,
    [switch]$PreserveConfig,
    [switch]$Silent
)

$TaskName = "ComputerInventoryAgent"
$ServiceName = "ComputerInventoryAgent"  # Legacy - for migration
$InstallPath = "$env:ProgramFiles\ComputerInventory\Agent"
$PersistPath = "$env:ProgramData\ComputerInventory"

# Detect if running headless (non-interactive)
$IsHeadless = $Silent -or 
    ([Environment]::UserInteractive -eq $false) -or 
    ([Environment]::GetCommandLineArgs() -contains '-NonInteractive') -or
    ($null -eq $Host.UI.RawUI.KeyAvailable) -or
    ($env:TERM -eq 'dumb') -or
    ($null -ne $env:CI)

# Helper function for safe user prompt (handles headless mode)
function Wait-ForUserOrTimeout {
    param(
        [string]$Message = "Press any key to exit...",
        [int]$TimeoutSeconds = 30
    )
    
    if ($IsHeadless) {
        Write-Host "Running in headless mode - auto-continuing in $TimeoutSeconds seconds..." -ForegroundColor Gray
        Start-Sleep -Seconds $TimeoutSeconds
        return
    }
    
    try {
        Write-Host ""
        Write-Host $Message -ForegroundColor Gray
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    } catch {
        # If ReadKey fails (e.g., redirected input), just continue
        Write-Host "Cannot read key input, continuing..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 3
    }
}

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Computer Inventory Agent Uninstaller" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Save configuration before uninstall if requested
if ($PreserveConfig) {
    $configPath = Join-Path $InstallPath "appsettings.json"
    if (Test-Path $configPath) {
        Write-Host "Preserving configuration for next install..." -ForegroundColor Cyan
        
        if (!(Test-Path $PersistPath)) {
            New-Item -ItemType Directory -Path $PersistPath -Force | Out-Null
        }
        
        $persistedConfigPath = Join-Path $PersistPath "agent-config-backup.json"
        $config = Get-Content $configPath | ConvertFrom-Json
        
        $persistedConfig = @{
            SavedAt = (Get-Date).ToUniversalTime().ToString("o")
            AgentVersion = "uninstall"
            MachineName = $env:COMPUTERNAME
            Configuration = $config
        }
        
        $persistedConfig | ConvertTo-Json -Depth 10 | Set-Content $persistedConfigPath
        Write-Host "Configuration saved to: $persistedConfigPath" -ForegroundColor Green
    }
}

# Stop and remove Scheduled Task (new method)
$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($task) {
    Write-Host "Stopping scheduled task..." -ForegroundColor Yellow
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    
    # Kill any remaining process (check both old and new names for migration)
    $proc = Get-Process -Name "MKS.InventoryAgent", "ComputerInventory.ThinClient" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "Stopping agent process..." -ForegroundColor Yellow
        $proc | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 1
    }
    
    Write-Host "Removing scheduled task..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# Stop and remove Windows Service (legacy - for migration)
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping legacy Windows Service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "Removing legacy Windows Service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Kill any remaining process (just in case - check both old and new names)
$proc = Get-Process -Name "MKS.InventoryAgent", "ComputerInventory.ThinClient" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "Stopping remaining agent processes..." -ForegroundColor Yellow
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# Remove files
if (Test-Path $InstallPath) {
    Write-Host "Removing installation files..." -ForegroundColor Yellow
    
    if ($KeepConfig) {
        Get-ChildItem $InstallPath -Exclude "appsettings.json" | Remove-Item -Recurse -Force
    } else {
        Remove-Item -Path $InstallPath -Recurse -Force
    }
    
    # Remove parent directory if empty
    $parentPath = Split-Path $InstallPath -Parent
    if ((Get-ChildItem $parentPath -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
        Remove-Item -Path $parentPath -Force -ErrorAction SilentlyContinue
    }
}

# Clean up startup diagnostic log
$diagPath = "$env:ProgramData\ComputerInventory\service-startup.log"
if (Test-Path $diagPath) {
    Remove-Item $diagPath -Force -ErrorAction SilentlyContinue
}

# Clean up CI policy marker if exists
$ciMarker = "$env:ProgramData\ComputerInventory\ci-policy-installed.txt"
if (Test-Path $ciMarker) {
    Remove-Item $ciMarker -Force -ErrorAction SilentlyContinue
}

# Remove ProgramData folder if empty (but preserve config backup if it exists)
if (Test-Path $PersistPath) {
    $remainingFiles = Get-ChildItem $PersistPath -ErrorAction SilentlyContinue
    if ($remainingFiles.Count -eq 0) {
        Remove-Item $PersistPath -Force -ErrorAction SilentlyContinue
    } elseif ($remainingFiles.Count -eq 1 -and $remainingFiles[0].Name -eq "agent-config-backup.json") {
        Write-Host "Keeping preserved configuration in $PersistPath" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " Uninstallation Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green

Wait-ForUserOrTimeout -Message "Press any key to exit..." -TimeoutSeconds 5
