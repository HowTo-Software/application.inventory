#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs the Computer Inventory Agent as a Scheduled Task.

.DESCRIPTION
    This script installs the Computer Inventory thin client agent on a loaner computer.
    It configures the agent to start automatically via Scheduled Task and connects to the specified server.
    
    Using Scheduled Task instead of Windows Service avoids HVCI/SCM timeout issues on secured systems.

.PARAMETER ServerUrl
    The URL of the inventory server API. Default: https://localhost:5000

.PARAMETER ApiKey
    The API key for authenticating with the server.

.PARAMETER Silent
    Run in silent mode without prompts.

.EXAMPLE
    .\install.ps1 -ServerUrl "https://inventory.company.com:5000" -ApiKey "secret-key"
#>

param(
    [string]$ServerUrl = "",
    [string]$ApiKey = "",
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

$TaskName = "ComputerInventoryAgent"
$DisplayName = "Computer Inventory Agent"
$Description = "Reports computer inventory and status to the central management server"
$InstallPath = "$env:ProgramFiles\ComputerInventory\Agent"
$ExeName = "MKS.InventoryAgent.exe"

# Detect if running headless (non-interactive)
$IsHeadless = $Silent -or 
    ([Environment]::UserInteractive -eq $false) -or 
    ([Environment]::GetCommandLineArgs() -contains '-NonInteractive') -or
    ($null -eq $Host.UI.RawUI.KeyAvailable) -or
    ($env:TERM -eq 'dumb') -or
    ($null -ne $env:CI)

# Helper function to write diagnostic info
function Write-Diagnostic {
    param([string]$Message)
    Write-Host "  [DIAG] $Message" -ForegroundColor DarkGray
}

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
        Write-Diagnostic "Cannot read key input, continuing..."
        Start-Sleep -Seconds 3
    }
}

# If no ServerUrl provided, read from the bundled appsettings.json
$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $bundledConfigPath = Join-Path $sourcePath "appsettings.json"
    if (Test-Path $bundledConfigPath) {
        $bundledConfig = Get-Content $bundledConfigPath | ConvertFrom-Json
        $ServerUrl = $bundledConfig.ApiSettings.BaseUrl
    }
}

# Fallback if still empty
if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    $ServerUrl = "http://localhost:5010"
}

# Clean up ServerUrl - remove any embedded quotes that may come from CMD argument parsing
$ServerUrl = $ServerUrl.Trim().Trim("'").Trim('"')

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Computer Inventory Agent Installer" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Unblock all files in the source directory (removes "downloaded from internet" flag)
$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Unblocking downloaded files..." -ForegroundColor Gray
Get-ChildItem -Path $sourcePath -Recurse | Unblock-File -ErrorAction SilentlyContinue

# Install code signing certificate if present (required for HVCI/WDAC compliance)
$certPath = Join-Path $sourcePath "MKSCodeSign.cer"
if (Test-Path $certPath) {
    Write-Host "Installing code signing certificate for HVCI compliance..." -ForegroundColor Green
    try {
        # Check if certificate is already installed
        $certToImport = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath)
        $existingCert = Get-ChildItem Cert:\LocalMachine\TrustedPublisher | Where-Object { $_.Thumbprint -eq $certToImport.Thumbprint }
        
        if ($existingCert) {
            Write-Diagnostic "Certificate already installed in TrustedPublisher store."
        } else {
            # Import to TrustedPublisher store (allows signed code to run under WDAC)
            $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "LocalMachine")
            $store.Open("ReadWrite")
            $store.Add($certToImport)
            $store.Close()
            Write-Diagnostic "Certificate installed to TrustedPublisher store."
        }
        
        # Also add to Root if not present (for certificate chain validation)
        $existingRoot = Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Thumbprint -eq $certToImport.Thumbprint }
        if (-not $existingRoot) {
            $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "LocalMachine")
            $rootStore.Open("ReadWrite")
            $rootStore.Add($certToImport)
            $rootStore.Close()
            Write-Diagnostic "Certificate installed to Root store."
        }
        
        Write-Host "  Certificate thumbprint: $($certToImport.Thumbprint)" -ForegroundColor Gray
    } catch {
        Write-Host "  Warning: Failed to install certificate: $_" -ForegroundColor Yellow
    }
} else {
    Write-Diagnostic "No code signing certificate found in package."
}

# Remove existing Windows Service if it exists (migration from service to task)
$existingService = Get-Service -Name "ComputerInventoryAgent" -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Found existing Windows Service. Migrating to Scheduled Task..." -ForegroundColor Yellow
    Stop-Service -Name "ComputerInventoryAgent" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    sc.exe delete "ComputerInventoryAgent" | Out-Null
    Start-Sleep -Seconds 2
    Write-Diagnostic "Windows Service removed."
}

# Check if scheduled task already exists
$existingTask = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($existingTask) {
    Write-Host "Scheduled Task already exists. Stopping and removing..." -ForegroundColor Yellow
    
    # Stop the task if running
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    
    # Remove the task
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}

# Always kill any remaining agent process (even if task was already removed by a previous failed install)
$proc = Get-Process -Name "MKS.InventoryAgent", "ComputerInventory.ThinClient" -ErrorAction SilentlyContinue
if ($proc) {
    Write-Diagnostic "Killing remaining agent process..."
    $proc | Stop-Process -Force -ErrorAction SilentlyContinue
    # Wait for process to actually exit (up to 15 seconds)
    $waitCount = 0
    while ($waitCount -lt 15) {
        $stillRunning = Get-Process -Name "MKS.InventoryAgent", "ComputerInventory.ThinClient" -ErrorAction SilentlyContinue
        if (-not $stillRunning) { break }
        Start-Sleep -Seconds 1
        $waitCount++
    }
    if ($waitCount -ge 15) {
        Write-Diagnostic "Warning: Process may still be running after 15s wait."
    } else {
        Write-Diagnostic "Process exited after ${waitCount}s."
    }
    # Extra delay for file handles to release
    Start-Sleep -Seconds 2
}

# Always clean old files from install directory (keep logs and config backup)
if (Test-Path $InstallPath) {
    Write-Diagnostic "Cleaning old installation files..."
    
    # Back up config before cleaning
    $configBackup = $null
    $configPath = Join-Path $InstallPath "appsettings.json"
    if (Test-Path $configPath) {
        $configBackup = Get-Content $configPath -Raw
        Write-Diagnostic "Backed up appsettings.json"
    }
    
    # Aggressively remove all files except logs directory
    Get-ChildItem -Path $InstallPath -Exclude "logs" | ForEach-Object {
        try {
            Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
        } catch {
            # PowerShell Remove-Item failed — try cmd /c del/rd for locked files
            Write-Diagnostic "PowerShell remove failed for $($_.Name), trying cmd..."
            if ($_.PSIsContainer) {
                cmd /c "rd /s /q `"$($_.FullName)`"" 2>$null
            } else {
                cmd /c "del /f /q `"$($_.FullName)`"" 2>$null
            }
            # If still there, take ownership and retry
            if (Test-Path $_.FullName) {
                Write-Diagnostic "Taking ownership of $($_.Name)..."
                cmd /c "takeown /f `"$($_.FullName)`" /r /d y" 2>$null
                cmd /c "icacls `"$($_.FullName)`" /grant Administrators:F /t /q" 2>$null
                Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path $_.FullName) {
                Write-Diagnostic "Warning: Could not remove $($_.Name), will be overwritten."
            }
        }
    }
    
    # Restore config backup
    if ($configBackup) {
        $configBackup | Set-Content $configPath -Force
        Write-Diagnostic "Restored appsettings.json"
    }
}

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Green
if (!(Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Create logs directory
$logsPath = Join-Path $InstallPath "logs"
Write-Host "Creating logs directory..." -ForegroundColor Green
if (!(Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
}

# Grant SYSTEM and Users write access to logs folder
Write-Diagnostic "Setting permissions on logs folder..."
$acl = Get-Acl $logsPath
$systemRule = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
$usersRule = New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($systemRule)
$acl.SetAccessRule($usersRule)
Set-Acl $logsPath $acl -ErrorAction SilentlyContinue

# Create Windows Event Log source (for startup error logging)
Write-Diagnostic "Creating Windows Event Log source..."
$eventSource = "ComputerInventoryAgent"
if (-not [System.Diagnostics.EventLog]::SourceExists($eventSource)) {
    try {
        [System.Diagnostics.EventLog]::CreateEventSource($eventSource, "Application")
        Write-Diagnostic "Event log source created: $eventSource"
    } catch {
        Write-Diagnostic "Could not create event log source: $_"
    }
}

# Copy files
Write-Host "Copying files..." -ForegroundColor Green
$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path
$copyRetries = 5
for ($i = 1; $i -le $copyRetries; $i++) {
    try {
        Copy-Item -Path "$sourcePath\*" -Destination $InstallPath -Recurse -Force -Exclude "install.ps1", "install.cmd", "install-native.cmd", "uninstall.ps1"
        break
    } catch {
        if ($i -lt $copyRetries) {
            Write-Host "  File copy attempt $i failed, retrying in 3 seconds..." -ForegroundColor Yellow
            # On retry, force-remove the specific blocking file
            $lockedFile = if ($_.TargetObject) { $_.TargetObject } 
                          elseif ($_.Exception.Message -match "'([^']+)'") { $Matches[1] } 
                          else { $null }
            if ($lockedFile -and (Test-Path $lockedFile)) {
                Write-Diagnostic "Force-removing blocking file: $(Split-Path -Leaf $lockedFile)"
                cmd /c "del /f /q `"$lockedFile`"" 2>$null
                if (Test-Path $lockedFile) {
                    cmd /c "takeown /f `"$lockedFile`" /d y" 2>$null
                    cmd /c "icacls `"$lockedFile`" /grant Administrators:F /q" 2>$null
                    cmd /c "del /f /q `"$lockedFile`"" 2>$null
                }
            }
            Start-Sleep -Seconds 3
        } else { throw }
    }
}

# Unblock all copied files (removes Zone.Identifier blocking from internet downloads)
Write-Host "Unblocking installed files..." -ForegroundColor Green
Get-ChildItem -Path $InstallPath -Recurse | Unblock-File -ErrorAction SilentlyContinue

# Check for persisted configuration from previous install
$persistedConfigPath = Join-Path $env:ProgramData "ComputerInventory\agent-config-backup.json"
if (Test-Path $persistedConfigPath) {
    Write-Host "Found persisted configuration from previous install..." -ForegroundColor Cyan
    try {
        $persisted = Get-Content $persistedConfigPath | ConvertFrom-Json
        if ($persisted.Configuration) {
            Write-Host "  Saved at: $($persisted.SavedAt)" -ForegroundColor Gray
            Write-Host "  From version: $($persisted.AgentVersion)" -ForegroundColor Gray
            Write-Host "  Machine: $($persisted.MachineName)" -ForegroundColor Gray
            
            $configPath = Join-Path $InstallPath "appsettings.json"
            # Restore the persisted config
            $restoredConfig = $persisted.Configuration | ConvertTo-Json -Depth 10
            $restoredConfig | Set-Content $configPath
            
            # Read back the restored values
            $config = Get-Content $configPath | ConvertFrom-Json
            $ServerUrl = $config.ApiSettings.BaseUrl
            Write-Host "Restored configuration from backup" -ForegroundColor Green
            
            # Remove the backup file since we've restored it
            Remove-Item $persistedConfigPath -Force
            Write-Host "Deleted persisted configuration file" -ForegroundColor Gray
        }
    } catch {
        Write-Host "Warning: Failed to restore persisted configuration: $_" -ForegroundColor Yellow
    }
}

# Update configuration (if ServerUrl or ApiKey specified on command line)
Write-Host "Updating configuration..." -ForegroundColor Green
$configPath = Join-Path $InstallPath "appsettings.json"
if (Test-Path $configPath) {
    $config = Get-Content $configPath | ConvertFrom-Json
    $config.ApiSettings.BaseUrl = $ServerUrl
    if ($ApiKey) {
        $config.ApiSettings.ApiKey = $ApiKey
    }
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath
}

# Install Scheduled Task
Write-Host "Installing Scheduled Task..." -ForegroundColor Green
$exePath = Join-Path $InstallPath $ExeName

# Verify the executable exists
if (!(Test-Path $exePath)) {
    Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
    exit 1
}

# Create the scheduled task action
$action = New-ScheduledTaskAction -Execute $exePath -WorkingDirectory $InstallPath

# Create trigger: At system startup with 30 second delay
$triggerStartup = New-ScheduledTaskTrigger -AtStartup
$triggerStartup.Delay = "PT30S"

# Create principal (run as SYSTEM with highest privileges)
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest

# Create settings
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -RestartCount 999 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Days 9999) `
    -MultipleInstances IgnoreNew `
    -Priority 4

# Disable the task idle settings (don't stop when idle)
$settings.IdleSettings.StopOnIdleEnd = $false
$settings.IdleSettings.RestartOnIdle = $false

# Register the scheduled task
Write-Host "Registering scheduled task..." -ForegroundColor Green
try {
    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $triggerStartup `
        -Principal $principal `
        -Settings $settings `
        -Description $Description `
        -Force | Out-Null
    
    Write-Diagnostic "Scheduled task registered successfully"
} catch {
    Write-Host "ERROR: Failed to register scheduled task: $_" -ForegroundColor Red
    exit 1
}

# Start the task immediately
Write-Host "Starting agent..." -ForegroundColor Green
try {
    Start-ScheduledTask -TaskName $TaskName -ErrorAction Stop
    Start-Sleep -Seconds 3
    
    # Check if the process is running
    $proc = Get-Process -Name "MKS.InventoryAgent" -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host ""
        Write-Host "======================================" -ForegroundColor Green
        Write-Host " Installation Complete!" -ForegroundColor Green
        Write-Host "======================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Agent Status: Running (PID: $($proc.Id))" -ForegroundColor Green
        Write-Host "Install Path: $InstallPath" -ForegroundColor Gray
        Write-Host "Server URL: $ServerUrl" -ForegroundColor Gray
        Write-Host "Logs: $logsPath" -ForegroundColor Gray
        Write-Host ""
        Write-Host "The agent runs as a Scheduled Task (not a Windows Service)" -ForegroundColor Cyan
        Write-Host "This avoids HVCI/SCM timeout issues on secured systems." -ForegroundColor Cyan
    } else {
        # Task started but process not found yet, wait a bit more
        Start-Sleep -Seconds 3
        $proc = Get-Process -Name "MKS.InventoryAgent" -ErrorAction SilentlyContinue
        if ($proc) {
            Write-Host ""
            Write-Host "======================================" -ForegroundColor Green
            Write-Host " Installation Complete!" -ForegroundColor Green
            Write-Host "======================================" -ForegroundColor Green
            Write-Host ""
            Write-Host "Agent Status: Running (PID: $($proc.Id))" -ForegroundColor Green
        } else {
            throw "Process not running after task start"
        }
    }
} catch {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Yellow
    Write-Host " Installation Completed with Warning" -ForegroundColor Yellow
    Write-Host "======================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "The scheduled task was created but the agent may not be running." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Troubleshooting steps:" -ForegroundColor Cyan
    Write-Host "  1. Check logs: $logsPath" -ForegroundColor Gray
    Write-Host "  2. Try starting manually:" -ForegroundColor Gray
    Write-Host "     Start-ScheduledTask -TaskName $TaskName" -ForegroundColor White
    Write-Host "  3. Run exe directly to see errors:" -ForegroundColor Gray
    Write-Host "     & '$exePath'" -ForegroundColor White
    Write-Host "  4. Check task status:" -ForegroundColor Gray
    Write-Host "     Get-ScheduledTask -TaskName $TaskName" -ForegroundColor White
    Write-Host ""
    
    # Try to get more diagnostic info
    $task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    if ($task) {
        $taskInfo = Get-ScheduledTaskInfo -TaskName $TaskName -ErrorAction SilentlyContinue
        Write-Host "Task State: $($task.State)" -ForegroundColor Gray
        if ($taskInfo) {
            Write-Host "Last Run: $($taskInfo.LastRunTime)" -ForegroundColor Gray
            Write-Host "Last Result: $($taskInfo.LastTaskResult)" -ForegroundColor Gray
        }
    }
}

# Final prompt - use safe function that handles headless mode
Wait-ForUserOrTimeout -Message "Press any key to exit..." -TimeoutSeconds 5
