#===============================================================================
# Service Tracker - Project Sync Script
# 
# Purpose: Sync perfected code from Visual Studio development to Service Tracker
#          templates directory for deployment. Optionally builds Docker images
#          locally and packages them for transfer to low-memory servers.
#
# This script can be run from ANYWHERE - just specify the Service Tracker path
# or set the environment variable SERVICE_TRACKER_PATH
#
# Usage (from any directory):
#   .\sync-project.ps1 -SourcePath "C:\Path\To\Project" -TemplateName "my-project" -ServiceTrackerPath "C:\Users\hank\Desktop\Service Tracker"
#   
# Usage (from within project directory - auto-detect source):
#   .\sync-project.ps1 -TemplateName "steam-survey" -ServiceTrackerPath "C:\Users\hank\Desktop\Service Tracker"
#   
# Usage (with environment variable set):
#   $env:SERVICE_TRACKER_PATH = "C:\Users\hank\Desktop\Service Tracker"
#   .\sync-project.ps1 -TemplateName "my-project"
#
# Usage (skip Docker build for faster sync):
#   .\sync-project.ps1 -TemplateName "my-project" -SkipBuild
#
# Usage (from Service Tracker directory - original behavior):
#   .\sync-project.ps1 -SourcePath "C:\Path\To\Project" -TemplateName "my-project"
#
# Parameters:
#   -BuildImages    Force building Docker images locally (default: auto-detect)
#   -SkipBuild      Skip Docker image building (files only sync)
#===============================================================================

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

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Determine Service Tracker path (priority: parameter > env var > script location)
if (-not $ServiceTrackerPath) {
    if ($env:SERVICE_TRACKER_PATH) {
        $ServiceTrackerPath = $env:SERVICE_TRACKER_PATH
    } elseif (Test-Path (Join-Path $ScriptRoot "packages\backend\templates")) {
        # Script is in Service Tracker directory
        $ServiceTrackerPath = $ScriptRoot
    } else {
        # Try common locations
        $commonPaths = @(
            "C:\Users\hank\Desktop\Service Tracker",
            "$env:USERPROFILE\Desktop\Service Tracker",
            "$env:USERPROFILE\Projects\Service Tracker"
        )
        foreach ($path in $commonPaths) {
            if (Test-Path (Join-Path $path "packages\backend\templates")) {
                $ServiceTrackerPath = $path
                break
            }
        }
    }
}

# Validate Service Tracker path
if (-not $ServiceTrackerPath -or -not (Test-Path $ServiceTrackerPath)) {
    Write-Host ""
    Write-Host "ERROR: Service Tracker path not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please specify the path using one of these methods:" -ForegroundColor Yellow
    Write-Host '  1. Parameter:    -ServiceTrackerPath "C:\Path\To\Service Tracker"' -ForegroundColor Gray
    Write-Host '  2. Environment:  $env:SERVICE_TRACKER_PATH = "C:\Path\To\Service Tracker"' -ForegroundColor Gray
    Write-Host ""
    exit 1
}

$TemplatesDir = Join-Path $ServiceTrackerPath "packages\backend\templates"

if (-not (Test-Path $TemplatesDir)) {
    Write-Host ""
    Write-Host "ERROR: Templates directory not found at: $TemplatesDir" -ForegroundColor Red
    Write-Host "Is this a valid Service Tracker installation?" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# If SourcePath not provided, use current directory
if (-not $SourcePath) {
    $SourcePath = Get-Location
    Write-Host "Using current directory as source: $SourcePath" -ForegroundColor Cyan
}

# Colors
function Write-Header { param($msg) Write-Host "`n$msg" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warning { param($msg) Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Err { param($msg) Write-Host "  [ERROR] $msg" -ForegroundColor Red }
function Write-Info { param($msg) Write-Host "  -> $msg" -ForegroundColor Gray }

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

#-------------------------------------------------------------------------------
# Validation Phase
#-------------------------------------------------------------------------------
Write-Header "[1/5] Validating Source Project..."

# Check source exists
if (-not (Test-Path $SourcePath)) {
    Write-Err "Source path not found: $SourcePath"
    exit 1
}
Write-Success "Source path exists"

# Get source info
$sourceInfo = Get-Item $SourcePath
Write-Info "Source: $($sourceInfo.FullName)"

# Required files to check (in order of priority)
$requiredFiles = @(
    @{ Path = "docker-compose.yml"; Required = $true; Description = "Docker Compose file" },
    @{ Path = "docker-compose.yaml"; Required = $false; Description = "Docker Compose file (alt)" },
    @{ Path = "compose.yml"; Required = $false; Description = "Compose file (new format)" },
    @{ Path = "Dockerfile"; Required = $false; Description = "Dockerfile" }
)

# .NET specific checks
$dotnetFiles = @(
    @{ Path = "*.sln"; Required = $false; Description = ".NET Solution file" },
    @{ Path = "*.csproj"; Required = $false; Description = ".NET Project file" }
)

# Python specific checks  
$pythonFiles = @(
    @{ Path = "requirements.txt"; Required = $false; Description = "Python requirements" },
    @{ Path = "*.py"; Required = $false; Description = "Python files" }
)

$foundComposeFile = $false
$validationErrors = @()
$validationWarnings = @()

foreach ($file in $requiredFiles) {
    $fullPath = Join-Path $SourcePath $file.Path
    if (Test-Path $fullPath) {
        Write-Success "Found: $($file.Description) ($($file.Path))"
        if ($file.Path -like "*compose*") {
            $foundComposeFile = $true
        }
    } elseif ($file.Required -and -not $foundComposeFile) {
        $validationErrors += "Missing required: $($file.Description)"
    }
}

if (-not $foundComposeFile) {
    $validationErrors += "No docker-compose file found (docker-compose.yml, docker-compose.yaml, or compose.yml)"
}

# Check for common project types
$projectType = "unknown"

# .NET detection
$slnFiles = Get-ChildItem -Path $SourcePath -Filter "*.sln" -File -ErrorAction SilentlyContinue
if ($slnFiles) {
    $projectType = ".NET"
    Write-Success "Detected .NET project: $($slnFiles[0].Name)"
    
    # Check if published
    $publishDir = Join-Path $SourcePath "publish"
    if (Test-Path $publishDir) {
        Write-Success "Found publish directory"
    } else {
        $validationWarnings += "No publish directory - ensure Dockerfile builds from source or pre-publish"
    }
}

# Python detection
$requirementsFile = Join-Path $SourcePath "requirements.txt"
if (Test-Path $requirementsFile) {
    $projectType = "Python"
    Write-Success "Detected Python project (requirements.txt)"
}

# Node.js detection
$packageJson = Join-Path $SourcePath "package.json"
if (Test-Path $packageJson) {
    $projectType = "Node.js"
    Write-Success "Detected Node.js project (package.json)"
}

Write-Info "Project type: $projectType"

# Exclusion patterns for sync
$excludePatterns = @(
    "bin",
    "obj",
    "node_modules",
    ".git",
    ".vs",
    ".idea",
    "__pycache__",
    "*.user",
    "*.suo",
    "*.log",
    ".env.local",
    ".env.*.local"
)

#-------------------------------------------------------------------------------
# Validation Results
#-------------------------------------------------------------------------------
Write-Header "[2/5] Validation Results..."

if ($validationErrors.Count -gt 0) {
    Write-Host ""
    Write-Host "  ERRORS:" -ForegroundColor Red
    foreach ($err in $validationErrors) {
        Write-Err $err
    }
    
    if (-not $Force) {
        Write-Host ""
        Write-Host "  Use -Force to sync anyway, or fix the issues above." -ForegroundColor Yellow
        exit 1
    } else {
        Write-Warning "Continuing due to -Force flag..."
    }
}

if ($validationWarnings.Count -gt 0) {
    Write-Host ""
    Write-Host "  WARNINGS:" -ForegroundColor Yellow
    foreach ($warn in $validationWarnings) {
        Write-Warning $warn
    }
}

if ($validationErrors.Count -eq 0) {
    Write-Success "Validation passed!"
}

#-------------------------------------------------------------------------------
# Calculate Changes
#-------------------------------------------------------------------------------
Write-Header "[3/5] Calculating Changes..."

$targetPath = Join-Path $TemplatesDir $TemplateName

# Get source files (excluding patterns)
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

Write-Info "Files to sync: $totalFiles"
Write-Info "Total size: $([math]::Round($totalSize / 1MB, 2)) MB"

# Check if target exists
if (Test-Path $targetPath) {
    $existingFiles = Get-ChildItem -Path $targetPath -Recurse -File -ErrorAction SilentlyContinue
    Write-Warning "Target exists with $($existingFiles.Count) files - will be replaced"
} else {
    Write-Info "Target is new directory"
}

#-------------------------------------------------------------------------------
# Preview (Dry Run)
#-------------------------------------------------------------------------------
if ($DryRun) {
    Write-Header "[4/5] DRY RUN - No changes made"
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
    exit 0
}

#-------------------------------------------------------------------------------
# Execute Sync
#-------------------------------------------------------------------------------
Write-Header "[4/5] Syncing Files..."

# Create target directory
if (-not (Test-Path $TemplatesDir)) {
    New-Item -ItemType Directory -Path $TemplatesDir -Force | Out-Null
}

# Remove existing target if it exists
if (Test-Path $targetPath) {
    Write-Info "Removing existing target directory..."
    Remove-Item -Path $targetPath -Recurse -Force
}

# Create fresh target
New-Item -ItemType Directory -Path $targetPath -Force | Out-Null

# Use robocopy for efficient sync (Windows native)
$robocopyArgs = @(
    $SourcePath,
    $targetPath,
    "/E",           # Copy subdirectories including empty ones
    "/NFL",         # No file list
    "/NDL",         # No directory list
    "/NJH",         # No job header
    "/NJS",         # No job summary
    "/NC",          # No file classes
    "/NS"           # No file sizes
)

# Add exclusions
foreach ($pattern in $excludePatterns) {
    $robocopyArgs += "/XD"
    $robocopyArgs += $pattern
}

Write-Info "Running robocopy..."
$robocopyResult = & robocopy @robocopyArgs 2>&1

# Robocopy returns codes 0-7 for success
if ($LASTEXITCODE -le 7) {
    Write-Success "Files copied successfully"
} else {
    Write-Err "Robocopy returned error code: $LASTEXITCODE"
    Write-Host $robocopyResult -ForegroundColor Red
}

#-------------------------------------------------------------------------------
# Build Docker Images Locally (Optional but recommended)
#-------------------------------------------------------------------------------
$shouldBuildImages = $BuildImages -or (-not $SkipBuild)

if ($shouldBuildImages -and $foundComposeFile) {
    Write-Header "[4.5/5] Building Docker Images Locally..."
    
    # Check if Docker is available and running
    $dockerAvailable = $false
    try {
        $dockerVersion = & docker version --format "{{.Server.Version}}" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $dockerAvailable = $true
            Write-Info "Docker daemon is running (version: $dockerVersion)"
        }
    } catch {
        # Docker not available
    }
    
    if (-not $dockerAvailable) {
        Write-Warning "Docker daemon is not running or not accessible"
        Write-Warning "Skipping local image build - images will be built on the server"
        Write-Info "To build locally, ensure Docker Desktop is running and try again"
    } else {
        # Find the compose file
        $composeFile = $null
        foreach ($variant in @("docker-compose.yml", "docker-compose.yaml", "compose.yml")) {
            $testPath = Join-Path $SourcePath $variant
            if (Test-Path $testPath) {
                $composeFile = $testPath
                break
            }
        }
        
        if ($composeFile) {
            Write-Info "Using compose file: $composeFile"
            
            # Create images directory in target
            $imagesDir = Join-Path $targetPath "docker-images"
            if (-not (Test-Path $imagesDir)) {
                New-Item -ItemType Directory -Path $imagesDir -Force | Out-Null
            }
            
            # Get project name from template
            $projectName = $TemplateName.ToLower() -replace '[^a-z0-9]', ''
            
            # Parse compose file to find services with build contexts
            $composeContent = Get-Content $composeFile -Raw
            $servicesToBuild = @()
            
            # Simple parsing to find services with build contexts
            $inServices = $false
            $currentService = $null
            $currentIndent = 0
            $hasBuild = $false
            $buildContext = "."
            $dockerfile = $null
            
            foreach ($line in (Get-Content $composeFile)) {
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
            
            if ($servicesToBuild.Count -eq 0) {
                Write-Info "No services with build contexts found"
            } else {
                Write-Info "Found $($servicesToBuild.Count) services to build: $($servicesToBuild.Name -join ', ')"
                
                Push-Location $SourcePath
                try {
                    foreach ($service in $servicesToBuild) {
                        $imageName = "${projectName}-$($service.Name):latest"
                        $tarFile = Join-Path $imagesDir "$($service.Name).tar"
                        
                        # Determine dockerfile path
                        $dockerfilePath = if ($service.Dockerfile) {
                            Join-Path $service.Context $service.Dockerfile
                        } else {
                            Join-Path $service.Context "Dockerfile"
                        }
                        
                        Write-Info "Building $($service.Name) from $dockerfilePath..."
                        
                        # Build the image (use Start-Process to avoid stderr being treated as error)
                        $buildProcess = Start-Process -FilePath "docker" -ArgumentList "build", "-t", $imageName, "-f", $dockerfilePath, $service.Context -Wait -PassThru -NoNewWindow
                        
                        if ($buildProcess.ExitCode -ne 0) {
                            Write-Err "Failed to build $($service.Name)"
                            continue
                        }
                        
                        Write-Success "Built image: $imageName"
                        
                        # Save to tar
                        Write-Info "Saving $($service.Name).tar..."
                        & docker save -o $tarFile $imageName 2>&1 | Out-Null
                        
                        if ($LASTEXITCODE -eq 0) {
                            $tarSize = [math]::Round((Get-Item $tarFile).Length / 1MB, 2)
                            Write-Success "Saved $($service.Name).tar ($tarSize MB)"
                        } else {
                            Write-Err "Failed to save $($service.Name).tar"
                        }
                    }
                    
                    # Create manifest
                    $imageManifest = @{
                        projectName = $projectName
                        templateName = $TemplateName
                        builtAt = (Get-Date).ToString("o")
                        images = @()
                    }
                    
                    $tarFiles = Get-ChildItem -Path $imagesDir -Filter "*.tar" -ErrorAction SilentlyContinue
                    foreach ($tar in $tarFiles) {
                        $imageManifest.images += @{
                            service = $tar.BaseName
                            file = $tar.Name
                            imageName = "${projectName}-$($tar.BaseName):latest"
                            sizeMB = [math]::Round($tar.Length / 1MB, 2)
                        }
                    }
                    
                    $manifestPath = Join-Path $imagesDir "manifest.json"
                    $imageManifest | ConvertTo-Json -Depth 3 | Set-Content -Path $manifestPath
                    Write-Success "Created image manifest"
                    
                } finally {
                    Pop-Location
                }
            }
        }
    }
} elseif ($SkipBuild) {
    Write-Info "Skipping Docker image build (-SkipBuild specified)"
}

#-------------------------------------------------------------------------------
# Post-Sync Verification
#-------------------------------------------------------------------------------
Write-Header "[5/5] Verifying Sync..."

$targetFiles = Get-ChildItem -Path $targetPath -Recurse -File -ErrorAction SilentlyContinue
$targetCount = $targetFiles.Count

Write-Info "Source files: $totalFiles"
Write-Info "Target files: $targetCount"

# Verify compose file exists in target
$composeVariants = @("docker-compose.yml", "docker-compose.yaml", "compose.yml")
$foundTargetCompose = $false
foreach ($variant in $composeVariants) {
    if (Test-Path (Join-Path $targetPath $variant)) {
        Write-Success "Verified: $variant exists in target"
        $foundTargetCompose = $true
        break
    }
}

if (-not $foundTargetCompose) {
    Write-Err "No compose file found in target!"
}

# Create sync metadata
$syncMetadata = @{
    templateName = $TemplateName
    sourcePath = $SourcePath
    serviceTrackerPath = $ServiceTrackerPath
    syncedAt = (Get-Date).ToString("o")
    projectType = $projectType
    fileCount = $targetCount
    syncedBy = $env:USERNAME
    machine = $env:COMPUTERNAME
}

$metadataPath = Join-Path $targetPath ".sync-metadata.json"
$syncMetadata | ConvertTo-Json -Depth 3 | Set-Content -Path $metadataPath

Write-Success "Created sync metadata"

#-------------------------------------------------------------------------------
# Summary
#-------------------------------------------------------------------------------
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
