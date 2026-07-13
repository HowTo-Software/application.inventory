#===============================================================================
# Project Sync - Template for New Projects
# 
# SETUP INSTRUCTIONS:
# 1. Copy this file to your project's root directory
# 2. Rename it to: sync.ps1
# 3. Change the $TemplateName below to your project name
# 4. Ensure the module is accessible (see module paths below)
#
# Usage:
#   .\sync.ps1                    # Sync with default settings
#   .\sync.ps1 -SkipBuild         # Skip Docker image building (faster)
#   .\sync.ps1 -DryRun            # Preview changes without syncing
#   .\sync.ps1 -Force             # Sync even if validation fails
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
# CONFIGURATION - CHANGE THIS FOR YOUR PROJECT
#-------------------------------------------------------------------------------

$TemplateName = "YOUR-PROJECT-NAME"  # <-- CHANGE THIS (e.g., "project-greg")

#-------------------------------------------------------------------------------
# DO NOT MODIFY BELOW THIS LINE
#-------------------------------------------------------------------------------

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find the module in common locations
$modulePaths = @(
    # Central shared location (recommended)
    "C:\Users\hank\Desktop\Service Tracker\tools\Sync-ToServiceTracker.psm1",
    # In a tools subfolder of this project
    (Join-Path $ScriptRoot "tools\Sync-ToServiceTracker.psm1"),
    # In the same folder as this script
    (Join-Path $ScriptRoot "Sync-ToServiceTracker.psm1"),
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
    Write-Host "Please copy Sync-ToServiceTracker.psm1 to one of these locations:" -ForegroundColor Yellow
    foreach ($path in $modulePaths) {
        Write-Host "  - $path" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Or download it from the Computer_Invintory project:" -ForegroundColor Cyan
    Write-Host "  C:\Users\hank\Desktop\Computer_Invintory\tools\Sync-ToServiceTracker.psm1" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Validate template name was changed
if ($TemplateName -eq "YOUR-PROJECT-NAME") {
    Write-Host ""
    Write-Host "ERROR: Please edit sync.ps1 and change `$TemplateName to your project name!" -ForegroundColor Red
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
