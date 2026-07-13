<#
.SYNOPSIS
    Builds the thin client deployment package.

.DESCRIPTION
    Compiles the thin client project and creates a deployment package.

.PARAMETER Configuration
    Build configuration (Debug/Release). Default: Release

.PARAMETER OutputPath
    Output path for the deployment package.
#>

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\deploy"
)

$ProjectPath = "..\src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"
$CertsPath = ".\certs"
$PfxPath = Join-Path $CertsPath "MKSCodeSign.pfx"
$CerPath = Join-Path $CertsPath "MKSCodeSign.cer"
$PfxPassword = $env:PFX_PASSWORD
if (-not $PfxPassword) {
    Write-Warning "PFX_PASSWORD environment variable is not set. Code signing will be skipped."
}

Write-Host "Building Computer Inventory Thin Client..." -ForegroundColor Cyan
Write-Host ""

# Check for signing certificate
$canSign = $false
if ((Test-Path $PfxPath) -and $PfxPassword) {
    $canSign = $true
    Write-Host "Code signing certificate found." -ForegroundColor Green
} elseif (-not $PfxPassword) {
    Write-Host "WARNING: PFX_PASSWORD not set. Building without code signing..." -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "WARNING: Code signing certificate not found at $PfxPath" -ForegroundColor Yellow
    Write-Host "Run Create-CodeSigningCert.ps1 first to create one." -ForegroundColor Yellow
    Write-Host "Building without code signing..." -ForegroundColor Yellow
    Write-Host ""
}

# Clean output
if (Test-Path $OutputPath) {
    Remove-Item $OutputPath -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Build and publish
Write-Host "Publishing project..." -ForegroundColor Green
dotnet publish $ProjectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$OutputPath\files"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Sign the executable
if ($canSign) {
    Write-Host "Signing executable..." -ForegroundColor Green
    $exePath = Join-Path "$OutputPath\files" "MKS.InventoryAgent.exe"
    
    # Use signtool if available, otherwise use Set-AuthenticodeSignature
    $signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($signtool) {
        & signtool sign /f $PfxPath /p $PfxPassword /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $exePath
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARNING: signtool signing failed, trying PowerShell method..." -ForegroundColor Yellow
            $cert = Get-PfxCertificate -FilePath $PfxPath
            Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
        }
    } else {
        # Use PowerShell's Set-AuthenticodeSignature
        # Note: Get-PfxCertificate doesn't accept -Password in older PS versions
        # Use X509Certificate2 directly instead
        try {
            $securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
            # Resolve to full path to avoid "path not found" errors
            $fullPfxPath = (Resolve-Path $PfxPath).Path
            $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($fullPfxPath, $securePassword)
            $result = Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
            if ($result.Status -eq "Valid") {
                Write-Host "Executable signed successfully." -ForegroundColor Green
            } else {
                Write-Host "WARNING: Signing result: $($result.Status)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "WARNING: Failed to sign executable: $_" -ForegroundColor Yellow
        }
    }
    
    # Copy the public certificate for client deployment
    if (Test-Path $CerPath) {
        Copy-Item $CerPath -Destination "$OutputPath\files\MKSCodeSign.cer"
        Write-Host "Included public certificate for client deployment." -ForegroundColor Green
    }
}

# Copy deployment scripts
Write-Host "Copying deployment scripts..." -ForegroundColor Green
Copy-Item ".\install.ps1" -Destination "$OutputPath\files\"
Copy-Item ".\install.cmd" -Destination "$OutputPath\files\"
Copy-Item ".\install-native.cmd" -Destination "$OutputPath\files\"
Copy-Item ".\uninstall.ps1" -Destination "$OutputPath\files\"

# Get version from the built executable
$exePath = Join-Path "$OutputPath\files" "MKS.InventoryAgent.exe"
$version = (Get-Item $exePath).VersionInfo.FileVersion
Write-Host "Built version: $version" -ForegroundColor Cyan

# Create ZIP package with version in filename
Write-Host "Creating deployment package..." -ForegroundColor Green
$zipPath = Join-Path $OutputPath "ComputerInventoryAgent-$version.zip"
Compress-Archive -Path "$OutputPath\files\*" -DestinationPath $zipPath -Force

# Also create a "latest" copy for scripts that expect the old name
$latestZipPath = Join-Path $OutputPath "ComputerInventoryAgent.zip"
Copy-Item $zipPath -Destination $latestZipPath -Force

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " Build Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host "Deployment package: $zipPath" -ForegroundColor Gray
Write-Host "Latest link: $latestZipPath" -ForegroundColor Gray
Write-Host ""
Write-Host "To deploy:"
Write-Host "  1. Copy the ZIP to target computer"
Write-Host "  2. Extract and run install.ps1 as Administrator"
