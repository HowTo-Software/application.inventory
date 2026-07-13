#===============================================================================
# Install Sync Module to Service Tracker
#
# Run this once to install the module to the central Service Tracker location
# so all your projects can use it.
#
# Usage:
#   .\install-sync-module.ps1
#
#===============================================================================

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "Installing Sync-ToServiceTracker Module" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find Service Tracker
$serviceTrackerPath = $null

if ($env:SERVICE_TRACKER_PATH -and (Test-Path $env:SERVICE_TRACKER_PATH)) {
    $serviceTrackerPath = $env:SERVICE_TRACKER_PATH
} else {
    $commonPaths = @(
        "C:\Users\hank\Desktop\Service Tracker",
        "$env:USERPROFILE\Desktop\Service Tracker",
        "$env:USERPROFILE\Projects\Service Tracker"
    )
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            $serviceTrackerPath = $path
            break
        }
    }
}

if (-not $serviceTrackerPath) {
    Write-Host "ERROR: Could not find Service Tracker installation!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please set the SERVICE_TRACKER_PATH environment variable:" -ForegroundColor Yellow
    Write-Host '  $env:SERVICE_TRACKER_PATH = "C:\Path\To\Service Tracker"' -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "Service Tracker found at: $serviceTrackerPath" -ForegroundColor Green
Write-Host ""

# Create tools directory
$toolsDir = Join-Path $serviceTrackerPath "tools"
if (-not (Test-Path $toolsDir)) {
    Write-Host "Creating tools directory..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
}

# Copy the module
$sourceModule = Join-Path $ScriptRoot "Sync-ToServiceTracker.psm1"
$targetModule = Join-Path $toolsDir "Sync-ToServiceTracker.psm1"

if (-not (Test-Path $sourceModule)) {
    Write-Host "ERROR: Source module not found at: $sourceModule" -ForegroundColor Red
    exit 1
}

Write-Host "Copying module to: $targetModule" -ForegroundColor Gray
Copy-Item -Path $sourceModule -Destination $targetModule -Force

Write-Host ""
Write-Host "SUCCESS! Module installed." -ForegroundColor Green
Write-Host ""
Write-Host "Now you can use the sync framework from any project:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. Copy sync-template.ps1 to your project as sync.ps1" -ForegroundColor Gray
Write-Host "  2. Edit the `$TemplateName in sync.ps1" -ForegroundColor Gray
Write-Host "  3. Run .\sync.ps1 from your project directory" -ForegroundColor Gray
Write-Host ""

# Copy template for reference
$templateSource = Join-Path $ScriptRoot "sync-template.ps1"
$templateTarget = Join-Path $toolsDir "sync-template.ps1"
if (Test-Path $templateSource) {
    Copy-Item -Path $templateSource -Destination $templateTarget -Force
    Write-Host "Template also copied to: $templateTarget" -ForegroundColor Gray
}

Write-Host ""
