#===============================================================================
# Project Sync - Lightweight Wrapper
# 
# Copy this file to any project's root directory and customize the TemplateName.
# The script will automatically import the shared module and sync the project.
#
# Usage:
#   .\sync.ps1                    # Sync with default settings
#   .\sync.ps1 -SkipBuild         # Skip Docker image building
#   .\sync.ps1 -DryRun            # Preview changes without syncing
#
#===============================================================================

param(
    [switch]$SkipBuild,
    [switch]$BuildImages,
    [switch]$DryRun,
    [switch]$Force,
    [string]$ServiceTrackerPath
)

#-------------------------------------------------------------------------------
# CONFIGURATION - Customize these for your project
#-------------------------------------------------------------------------------
$TemplateName = "computer-inventory"  # Change this to your project's template name
#-------------------------------------------------------------------------------

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find the module in common locations
$modulePaths = @(
    # In a tools subfolder of this project
    (Join-Path $ScriptRoot "tools\Sync-ToServiceTracker.psm1"),
    # In the same folder as this script
    (Join-Path $ScriptRoot "Sync-ToServiceTracker.psm1"),
    # In Service Tracker's tools folder
    "C:\Users\hank\Desktop\Service Tracker\tools\Sync-ToServiceTracker.psm1",
    # In user's PowerShell modules
    "$env:USERPROFILE\Documents\PowerShell\Modules\Sync-ToServiceTracker\Sync-ToServiceTracker.psm1",
    "$env:USERPROFILE\Documents\WindowsPowerShell\Modules\Sync-ToServiceTracker\Sync-ToServiceTracker.psm1"
)

$moduleFound = $false
foreach ($modulePath in $modulePaths) {
    if (Test-Path $modulePath) {
        Write-Host "Loading module from: $modulePath" -ForegroundColor DarkGray
        Import-Module $modulePath -Force
        $moduleFound = $true
        break
    }
}

if (-not $moduleFound) {
    Write-Host ""
    Write-Host "ERROR: Sync-ToServiceTracker module not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure Sync-ToServiceTracker.psm1 is in one of these locations:" -ForegroundColor Yellow
    foreach ($path in $modulePaths) {
        Write-Host "  - $path" -ForegroundColor Gray
    }
    Write-Host ""
    exit 1
}

# Build parameters
$syncParams = @{
    SourcePath = $ScriptRoot
    TemplateName = $TemplateName
}

if ($SkipBuild) { $syncParams.SkipBuild = $true }
if ($BuildImages) { $syncParams.BuildImages = $true }
if ($DryRun) { $syncParams.DryRun = $true }
if ($Force) { $syncParams.Force = $true }
if ($ServiceTrackerPath) { $syncParams.ServiceTrackerPath = $ServiceTrackerPath }

# Run the sync
Sync-ToServiceTracker @syncParams
