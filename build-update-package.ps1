#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Builds and packages the Computer Inventory Agent for deployment/updates.
    
.DESCRIPTION
    This script publishes both the ThinClient and Updater, then creates a 
    zip package that can be uploaded to the server for auto-update distribution.
    
.PARAMETER Version
    The version number for this release (e.g., "1.0.1.0")
    
.PARAMETER OutputPath
    Path where the update package will be created
    
.PARAMETER IsBreakingChange
    Mark this as a breaking change update
    
.PARAMETER ReleaseNotes
    Release notes for this update
    
.EXAMPLE
    .\build-update-package.ps1 -Version "1.0.1.0" -ReleaseNotes "Bug fixes and improvements"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    
    [string]$OutputPath = ".\publish-update",
    
    [switch]$IsBreakingChange,
    
    [string]$ReleaseNotes = ""
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = $scriptDir
$thinClientProject = Join-Path $solutionDir "src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"
$updaterProject = Join-Path $solutionDir "src\ComputerInventory.ThinClient.Updater\ComputerInventory.ThinClient.Updater.csproj"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Agent Update Package Builder" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Output: $OutputPath" -ForegroundColor Yellow
Write-Host ""

# Clean output directory
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

$tempPublish = Join-Path $OutputPath "temp"
New-Item -ItemType Directory -Path $tempPublish -Force | Out-Null

# Update version in project file
Write-Host "Setting version to $Version..." -ForegroundColor Green
$projectContent = Get-Content $thinClientProject -Raw
if ($projectContent -match '<Version>.*?</Version>') {
    $projectContent = $projectContent -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
} else {
    # Add version if it doesn't exist
    $projectContent = $projectContent -replace '(<PropertyGroup>)', "`$1`n    <Version>$Version</Version>"
}
Set-Content $thinClientProject -Value $projectContent

# Publish ThinClient
Write-Host "Publishing ThinClient..." -ForegroundColor Green
dotnet publish $thinClientProject -c Release -r win-x64 --self-contained true -o "$tempPublish\agent" /p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish ThinClient"
}

# Publish Updater
Write-Host "Publishing Updater..." -ForegroundColor Green
dotnet publish $updaterProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$tempPublish\agent" /p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    throw "Failed to publish Updater"
}

# Create zip package
Write-Host "Creating update package..." -ForegroundColor Green
$zipPath = Join-Path $OutputPath "agent-update.zip"
Compress-Archive -Path "$tempPublish\agent\*" -DestinationPath $zipPath -Force

# Calculate checksum
Write-Host "Calculating checksum..." -ForegroundColor Green
$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
$checksum = $hash.Hash.ToLower()

# Create update.json
$updateInfo = @{
    Version = $Version
    Checksum = $checksum
    FileSize = (Get-Item $zipPath).Length
    IsRequired = $false
    IsBreakingChange = $IsBreakingChange.IsPresent
    ReleaseNotes = $ReleaseNotes
    ReleasedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

$updateJsonPath = Join-Path $OutputPath "update.json"
$updateInfo | ConvertTo-Json -Depth 10 | Set-Content $updateJsonPath

# Cleanup temp
Remove-Item $tempPublish -Recurse -Force

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " Package Created Successfully!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Package: $zipPath" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round((Get-Item $zipPath).Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host "Checksum: $checksum" -ForegroundColor Cyan
Write-Host ""
Write-Host "To deploy this update:" -ForegroundColor Yellow
Write-Host "1. Copy these files to the server's 'agent/update' folder:" -ForegroundColor White
Write-Host "   - agent-update.zip" -ForegroundColor White
Write-Host "   - update.json" -ForegroundColor White
Write-Host ""
Write-Host "2. Or upload via the admin panel at /api/agent/upload-update" -ForegroundColor White
Write-Host ""
