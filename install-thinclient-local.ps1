<#
.SYNOPSIS
    Installs the thin client on the local development machine.

.PARAMETER ServerUrl
    The URL of the inventory server API

.PARAMETER ApiKey
    Optional API key for authentication
#>

param(
    [string]$ServerUrl = "http://10.241.24.60:5010",
    [string]$ApiKey = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Thin Client Local Installation" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server URL: $ServerUrl" -ForegroundColor Yellow
Write-Host ""

# Check for admin rights
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script requires Administrator privileges." -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

$ServiceName = "ComputerInventoryAgent"
$InstallPath = "$env:ProgramFiles\ComputerInventory\Agent"

# Stop existing service if running
Write-Host "[1/5] Checking for existing installation..." -ForegroundColor Green
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "  Stopping existing service..." -ForegroundColor Yellow
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

# Build the project
Write-Host "[2/5] Building thin client..." -ForegroundColor Green
$publishPath = Join-Path $ProjectRoot "publish-thinclient"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create installation directory
Write-Host "[3/5] Installing files..." -ForegroundColor Green
if (Test-Path $InstallPath) {
    Remove-Item $InstallPath -Recurse -Force
}
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# Copy files
Copy-Item -Path "$publishPath\*" -Destination $InstallPath -Recurse -Force

# Update configuration
Write-Host "[4/5] Configuring agent..." -ForegroundColor Green
$configPath = Join-Path $InstallPath "appsettings.json"
$config = Get-Content $configPath | ConvertFrom-Json
$config.ApiSettings.BaseUrl = $ServerUrl
if ($ApiKey) {
    $config.ApiSettings.ApiKey = $ApiKey
}
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath

# Install and start service
Write-Host "[5/5] Installing Windows Service..." -ForegroundColor Green
$exePath = Join-Path $InstallPath "MKS.InventoryAgent.exe"

New-Service -Name $ServiceName `
    -BinaryPathName $exePath `
    -DisplayName "Computer Inventory Agent" `
    -Description "Reports computer inventory and status to the central management server" `
    -StartupType Automatic | Out-Null

# Configure service recovery
sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

# Start the service
Start-Service -Name $ServiceName

# Clean up
Remove-Item $publishPath -Recurse -Force

# Verify
Start-Sleep -Seconds 3
$service = Get-Service -Name $ServiceName

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " Installation Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Service Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq "Running") { "Green" } else { "Yellow" })
Write-Host "Install Path: $InstallPath" -ForegroundColor Gray
Write-Host "Server URL: $ServerUrl" -ForegroundColor Gray
Write-Host ""
Write-Host "Commands:" -ForegroundColor Cyan
Write-Host "  View status:  Get-Service $ServiceName" -ForegroundColor Gray
Write-Host "  View logs:    Get-EventLog -LogName Application -Source ComputerInventoryAgent -Newest 20" -ForegroundColor Gray
Write-Host "  Stop:         Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Uninstall:    .\uninstall-thinclient.ps1" -ForegroundColor Gray
