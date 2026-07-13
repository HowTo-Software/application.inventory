# Service Tracker - Project Sync Framework

A reusable PowerShell module for syncing projects to Service Tracker's deployment system.

## Files

| File | Description |
|------|-------------|
| `Sync-ToServiceTracker.psm1` | The main PowerShell module (shared) |
| `sync.ps1` | Wrapper script for this project |
| `sync-template.ps1` | Template to copy to other projects |

## Quick Start for New Projects

### 1. Copy the template to your project

```powershell
# Copy the sync script template to your project
Copy-Item "C:\Users\hank\Desktop\Computer_Invintory\tools\sync-template.ps1" "C:\Path\To\Project-Greg\sync.ps1"
```

### 2. Edit the template name

Open `sync.ps1` in your project and change:

```powershell
$TemplateName = "YOUR-PROJECT-NAME"  # Change to "project-greg"
```

### 3. Ensure the module is accessible

The script looks for `Sync-ToServiceTracker.psm1` in these locations:
1. `C:\Users\hank\Desktop\Service Tracker\tools\` (recommended central location)
2. `<project>\tools\Sync-ToServiceTracker.psm1`
3. `<project>\Sync-ToServiceTracker.psm1`
4. User's PowerShell modules folder

**Recommended:** Copy the module to Service Tracker:

```powershell
# Create tools directory in Service Tracker
New-Item -ItemType Directory -Path "C:\Users\hank\Desktop\Service Tracker\tools" -Force

# Copy the module
Copy-Item "C:\Users\hank\Desktop\Computer_Invintory\tools\Sync-ToServiceTracker.psm1" "C:\Users\hank\Desktop\Service Tracker\tools\"
```

### 4. Run from your project

```powershell
cd "C:\Path\To\Project-Greg"
.\sync.ps1              # Full sync with Docker build
.\sync.ps1 -SkipBuild   # Quick sync, skip Docker images
.\sync.ps1 -DryRun      # Preview without making changes
```

## Usage Options

| Parameter | Description |
|-----------|-------------|
| `-SkipBuild` | Skip Docker image building (faster sync) |
| `-BuildImages` | Force building Docker images |
| `-DryRun` | Preview changes without syncing |
| `-Force` | Continue even if validation fails |
| `-ServiceTrackerPath` | Override Service Tracker location |

## Project Requirements

For a project to sync properly, it should have:

- **Required:** `docker-compose.yml` (or `.yaml` or `compose.yml`)
- **Optional:** Dockerfile(s) for building images locally

The module auto-detects project types:
- **.NET** - `*.sln` or `*.csproj` files
- **Python** - `requirements.txt`
- **Node.js** - `package.json`
- **Go** - `go.mod`
- **Rust** - `Cargo.toml`

## Environment Variables

You can set `SERVICE_TRACKER_PATH` to avoid specifying it each time:

```powershell
# Add to your PowerShell profile
$env:SERVICE_TRACKER_PATH = "C:\Users\hank\Desktop\Service Tracker"
```

## Example: Setting Up Project-Greg

```powershell
# 1. Go to Project-Greg
cd "C:\Path\To\Project-Greg"

# 2. Copy the sync template
Copy-Item "C:\Users\hank\Desktop\Computer_Invintory\tools\sync-template.ps1" ".\sync.ps1"

# 3. Edit sync.ps1 and change $TemplateName to "project-greg"
notepad .\sync.ps1

# 4. Run sync
.\sync.ps1
```

## What Gets Synced

The module syncs all files except:
- `bin/`, `obj/` (.NET build artifacts)
- `node_modules/`
- `.git/`
- `.vs/`, `.idea/` (IDE folders)
- `__pycache__/` (Python cache)
- `target/` (Rust/Java builds)
- `*.user`, `*.suo`, `*.log`
- `.env.local`, `.env.*.local`

## Pre-built Docker Images

When Docker is running, the module:
1. Parses `docker-compose.yml` for services with `build:` sections
2. Builds each image locally
3. Saves them as `.tar` files in `docker-images/`
4. Creates a manifest for Service Tracker

This allows deployment to low-memory servers without building on the server.
