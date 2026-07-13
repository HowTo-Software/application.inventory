<#
.SYNOPSIS
    Uninstalls the thin client from the local machine.
#>

$ErrorActionPreference = "Stop"
$ServiceName = "ComputerInventoryAgent"
$InstallPath = "$env:ProgramFiles\ComputerInventory\Agent"

# Check for admin rights
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "This script requires Administrator privileges." -ForegroundColor Red
    exit 1
}

Write-Host "Uninstalling Computer Inventory Agent..." -ForegroundColor Yellow

# Stop and remove service
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Service removed." -ForegroundColor Green
}

# Remove files
if (Test-Path $InstallPath) {
    Remove-Item -Path $InstallPath -Recurse -Force
    Write-Host "Files removed." -ForegroundColor Green
}

Write-Host "Uninstallation complete!" -ForegroundColor Green
