#===============================================================================
# Service Tracker - Project Sync Module
# 
# A reusable PowerShell module for syncing projects to Service Tracker.
# This module can be used by any project to sync files and build Docker images.
#
# Installation:
#   1. Copy this file to your PowerShell modules directory, OR
#   2. Import directly: Import-Module "path\to\Sync-ToServiceTracker.psm1"
#
# Usage from any project:
#   Import-Module "C:\Path\To\Sync-ToServiceTracker.psm1"
#   Sync-ToServiceTracker -TemplateName "my-project"
#
#===============================================================================

$ErrorActionPreference = "Stop"

#-------------------------------------------------------------------------------
# Helper Functions
#-------------------------------------------------------------------------------
function Write-SyncHeader { param($msg) Write-Host "`n$msg" -ForegroundColor Cyan }
function Write-SyncSuccess { param($msg) Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-SyncWarning { param($msg) Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-SyncError { param($msg) Write-Host "  [ERROR] $msg" -ForegroundColor Red }
function Write-SyncInfo { param($msg) Write-Host "  -> $msg" -ForegroundColor Gray }

#-------------------------------------------------------------------------------
# Find Service Tracker Path
#-------------------------------------------------------------------------------
function Find-ServiceTrackerPath {
    param(
        [string]$ExplicitPath,
        [string]$ScriptRoot
    )
    
    # Priority: parameter > env var > script location > common locations
    if ($ExplicitPath -and (Test-Path $ExplicitPath)) {
        return $ExplicitPath
    }
    
    if ($env:SERVICE_TRACKER_PATH -and (Test-Path $env:SERVICE_TRACKER_PATH)) {
        return $env:SERVICE_TRACKER_PATH
    }
    
    if ($ScriptRoot -and (Test-Path (Join-Path $ScriptRoot "packages\backend\templates"))) {
        return $ScriptRoot
    }
    
    # Try common locations
    $commonPaths = @(
        "C:\Users\hank\Desktop\Service Tracker",
        "$env:USERPROFILE\Desktop\Service Tracker",
        "$env:USERPROFILE\Projects\Service Tracker",
        "D:\Service Tracker"
    )
    
    foreach ($path in $commonPaths) {
        if (Test-Path (Join-Path $path "packages\backend\templates")) {
            return $path
        }
    }
    
    return $null
}

#-------------------------------------------------------------------------------
# Validate Project
#-------------------------------------------------------------------------------
function Test-ProjectValid {
    param(
        [string]$SourcePath,
        [switch]$Force
    )
    
    $result = @{
        Valid = $true
        ProjectType = "unknown"
        ComposeFile = $null
        Errors = @()
        Warnings = @()
    }
    
    # Check source exists
    if (-not (Test-Path $SourcePath)) {
        $result.Errors += "Source path not found: $SourcePath"
        $result.Valid = $false
        return $result
    }
    
    # Find compose file
    $composeVariants = @("docker-compose.yml", "docker-compose.yaml", "compose.yml")
    foreach ($variant in $composeVariants) {
        $testPath = Join-Path $SourcePath $variant
        if (Test-Path $testPath) {
            $result.ComposeFile = $testPath
            break
        }
    }
    
    if (-not $result.ComposeFile) {
        $result.Errors += "No docker-compose file found (docker-compose.yml, docker-compose.yaml, or compose.yml)"
    }
    
    # Detect project type
    # .NET
    $slnFiles = Get-ChildItem -Path $SourcePath -Filter "*.sln" -File -ErrorAction SilentlyContinue
    if ($slnFiles) {
        $result.ProjectType = ".NET"
        $publishDir = Join-Path $SourcePath "publish"
        if (-not (Test-Path $publishDir)) {
            $result.Warnings += "No publish directory - ensure Dockerfile builds from source or pre-publish"
        }
    }
    
    # Python
    if (Test-Path (Join-Path $SourcePath "requirements.txt")) {
        $result.ProjectType = "Python"
    }
    
    # Node.js
    if (Test-Path (Join-Path $SourcePath "package.json")) {
        $result.ProjectType = "Node.js"
    }
    
    # Go
    if (Test-Path (Join-Path $SourcePath "go.mod")) {
        $result.ProjectType = "Go"
    }
    
    # Rust
    if (Test-Path (Join-Path $SourcePath "Cargo.toml")) {
        $result.ProjectType = "Rust"
    }
    
    # Check for errors
    if ($result.Errors.Count -gt 0 -and -not $Force) {
        $result.Valid = $false
    }
    
    return $result
}

#-------------------------------------------------------------------------------
# Parse Docker Compose for Build Services
#-------------------------------------------------------------------------------
function Get-ComposeBuildServices {
    param(
        [string]$ComposeFile
    )
    
    $servicesToBuild = @()
    
    if (-not (Test-Path $ComposeFile)) {
        return $servicesToBuild
    }
    
    $inServices = $false
    $currentService = $null
    $hasBuild = $false
    $buildContext = "."
    $dockerfile = $null
    
    foreach ($line in (Get-Content $ComposeFile)) {
        if ($line -match "^services:\s*$") {
            $inServices = $true
            continue
        }
        
        if ($inServices) {
            # Check for service name (2-space indent, ends with colon)
            if ($line -match "^  ([a-zA-Z0-9_-]+):\s*$") {
                # Save previous service if it had build
                if ($currentService -and $hasBuild) {
                    $servicesToBuild += @{
                        Name = $currentService
                        Context = $buildContext
                        Dockerfile = $dockerfile
                    }
                }
                $currentService = $matches[1]
                $hasBuild = $false
                $buildContext = "."
                $dockerfile = $null
            }
            
            # Check for build section
            if ($line -match "^\s{4}build:\s*$") {
                $hasBuild = $true
            }
            if ($line -match "^\s{4}build:\s+(.+)$") {
                $hasBuild = $true
                $buildContext = $matches[1].Trim()
            }
            if ($line -match "^\s{6}context:\s+(.+)$") {
                $buildContext = $matches[1].Trim()
            }
            if ($line -match "^\s{6}dockerfile:\s+(.+)$") {
                $dockerfile = $matches[1].Trim()
            }
        }
    }
    
    # Don't forget the last service
    if ($currentService -and $hasBuild) {
        $servicesToBuild += @{
            Name = $currentService
            Context = $buildContext
            Dockerfile = $dockerfile
        }
    }
    
    return $servicesToBuild
}

#-------------------------------------------------------------------------------
# Build Docker Images
#-------------------------------------------------------------------------------
function Build-DockerImages {
    param(
        [string]$SourcePath,
        [string]$TargetPath,
        [string]$ProjectName,
        [array]$Services
    )
    
    $results = @{
        Success = $true
        ImagesBuilt = @()
        Errors = @()
    }
    
    # Check Docker availability
    try {
        $dockerVersion = & docker version --format "{{.Server.Version}}" 2>&1
        if ($LASTEXITCODE -ne 0) {
            $results.Success = $false
            $results.Errors += "Docker daemon is not running"
            return $results
        }
        Write-SyncInfo "Docker daemon is running (version: $dockerVersion)"
    } catch {
        $results.Success = $false
        $results.Errors += "Docker is not available"
        return $results
    }
    
    # Create images directory
    $imagesDir = Join-Path $TargetPath "docker-images"
    if (-not (Test-Path $imagesDir)) {
        New-Item -ItemType Directory -Path $imagesDir -Force | Out-Null
    }
    
    Push-Location $SourcePath
    try {
        foreach ($service in $Services) {
            $imageName = "${ProjectName}-$($service.Name):latest"
            $tarFile = Join-Path $imagesDir "$($service.Name).tar"
            
            # Determine dockerfile path
            $dockerfilePath = if ($service.Dockerfile) {
                Join-Path $service.Context $service.Dockerfile
            } else {
                Join-Path $service.Context "Dockerfile"
            }
            
            Write-SyncInfo "Building $($service.Name) from $dockerfilePath..."
            
            # Build the image
            $buildProcess = Start-Process -FilePath "docker" -ArgumentList "build", "-t", $imageName, "-f", $dockerfilePath, $service.Context -Wait -PassThru -NoNewWindow
            
            if ($buildProcess.ExitCode -ne 0) {
                Write-SyncError "Failed to build $($service.Name)"
                $results.Errors += "Failed to build $($service.Name)"
                continue
            }
            
            Write-SyncSuccess "Built image: $imageName"
            
            # Save to tar
            Write-SyncInfo "Saving $($service.Name).tar..."
            & docker save -o $tarFile $imageName 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                $tarSize = [math]::Round((Get-Item $tarFile).Length / 1MB, 2)
                Write-SyncSuccess "Saved $($service.Name).tar ($tarSize MB)"
                $results.ImagesBuilt += @{
                    Service = $service.Name
                    ImageName = $imageName
                    TarFile = $tarFile
                    SizeMB = $tarSize
                }
            } else {
                Write-SyncError "Failed to save $($service.Name).tar"
                $results.Errors += "Failed to save $($service.Name).tar"
            }
        }
        
        # Create manifest
        if ($results.ImagesBuilt.Count -gt 0) {
            $imageManifest = @{
                projectName = $ProjectName
                builtAt = (Get-Date).ToString("o")
                images = $results.ImagesBuilt | ForEach-Object {
                    @{
                        service = $_.Service
                        file = (Split-Path $_.TarFile -Leaf)
                        imageName = $_.ImageName
                        sizeMB = $_.SizeMB
                    }
                }
            }
            
            $manifestPath = Join-Path $imagesDir "manifest.json"
            $imageManifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath
            Write-SyncSuccess "Created image manifest"
        }
        
    } finally {
        Pop-Location
    }
    
    return $results
}

#-------------------------------------------------------------------------------
# Main Sync Function
#-------------------------------------------------------------------------------
function Sync-ToServiceTracker {
    <#
    .SYNOPSIS
        Syncs a project to Service Tracker templates directory.
    
    .DESCRIPTION
        Copies project files to Service Tracker and optionally builds Docker images
        locally for deployment to low-memory servers.
    
    .PARAMETER SourcePath
        Path to the source project. Defaults to current directory.
    
    .PARAMETER TemplateName
        Name of the template in Service Tracker.
    
    .PARAMETER ServiceTrackerPath
        Path to Service Tracker installation. Can also be set via SERVICE_TRACKER_PATH env var.
    
    .PARAMETER SkipBuild
        Skip building Docker images.
    
    .PARAMETER BuildImages
        Force building Docker images.
    
    .PARAMETER DryRun
        Preview changes without making them.
    
    .PARAMETER Force
        Continue even if validation fails.
    
    .EXAMPLE
        Sync-ToServiceTracker -TemplateName "my-project"
    
    .EXAMPLE
        Sync-ToServiceTracker -SourcePath "C:\Projects\MyApp" -TemplateName "my-app" -SkipBuild
    #>
    
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$false)]
        [string]$SourcePath,
        
        [Parameter(Mandatory=$true)]
        [string]$TemplateName,
        
        [Parameter(Mandatory=$false)]
        [string]$ServiceTrackerPath,
        
        [switch]$Validate,
        [switch]$DryRun,
        [switch]$Force,
        [switch]$BuildImages,
        [switch]$SkipBuild
    )
    
    # Determine source path
    if (-not $SourcePath) {
        $SourcePath = Get-Location
        Write-Host "Using current directory as source: $SourcePath" -ForegroundColor Cyan
    }
    
    # Find Service Tracker
    $ServiceTrackerPath = Find-ServiceTrackerPath -ExplicitPath $ServiceTrackerPath
    
    if (-not $ServiceTrackerPath) {
        Write-Host ""
        Write-Host "ERROR: Service Tracker path not found!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please specify the path using one of these methods:" -ForegroundColor Yellow
        Write-Host '  1. Parameter:    -ServiceTrackerPath "C:\Path\To\Service Tracker"' -ForegroundColor Gray
        Write-Host '  2. Environment:  $env:SERVICE_TRACKER_PATH = "C:\Path\To\Service Tracker"' -ForegroundColor Gray
        Write-Host ""
        return $false
    }
    
    $TemplatesDir = Join-Path $ServiceTrackerPath "packages\backend\templates"
    
    if (-not (Test-Path $TemplatesDir)) {
        Write-Host ""
        Write-Host "ERROR: Templates directory not found at: $TemplatesDir" -ForegroundColor Red
        Write-Host "Is this a valid Service Tracker installation?" -ForegroundColor Yellow
        Write-Host ""
        return $false
    }
    
    # Display banner
    Write-Host ""
    Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "  Service Tracker - Project Sync" -ForegroundColor Cyan
    Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Source:          $SourcePath" -ForegroundColor White
    Write-Host "  Template:        $TemplateName" -ForegroundColor White
    Write-Host "  Service Tracker: $ServiceTrackerPath" -ForegroundColor White
    Write-Host "  Build Images:    $($BuildImages -or (-not $SkipBuild))" -ForegroundColor White
    Write-Host ""
    
    #---------------------------------------------------------------------------
    # Validation
    #---------------------------------------------------------------------------
    Write-SyncHeader "[1/5] Validating Source Project..."
    
    $validation = Test-ProjectValid -SourcePath $SourcePath -Force:$Force
    
    Write-SyncSuccess "Source path exists"
    Write-SyncInfo "Source: $SourcePath"
    
    if ($validation.ComposeFile) {
        Write-SyncSuccess "Found: Docker Compose file ($(Split-Path $validation.ComposeFile -Leaf))"
    }
    
    if ($validation.ProjectType -ne "unknown") {
        Write-SyncSuccess "Detected $($validation.ProjectType) project"
    }
    
    Write-SyncInfo "Project type: $($validation.ProjectType)"
    
    #---------------------------------------------------------------------------
    # Validation Results
    #---------------------------------------------------------------------------
    Write-SyncHeader "[2/5] Validation Results..."
    
    if ($validation.Errors.Count -gt 0) {
        foreach ($err in $validation.Errors) {
            Write-SyncError $err
        }
        
        if (-not $Force) {
            Write-Host ""
            Write-Host "  Use -Force to sync anyway, or fix the issues above." -ForegroundColor Yellow
            return $false
        } else {
            Write-SyncWarning "Continuing due to -Force flag..."
        }
    }
    
    foreach ($warn in $validation.Warnings) {
        Write-SyncWarning $warn
    }
    
    if ($validation.Errors.Count -eq 0) {
        Write-SyncSuccess "Validation passed!"
    }
    
    #---------------------------------------------------------------------------
    # Calculate Changes
    #---------------------------------------------------------------------------
    Write-SyncHeader "[3/5] Calculating Changes..."
    
    $targetPath = Join-Path $TemplatesDir $TemplateName
    
    # Exclusion patterns
    $excludePatterns = @(
        "bin", "obj", "node_modules", ".git", ".vs", ".idea",
        "__pycache__", "*.user", "*.suo", "*.log",
        ".env.local", ".env.*.local", "target"
    )
    
    # Get source files
    $sourceFiles = Get-ChildItem -Path $SourcePath -Recurse -File | Where-Object {
        $relativePath = $_.FullName.Substring($SourcePath.Length + 1)
        $exclude = $false
        foreach ($pattern in $excludePatterns) {
            if ($relativePath -like "*$pattern*") {
                $exclude = $true
                break
            }
        }
        -not $exclude
    }
    
    $totalSize = ($sourceFiles | Measure-Object -Property Length -Sum).Sum
    $totalFiles = $sourceFiles.Count
    
    Write-SyncInfo "Files to sync: $totalFiles"
    Write-SyncInfo "Total size: $([math]::Round($totalSize / 1MB, 2)) MB"
    
    if (Test-Path $targetPath) {
        $existingFiles = Get-ChildItem -Path $targetPath -Recurse -File -ErrorAction SilentlyContinue
        Write-SyncWarning "Target exists with $($existingFiles.Count) files - will be replaced"
    } else {
        Write-SyncInfo "Target is new directory"
    }
    
    #---------------------------------------------------------------------------
    # Dry Run
    #---------------------------------------------------------------------------
    if ($DryRun) {
        Write-SyncHeader "[4/5] DRY RUN - No changes made"
        Write-Host ""
        Write-Host "  Would sync the following:" -ForegroundColor Gray
        Write-Host "    From: $SourcePath" -ForegroundColor White
        Write-Host "    To:   $targetPath" -ForegroundColor White
        Write-Host ""
        Write-Host "  First 20 files:" -ForegroundColor Gray
        $sourceFiles | Select-Object -First 20 | ForEach-Object {
            $rel = $_.FullName.Substring($SourcePath.Length + 1)
            Write-Host "    $rel" -ForegroundColor DarkGray
        }
        if ($totalFiles -gt 20) {
            Write-Host "    ... and $($totalFiles - 20) more files" -ForegroundColor DarkGray
        }
        Write-Host ""
        Write-Host "  Run without -DryRun to execute sync." -ForegroundColor Yellow
        return $true
    }
    
    #---------------------------------------------------------------------------
    # Execute Sync
    #---------------------------------------------------------------------------
    Write-SyncHeader "[4/5] Syncing Files..."
    
    # Create templates directory if needed
    if (-not (Test-Path $TemplatesDir)) {
        New-Item -ItemType Directory -Path $TemplatesDir -Force | Out-Null
    }
    
    # Remove existing target
    if (Test-Path $targetPath) {
        Write-SyncInfo "Removing existing target directory..."
        Remove-Item -Path $targetPath -Recurse -Force
    }
    
    # Create fresh target
    New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
    
    # Use robocopy
    $robocopyArgs = @(
        $SourcePath,
        $targetPath,
        "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/NC", "/NS"
    )
    
    foreach ($pattern in $excludePatterns) {
        $robocopyArgs += "/XD"
        $robocopyArgs += $pattern
    }
    
    Write-SyncInfo "Running robocopy..."
    $robocopyResult = & robocopy @robocopyArgs 2>&1
    
    if ($LASTEXITCODE -le 7) {
        Write-SyncSuccess "Files copied successfully"
    } else {
        Write-SyncError "Robocopy returned error code: $LASTEXITCODE"
        Write-Host $robocopyResult -ForegroundColor Red
    }
    
    #---------------------------------------------------------------------------
    # Build Docker Images
    #---------------------------------------------------------------------------
    $shouldBuildImages = $BuildImages -or (-not $SkipBuild)
    
    if ($shouldBuildImages -and $validation.ComposeFile) {
        Write-SyncHeader "[4.5/5] Building Docker Images Locally..."
        
        $services = Get-ComposeBuildServices -ComposeFile $validation.ComposeFile
        
        if ($services.Count -gt 0) {
            Write-SyncInfo "Found $($services.Count) services to build: $($services.Name -join ', ')"
            
            $projectName = $TemplateName.ToLower() -replace '[^a-z0-9]', ''
            $buildResult = Build-DockerImages -SourcePath $SourcePath -TargetPath $targetPath -ProjectName $projectName -Services $services
            
            if (-not $buildResult.Success) {
                foreach ($err in $buildResult.Errors) {
                    Write-SyncWarning $err
                }
                Write-SyncWarning "Some images failed to build - they will be built on the server"
            }
        } else {
            Write-SyncInfo "No services with build contexts found"
        }
    } elseif ($SkipBuild) {
        Write-SyncInfo "Skipping Docker image build (-SkipBuild specified)"
    }
    
    #---------------------------------------------------------------------------
    # Verification
    #---------------------------------------------------------------------------
    Write-SyncHeader "[5/5] Verifying Sync..."
    
    $targetFiles = Get-ChildItem -Path $targetPath -Recurse -File -ErrorAction SilentlyContinue
    $targetCount = $targetFiles.Count
    
    Write-SyncInfo "Source files: $totalFiles"
    Write-SyncInfo "Target files: $targetCount"
    
    # Verify compose file
    $composeVariants = @("docker-compose.yml", "docker-compose.yaml", "compose.yml")
    $foundTargetCompose = $false
    foreach ($variant in $composeVariants) {
        if (Test-Path (Join-Path $targetPath $variant)) {
            Write-SyncSuccess "Verified: $variant exists in target"
            $foundTargetCompose = $true
            break
        }
    }
    
    if (-not $foundTargetCompose) {
        Write-SyncError "No compose file found in target!"
    }
    
    # Create sync metadata
    $syncMetadata = @{
        templateName = $TemplateName
        sourcePath = $SourcePath
        serviceTrackerPath = $ServiceTrackerPath
        syncedAt = (Get-Date).ToString("o")
        projectType = $validation.ProjectType
        fileCount = $targetCount
        syncedBy = $env:USERNAME
        machine = $env:COMPUTERNAME
    }
    
    $metadataPath = Join-Path $targetPath ".sync-metadata.json"
    $syncMetadata | ConvertTo-Json -Depth 3 | Set-Content -Path $metadataPath
    
    Write-SyncSuccess "Created sync metadata"
    
    #---------------------------------------------------------------------------
    # Summary
    #---------------------------------------------------------------------------
    Write-Host ""
    Write-Host "-----------------------------------------------------------" -ForegroundColor Green
    Write-Host "  SYNC COMPLETE!" -ForegroundColor Green
    Write-Host "-----------------------------------------------------------" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Source:         $SourcePath" -ForegroundColor White
    Write-Host "  Template:       $TemplateName" -ForegroundColor White
    Write-Host "  Service Tracker: $ServiceTrackerPath" -ForegroundColor White
    Write-Host "  Files synced:   $targetCount" -ForegroundColor White
    
    # Check if images were built
    $imagesDir = Join-Path $targetPath "docker-images"
    if (Test-Path $imagesDir) {
        $imageTars = Get-ChildItem -Path $imagesDir -Filter "*.tar" -ErrorAction SilentlyContinue
        if ($imageTars) {
            Write-Host "  Docker images: $($imageTars.Count) pre-built" -ForegroundColor Green
        }
    }
    
    Write-Host ""
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "    1. Open Service Tracker web UI" -ForegroundColor Gray
    Write-Host "    2. Go to Deployment Bay" -ForegroundColor Gray
    Write-Host "    3. Click 'Add Project' and select template: $TemplateName" -ForegroundColor Gray
    Write-Host "    4. Configure target host and deploy!" -ForegroundColor Gray
    
    if (Test-Path $imagesDir) {
        Write-Host ""
        Write-Host "  Pre-built Images Note:" -ForegroundColor Yellow
        Write-Host "    Docker images were built locally and saved as .tar files." -ForegroundColor Gray
        Write-Host "    On the server, the images will be loaded automatically before" -ForegroundColor Gray
        Write-Host "    running docker compose, avoiding memory-intensive builds." -ForegroundColor Gray
    }
    
    Write-Host ""
    
    return $true
}

# Export the main function
Export-ModuleMember -Function Sync-ToServiceTracker, Find-ServiceTrackerPath, Test-ProjectValid, Get-ComposeBuildServices, Build-DockerImages
