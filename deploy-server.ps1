<#
.SYNOPSIS
    Deploys the Computer Inventory system to a remote server.

.PARAMETER ServerHost
    The SSH host IP address or hostname (local network address)

.PARAMETER ExternalUrl
    Optional external/public URL for clients outside the local network.
    If not specified, only the local ServerHost will be used.

.PARAMETER SshUser
    The SSH username (defaults to 'hank')

.PARAMETER ServerPath
    The path on the server to deploy to

.PARAMETER DbServer
    SQL Server host (e.g., 10.241.24.41,1433). If not provided, uses existing .env value on server.

.PARAMETER DbName
    Database name. Default: device_management

.PARAMETER DbUser
    Database login username.

.PARAMETER DbPassword
    Database login password.

.PARAMETER AgentApiKey
    API key for agent authentication. Auto-generated if not specified and no existing .env found.

.PARAMETER SkipBuild
    Skip building the solution
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ServerHost,
    
    [string]$ExternalUrl = "",
    
    [string]$SshUser = "hank",
    
    [string]$ServerPath = "~/computer-inventory",
    
    [string]$DbServer = "",
    
    [string]$DbName = "device_management",
    
    [string]$DbUser = "",
    
    [string]$DbPassword = "",
    
    [string]$AgentApiKey = "",
    
    [switch]$SkipBuild
)

# Build SSH target - if ServerHost already contains user@host, use it directly
if ($ServerHost -match '@') {
    $SshTarget = $ServerHost
} else {
    $SshTarget = "${SshUser}@${ServerHost}"
}

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Computer Inventory Server Deployment" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Target: $SshTarget" -ForegroundColor Yellow
Write-Host "Path: $ServerPath" -ForegroundColor Yellow
Write-Host ""

# Get current version from ThinClient csproj (needed for version tracking)
$thinClientCsproj = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"
[xml]$csprojXml = Get-Content $thinClientCsproj
$currentVersion = $csprojXml.Project.PropertyGroup.Version
if (-not $currentVersion) { $currentVersion = "1.0.0.0" }

# Variables to track version info for later use
$newVersion = $currentVersion
$checksum = ""
$fileSize = 0

# Build solution
if (-not $SkipBuild) {
    Write-Host "[1/6] Building solution..." -ForegroundColor Green
    Push-Location $ProjectRoot
    
    # Parse and increment the version
    $versionParts = $currentVersion.Split('.')
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $build = [int]$versionParts[2]
    $revision = [int]$versionParts[3]
    $revision++
    $newVersion = "$major.$minor.$build.$revision"
    
    Write-Host "  Version: $currentVersion -> $newVersion" -ForegroundColor Cyan
    
    # Update the csproj with new version
    $csprojXml.Project.PropertyGroup.Version = $newVersion
    $csprojXml.Project.PropertyGroup.FileVersion = $newVersion
    $csprojXml.Project.PropertyGroup.AssemblyVersion = $newVersion
    $csprojXml.Save($thinClientCsproj)
    
    # Clean ALL build artifacts before building to prevent file lock issues
    Write-Host "  Cleaning build artifacts..." -ForegroundColor Gray
    $projectDirs = @(
        "src\ComputerInventory.Core",
        "src\ComputerInventory.Infrastructure",
        "src\ComputerInventory.Api",
        "src\ComputerInventory.Web",
        "src\ComputerInventory.ThinClient",
        "src\ComputerInventory.ThinClient.Updater"
    )
    foreach ($dir in $projectDirs) {
        $binDir = Join-Path $ProjectRoot "$dir\bin"
        $objDir = Join-Path $ProjectRoot "$dir\obj"
        if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force -ErrorAction SilentlyContinue }
        if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
    
    # Also clean the agent output directory (handle locked files from running agent)
    $agentOutputDir = Join-Path $ProjectRoot "src\ComputerInventory.Api\agent"
    if (Test-Path $agentOutputDir) {
        # Rename locked executables before cleanup so Remove-Item succeeds
        Get-ChildItem $agentOutputDir -Recurse -Filter "*.exe" -ErrorAction SilentlyContinue | ForEach-Object {
            try { Rename-Item $_.FullName "$($_.FullName).locked" -ErrorAction Stop } catch {}
        }
        Remove-Item $agentOutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    
    # Build Thin Client
    Write-Host "[1.5/6] Building Thin Client agent..." -ForegroundColor Green
    $thinClientProject = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"
    $agentOutputDir = Join-Path $ProjectRoot "src\ComputerInventory.Api\agent"
    
    # Create agent output directory (handle locked files from running agent)
    if (Test-Path $agentOutputDir) {
        Get-ChildItem $agentOutputDir -Recurse -Filter "*.exe" -ErrorAction SilentlyContinue | ForEach-Object {
            try { Rename-Item $_.FullName "$($_.FullName).locked" -ErrorAction Stop } catch {}
        }
        Remove-Item $agentOutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $agentOutputDir -Force | Out-Null
    
    # Publish thin client as self-contained single file
    $agentFilesDir = Join-Path $agentOutputDir "files"
    dotnet publish $thinClientProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $agentFilesDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Thin client build failed!" -ForegroundColor Red
        exit 1
    }
    
    # Publish the AgentUpdater.exe
    Write-Host "  Building AgentUpdater.exe..." -ForegroundColor Gray
    $updaterProject = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient.Updater\ComputerInventory.ThinClient.Updater.csproj"
    dotnet publish $updaterProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $agentFilesDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "AgentUpdater build failed!" -ForegroundColor Red
        exit 1
    }
    
    # Code signing - auto-generate certificate if needed
    $certsPath = Join-Path $ProjectRoot "thin-client\certs"
    $pfxPath = Join-Path $certsPath "MKSCodeSign.pfx"
    $cerPath = Join-Path $certsPath "MKSCodeSign.cer"
    $CodeSignPassword = "MKS-CodeSign-2026!"
    $CertName = "MKS Computer Inventory Code Signing"
    
    # Auto-generate certificate if it doesn't exist
    if (-not (Test-Path $pfxPath)) {
        Write-Host "  Creating code signing certificate..." -ForegroundColor Yellow
        
        # Create certs directory
        if (-not (Test-Path $certsPath)) {
            New-Item -ItemType Directory -Path $certsPath -Force | Out-Null
        }
        
        # Check if cert exists in store, reuse it
        $existingCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=$CertName" }
        if ($existingCert) {
            Write-Host "    Found existing certificate in store" -ForegroundColor Gray
            $cert = $existingCert
        } else {
            # Create new self-signed certificate
            $cert = New-SelfSignedCertificate `
                -Type CodeSigningCert `
                -Subject "CN=$CertName" `
                -FriendlyName $CertName `
                -CertStoreLocation Cert:\CurrentUser\My `
                -NotAfter (Get-Date).AddYears(5) `
                -KeyUsage DigitalSignature `
                -KeyAlgorithm RSA `
                -KeyLength 2048 `
                -HashAlgorithm SHA256
            Write-Host "    Created new certificate: $($cert.Thumbprint)" -ForegroundColor Green
        }
        
        # Export public certificate
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
        Write-Host "    Exported: MKSCodeSign.cer" -ForegroundColor Gray
        
        # Export PFX with private key
        $securePassword = ConvertTo-SecureString -String $CodeSignPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
        Write-Host "    Exported: MKSCodeSign.pfx" -ForegroundColor Gray
        
        # Save thumbprint
        $thumbprintPath = Join-Path $certsPath "thumbprint.txt"
        $cert.Thumbprint | Out-File -FilePath $thumbprintPath -NoNewline
    }
    
    if (Test-Path $pfxPath) {
        Write-Host "  Signing executables for HVCI compliance..." -ForegroundColor Gray
        try {
            $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($pfxPath, $CodeSignPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
            
            # Sign the main executable
            $exePath = Join-Path $agentFilesDir "MKS.InventoryAgent.exe"
            $result = Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
            # UnknownError means signed but cert not trusted on THIS machine - valid for deployment
            if ($result.Status -eq "Valid" -or $result.Status -eq "UnknownError") {
                Write-Host "    Signed: MKS.InventoryAgent.exe" -ForegroundColor Green
            } else {
                Write-Host "    Warning: Signing ThinClient: $($result.Status) - $($result.StatusMessage)" -ForegroundColor Yellow
            }
            
            # Sign the updater
            $updaterExePath = Join-Path $agentFilesDir "AgentUpdater.exe"
            if (Test-Path $updaterExePath) {
                $result = Set-AuthenticodeSignature -FilePath $updaterExePath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com"
                if ($result.Status -eq "Valid" -or $result.Status -eq "UnknownError") {
                    Write-Host "    Signed: AgentUpdater.exe" -ForegroundColor Green
                } else {
                    Write-Host "    Warning: Signing AgentUpdater.exe: $($result.Status) - $($result.StatusMessage)" -ForegroundColor Yellow
                }
            }
            
        } catch {
            Write-Host "    Warning: Code signing failed: $_" -ForegroundColor Yellow
        }
        
        # Copy public certificate to agent files for client deployment (always include if exists)
        if (Test-Path $cerPath) {
            Copy-Item $cerPath -Destination $agentFilesDir
            Write-Host "    Included: MKSCodeSign.cer (for client HVCI trust)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  No code signing certificate - run thin-client\Create-CodeSigningCert.ps1" -ForegroundColor Yellow
    }
    
    # Copy install/uninstall scripts
    Copy-Item (Join-Path $ProjectRoot "thin-client\install.ps1") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\install.cmd") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\install-native.cmd") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\uninstall.ps1") -Destination $agentFilesDir
    
    # Also update publish-thinclient/files with the latest install scripts
    $publishThinClientFiles = Join-Path $ProjectRoot "publish-thinclient\files"
    if (Test-Path $publishThinClientFiles) {
        Write-Host "  Updating publish-thinclient install scripts..." -ForegroundColor Gray
        Copy-Item (Join-Path $ProjectRoot "thin-client\install.ps1") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\install.cmd") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\install-native.cmd") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\uninstall.ps1") -Destination $publishThinClientFiles -Force
    }
    
    # Update appsettings.json with the correct server URL and fallback URLs
    $ServerIP = $ServerHost
    $agentConfigPath = Join-Path $agentFilesDir "appsettings.json"
    if (Test-Path $agentConfigPath) {
        $agentConfig = Get-Content $agentConfigPath | ConvertFrom-Json
        
        # Build the URL list: primary (external if available) + fallback (local)
        $localUrl = "http://${ServerIP}:5010"
        
        if (-not [string]::IsNullOrWhiteSpace($ExternalUrl)) {
            # External URL provided - use it as primary, local as fallback
            $primaryUrl = $ExternalUrl.TrimEnd('/')
            if (-not $primaryUrl.Contains(":")) {
                $primaryUrl = "${primaryUrl}:5010"
            }
            if (-not $primaryUrl.StartsWith("http")) {
                $primaryUrl = "http://${primaryUrl}"
            }
            
            $agentConfig.ApiSettings.BaseUrl = $primaryUrl
            
            # Ensure FallbackUrls property exists and add local URL as fallback
            if (-not $agentConfig.ApiSettings.PSObject.Properties["FallbackUrls"]) {
                $agentConfig.ApiSettings | Add-Member -MemberType NoteProperty -Name "FallbackUrls" -Value @()
            }
            $agentConfig.ApiSettings.FallbackUrls = @($localUrl)
            
            Write-Host "  Agent configured: Primary=$primaryUrl, Fallback=$localUrl" -ForegroundColor Gray
        } else {
            # No external URL - use local URL only
            $agentConfig.ApiSettings.BaseUrl = $localUrl
            
            # Ensure FallbackUrls property exists (empty)
            if (-not $agentConfig.ApiSettings.PSObject.Properties["FallbackUrls"]) {
                $agentConfig.ApiSettings | Add-Member -MemberType NoteProperty -Name "FallbackUrls" -Value @()
            }
            $agentConfig.ApiSettings.FallbackUrls = @()
            
            Write-Host "  Agent configured for server: $localUrl" -ForegroundColor Gray
        }
        
        $agentConfig | ConvertTo-Json -Depth 10 | Set-Content $agentConfigPath
    }
    
    # Wait for file handles to be released after dotnet publish
    Start-Sleep -Seconds 2
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Start-Sleep -Milliseconds 500
    
    # Ensure code signing certificate is included for HVCI compliance on clients
    $certSourcePath = Join-Path $ProjectRoot "thin-client\certs\MKSCodeSign.cer"
    if (Test-Path $certSourcePath) {
        Copy-Item $certSourcePath -Destination $agentFilesDir -Force
        Write-Host "  Included MKSCodeSign.cer for client HVCI trust" -ForegroundColor Green
    } else {
        Write-Host "  Warning: MKSCodeSign.cer not found - clients may fail HVCI checks" -ForegroundColor Yellow
    }
    
    # Copy files to temp directory to avoid file locking issues
    $tempZipDir = Join-Path $env:TEMP "AgentZipTemp_$(Get-Random)"
    New-Item -ItemType Directory -Path $tempZipDir -Force | Out-Null
    Copy-Item -Path "$agentFilesDir\*" -Destination $tempZipDir -Recurse -Force
    
    # Create ZIP package for download endpoint
    $agentZipPath = Join-Path $agentOutputDir "ComputerInventoryAgent.zip"
    # Remove existing zip if it exists to avoid conflicts
    if (Test-Path $agentZipPath) {
        Remove-Item $agentZipPath -Force
    }
    
    # Use .NET ZipFile for more reliable zipping
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempZipDir, $agentZipPath)
    
    # Clean up temp directory
    Remove-Item $tempZipDir -Recurse -Force -ErrorAction SilentlyContinue
    
    # Create update package for auto-update system
    Write-Host "  Creating auto-update package..." -ForegroundColor Gray
    $updateDir = Join-Path $agentOutputDir "update"
    New-Item -ItemType Directory -Path $updateDir -Force | Out-Null
    
    # Copy the same zip as the update package
    Copy-Item $agentZipPath -Destination (Join-Path $updateDir "agent-update.zip") -Force
    
    # Calculate checksum and create update.json
    $hash = Get-FileHash -Path $agentZipPath -Algorithm SHA256
    $checksum = $hash.Hash.ToLower()
    $fileSize = (Get-Item $agentZipPath).Length
    
    $updateInfo = @{
        Version = $newVersion
        Checksum = $checksum
        FileSize = $fileSize
        IsRequired = $false
        IsBreakingChange = $false
        ReleaseNotes = "Auto-deployed version $newVersion"
        ReleasedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    } | ConvertTo-Json -Depth 10
    
    Set-Content -Path (Join-Path $updateDir "update.json") -Value $updateInfo
    Write-Host "  Auto-update package v$newVersion created (checksum: $($checksum.Substring(0,16))...)" -ForegroundColor Gray
    
    # Clean up files dir (release any certificate handles first)
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Start-Sleep -Seconds 1
    Remove-Item $agentFilesDir -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Host "  Agent package created: ComputerInventoryAgent.zip" -ForegroundColor Gray
    
    Pop-Location
} else {
    # Even when skipping build, we need to build the agent package for updates
    Write-Host "[1/6] Building agent package only (SkipBuild)..." -ForegroundColor Green
    Push-Location $ProjectRoot
    
    Write-Host "  Using existing version: $newVersion" -ForegroundColor Cyan
    
    # Build Thin Client
    $thinClientProject = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\ComputerInventory.ThinClient.csproj"
    $agentOutputDir = Join-Path $ProjectRoot "src\ComputerInventory.Api\agent"
    
    # Clean ThinClient build artifacts to ensure fresh compilation
    Write-Host "  Cleaning ThinClient build artifacts..." -ForegroundColor Gray
    $thinClientBinDir = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\bin"
    $thinClientObjDir = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient\obj"
    if (Test-Path $thinClientBinDir) { Remove-Item $thinClientBinDir -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $thinClientObjDir) { Remove-Item $thinClientObjDir -Recurse -Force -ErrorAction SilentlyContinue }
    
    # Clean Updater build artifacts
    $updaterBinDir = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient.Updater\bin"
    $updaterObjDir = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient.Updater\obj"
    if (Test-Path $updaterBinDir) { Remove-Item $updaterBinDir -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $updaterObjDir) { Remove-Item $updaterObjDir -Recurse -Force -ErrorAction SilentlyContinue }
    
    # Create agent output directory
    if (Test-Path $agentOutputDir) {
        Remove-Item $agentOutputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $agentOutputDir -Force | Out-Null
    
    # Publish thin client as self-contained single file
    $agentFilesDir = Join-Path $agentOutputDir "files"
    dotnet publish $thinClientProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $agentFilesDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Thin client build failed!" -ForegroundColor Red
        exit 1
    }
    
    # Publish the AgentUpdater.exe
    Write-Host "  Building AgentUpdater.exe..." -ForegroundColor Gray
    $updaterProject = Join-Path $ProjectRoot "src\ComputerInventory.ThinClient.Updater\ComputerInventory.ThinClient.Updater.csproj"
    dotnet publish $updaterProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $agentFilesDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "AgentUpdater build failed!" -ForegroundColor Red
        exit 1
    }
    
    # Sign executables if certificate is available (for HVCI/WDAC compliance)
    # Auto-generate certificate if needed
    $certsPath = Join-Path $ProjectRoot "thin-client\certs"
    $certPath = Join-Path $certsPath "MKSCodeSign.pfx"
    $cerPath = Join-Path $certsPath "MKSCodeSign.cer"
    $CodeSignPassword = "MKS-CodeSign-2026!"
    $CertName = "MKS Computer Inventory Code Signing"
    
    if (-not (Test-Path $certPath)) {
        Write-Host "  Creating code signing certificate..." -ForegroundColor Yellow
        
        # Create certs directory
        if (-not (Test-Path $certsPath)) {
            New-Item -ItemType Directory -Path $certsPath -Force | Out-Null
        }
        
        # Check if cert exists in store, reuse it
        $existingCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=$CertName" }
        if ($existingCert) {
            Write-Host "    Found existing certificate in store" -ForegroundColor Gray
            $cert = $existingCert
        } else {
            # Create new self-signed certificate
            $cert = New-SelfSignedCertificate `
                -Type CodeSigningCert `
                -Subject "CN=$CertName" `
                -FriendlyName $CertName `
                -CertStoreLocation Cert:\CurrentUser\My `
                -NotAfter (Get-Date).AddYears(5) `
                -KeyUsage DigitalSignature `
                -KeyAlgorithm RSA `
                -KeyLength 2048 `
                -HashAlgorithm SHA256
            Write-Host "    Created new certificate: $($cert.Thumbprint)" -ForegroundColor Green
        }
        
        # Export public certificate
        Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
        Write-Host "    Exported: MKSCodeSign.cer" -ForegroundColor Gray
        
        # Export PFX with private key
        $securePassword = ConvertTo-SecureString -String $CodeSignPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword | Out-Null
        Write-Host "    Exported: MKSCodeSign.pfx" -ForegroundColor Gray
        
        # Save thumbprint
        $thumbprintPath = Join-Path $certsPath "thumbprint.txt"
        $cert.Thumbprint | Out-File -FilePath $thumbprintPath -NoNewline
    }
    
    if (Test-Path $certPath) {
        Write-Host "  Signing executables for HVCI compliance..." -ForegroundColor Gray
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($certPath, $CodeSignPassword, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        
        $thinClientExe = Join-Path $agentFilesDir "MKS.InventoryAgent.exe"
$updaterExe = Join-Path $agentFilesDir "AgentUpdater.exe"
        
        if (Test-Path $thinClientExe) {
            $result = Set-AuthenticodeSignature -FilePath $thinClientExe -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
            # UnknownError means signed but cert not trusted on THIS machine - valid for deployment
            if ($result.Status -eq "Valid" -or $result.Status -eq "UnknownError") {
                Write-Host "    Signed: MKS.InventoryAgent.exe" -ForegroundColor Green
            } else {
                Write-Host "    Warning: Failed to sign ThinClient.exe: $($result.StatusMessage)" -ForegroundColor Yellow
            }
        }
        
        if (Test-Path $updaterExe) {
            $result = Set-AuthenticodeSignature -FilePath $updaterExe -Certificate $cert -TimestampServer "http://timestamp.digicert.com" -HashAlgorithm SHA256
            if ($result.Status -eq "Valid" -or $result.Status -eq "UnknownError") {
                Write-Host "    Signed: AgentUpdater.exe" -ForegroundColor Green
            } else {
                Write-Host "    Warning: Failed to sign AgentUpdater.exe: $($result.StatusMessage)" -ForegroundColor Yellow
            }
        }
        
        # Copy the public certificate for client installation
        if (Test-Path $cerPath) {
            Copy-Item $cerPath -Destination $agentFilesDir
            Write-Host "    Included: MKSCodeSign.cer for client trust" -ForegroundColor Gray
        }
    }
    
    # Copy install/uninstall scripts
    Copy-Item (Join-Path $ProjectRoot "thin-client\install.ps1") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\install.cmd") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\install-native.cmd") -Destination $agentFilesDir
    Copy-Item (Join-Path $ProjectRoot "thin-client\uninstall.ps1") -Destination $agentFilesDir
    
    # Also update publish-thinclient/files with the latest install scripts
    $publishThinClientFiles = Join-Path $ProjectRoot "publish-thinclient\files"
    if (Test-Path $publishThinClientFiles) {
        Write-Host "  Updating publish-thinclient install scripts..." -ForegroundColor Gray
        Copy-Item (Join-Path $ProjectRoot "thin-client\install.ps1") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\install.cmd") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\install-native.cmd") -Destination $publishThinClientFiles -Force
        Copy-Item (Join-Path $ProjectRoot "thin-client\uninstall.ps1") -Destination $publishThinClientFiles -Force
    }
    
    # Update appsettings.json with the correct server URL and fallback URLs
    $ServerIP = $ServerHost
    $agentConfigPath = Join-Path $agentFilesDir "appsettings.json"
    if (Test-Path $agentConfigPath) {
        $agentConfig = Get-Content $agentConfigPath | ConvertFrom-Json
        
        # Build the URL list: primary (external if available) + fallback (local)
        $localUrl = "http://${ServerIP}:5010"
        
        if (-not [string]::IsNullOrWhiteSpace($ExternalUrl)) {
            # External URL provided - use it as primary, local as fallback
            $primaryUrl = $ExternalUrl.TrimEnd('/')
            if (-not $primaryUrl.Contains(":")) {
                $primaryUrl = "${primaryUrl}:5010"
            }
            if (-not $primaryUrl.StartsWith("http")) {
                $primaryUrl = "http://${primaryUrl}"
            }
            
            $agentConfig.ApiSettings.BaseUrl = $primaryUrl
            
            # Ensure FallbackUrls property exists and add local URL as fallback
            if (-not $agentConfig.ApiSettings.PSObject.Properties["FallbackUrls"]) {
                $agentConfig.ApiSettings | Add-Member -MemberType NoteProperty -Name "FallbackUrls" -Value @()
            }
            $agentConfig.ApiSettings.FallbackUrls = @($localUrl)
            
            Write-Host "  Agent configured: Primary=$primaryUrl, Fallback=$localUrl" -ForegroundColor Gray
        } else {
            # No external URL - use local URL only
            $agentConfig.ApiSettings.BaseUrl = $localUrl
            
            # Ensure FallbackUrls property exists (empty)
            if (-not $agentConfig.ApiSettings.PSObject.Properties["FallbackUrls"]) {
                $agentConfig.ApiSettings | Add-Member -MemberType NoteProperty -Name "FallbackUrls" -Value @()
            }
            $agentConfig.ApiSettings.FallbackUrls = @()
            
            Write-Host "  Agent configured for server: $localUrl" -ForegroundColor Gray
        }
        
        $agentConfig | ConvertTo-Json -Depth 10 | Set-Content $agentConfigPath
    }
    
    # Wait for file handles to be released after dotnet publish
    Start-Sleep -Seconds 2
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Start-Sleep -Milliseconds 500
    
    # Ensure code signing certificate is included for HVCI compliance on clients
    $certSourcePath = Join-Path $ProjectRoot "thin-client\certs\MKSCodeSign.cer"
    if (Test-Path $certSourcePath) {
        Copy-Item $certSourcePath -Destination $agentFilesDir -Force
        Write-Host "  Included MKSCodeSign.cer for client HVCI trust" -ForegroundColor Green
    } else {
        Write-Host "  Warning: MKSCodeSign.cer not found - clients may fail HVCI checks" -ForegroundColor Yellow
    }
    
    # Copy files to temp directory to avoid file locking issues
    $tempZipDir = Join-Path $env:TEMP "AgentZipTemp_$(Get-Random)"
    New-Item -ItemType Directory -Path $tempZipDir -Force | Out-Null
    Copy-Item -Path "$agentFilesDir\*" -Destination $tempZipDir -Recurse -Force
    
    # Create ZIP package for download endpoint
    $agentZipPath = Join-Path $agentOutputDir "ComputerInventoryAgent.zip"
    if (Test-Path $agentZipPath) {
        Remove-Item $agentZipPath -Force
    }
    
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempZipDir, $agentZipPath)
    
    # Clean up temp directory
    Remove-Item $tempZipDir -Recurse -Force -ErrorAction SilentlyContinue
    
    # Create update package for auto-update system
    Write-Host "  Creating auto-update package..." -ForegroundColor Gray
    $updateDir = Join-Path $agentOutputDir "update"
    New-Item -ItemType Directory -Path $updateDir -Force | Out-Null
    
    # Copy the same zip as the update package
    Copy-Item $agentZipPath -Destination (Join-Path $updateDir "agent-update.zip") -Force
    
    # Calculate checksum and create update.json
    $hash = Get-FileHash -Path $agentZipPath -Algorithm SHA256
    $checksum = $hash.Hash.ToLower()
    $fileSize = (Get-Item $agentZipPath).Length
    
    $updateInfo = @{
        Version = $newVersion
        Checksum = $checksum
        FileSize = $fileSize
        IsRequired = $false
        IsBreakingChange = $false
        ReleaseNotes = "Auto-deployed version $newVersion"
        ReleasedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    } | ConvertTo-Json -Depth 10
    
    Set-Content -Path (Join-Path $updateDir "update.json") -Value $updateInfo
    Write-Host "  Auto-update package v$newVersion created (checksum: $($checksum.Substring(0,16))...)" -ForegroundColor Gray
    
    # Clean up files dir (release any certificate handles first)
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
    Start-Sleep -Seconds 1
    Remove-Item $agentFilesDir -Recurse -Force -ErrorAction SilentlyContinue
    
    Write-Host "  Agent package created: ComputerInventoryAgent.zip" -ForegroundColor Gray
    
    Pop-Location
}

# Clean remote server of old installation (preserve .env)
Write-Host "[2/6] Cleaning old installation on remote server..." -ForegroundColor Green
ssh $SshTarget "cd ~/computer-inventory 2>/dev/null && cp .env /tmp/.env.backup 2>/dev/null; docker compose -f ~/computer-inventory/docker-compose.yml down --rmi all --volumes 2>/dev/null; docker-compose -f ~/computer-inventory/docker-compose.yml down --rmi all --volumes 2>/dev/null; sudo rm -rf ~/computer-inventory 2>/dev/null; mkdir -p ~/computer-inventory; if [ -f /tmp/.env.backup ]; then mv /tmp/.env.backup ~/computer-inventory/.env; echo 'Old installation cleaned. Existing .env preserved.'; else echo 'Old installation cleaned. No existing .env found.'; fi"

# Create deployment package
Write-Host "[3/6] Creating deployment package..." -ForegroundColor Green
$deployDir = Join-Path $ProjectRoot "deploy-server"
if (Test-Path $deployDir) {
    Remove-Item $deployDir -Recurse -Force
}
New-Item -ItemType Directory -Path $deployDir -Force | Out-Null

# Copy docker-compose and configs
Copy-Item (Join-Path $ProjectRoot "docker-compose.yml") -Destination $deployDir

# Build the Agent Server URL
$agentServerUrl = "http://${ServerHost}:5010"
if (-not [string]::IsNullOrWhiteSpace($ExternalUrl)) {
    $agentServerUrl = $ExternalUrl.TrimEnd('/')
    if (-not $agentServerUrl.StartsWith("http")) {
        $agentServerUrl = "http://${agentServerUrl}"
    }
    if (-not $agentServerUrl -match ":\d+") {
        $agentServerUrl = "${agentServerUrl}:5010"
    }
}
Write-Host "  Agent Server URL: $agentServerUrl" -ForegroundColor Gray

# Generate .env file
$envFile = Join-Path $deployDir ".env"
Write-Host "  Generating .env configuration..." -ForegroundColor Gray

# Try to read existing .env from server for values we don't override
$existingEnv = @{}
$existingEnvRaw = ssh $SshTarget "cat ${ServerPath}/.env 2>/dev/null" 2>$null
if ($existingEnvRaw) {
    foreach ($line in $existingEnvRaw -split "`n") {
        $line = $line.Trim()
        if ($line -and -not $line.StartsWith('#') -and $line.Contains('=')) {
            $eqIdx = $line.IndexOf('=')
            $key = $line.Substring(0, $eqIdx).Trim()
            $val = $line.Substring($eqIdx + 1).Trim()
            $existingEnv[$key] = $val
        }
    }
    Write-Host "  Loaded existing .env from server ($($existingEnv.Count) values)" -ForegroundColor Gray
}

# Helper: use parameter if provided, else existing .env value, else default
function Resolve-EnvValue {
    param([string]$ParamValue, [string]$EnvKey, [string]$Default = "")
    if (-not [string]::IsNullOrWhiteSpace($ParamValue)) { return $ParamValue }
    if ($existingEnv.ContainsKey($EnvKey) -and -not [string]::IsNullOrWhiteSpace($existingEnv[$EnvKey])) { return $existingEnv[$EnvKey] }
    return $Default
}

# Auto-generate secrets if needed
$generateRandom = { param([int]$len) -join ((65..90) + (97..122) + (48..57) | Get-Random -Count $len | ForEach-Object {[char]$_}) }

$resolvedJwtKey = Resolve-EnvValue "" "JWT_SECRET_KEY" ""
if ([string]::IsNullOrWhiteSpace($resolvedJwtKey) -or $resolvedJwtKey -eq "REPLACE_WITH_A_RANDOM_64_CHAR_SECRET_KEY") {
    $resolvedJwtKey = & $generateRandom 64
    Write-Host "  Generated new JWT secret key" -ForegroundColor Green
}

$resolvedAgentKey = Resolve-EnvValue $AgentApiKey "AGENT_API_KEY" ""
if ([string]::IsNullOrWhiteSpace($resolvedAgentKey)) {
    $resolvedAgentKey = & $generateRandom 32
    Write-Host "  Generated new Agent API Key: $resolvedAgentKey" -ForegroundColor Green
} else {
    Write-Host "  Agent API Key: $resolvedAgentKey" -ForegroundColor Gray
}

# Build DB connection string
$resolvedDbServer = Resolve-EnvValue $DbServer "" ""
$resolvedDbUser = Resolve-EnvValue $DbUser "" ""
$resolvedDbPassword = Resolve-EnvValue $DbPassword "" ""
$resolvedDbName = $DbName

if (-not [string]::IsNullOrWhiteSpace($resolvedDbServer) -and -not [string]::IsNullOrWhiteSpace($resolvedDbUser)) {
    # Escape $ as $$ for Docker Compose .env files
    $escapedDbPassword = $resolvedDbPassword -replace '\$', '$$$$'
    $dbConnString = "Server=${resolvedDbServer};Database=${resolvedDbName};User Id=${resolvedDbUser};Password=${escapedDbPassword};TrustServerCertificate=True;"
    Write-Host "  DB: ${resolvedDbServer} / ${resolvedDbName} (user: ${resolvedDbUser})" -ForegroundColor Gray
} else {
    # Fall back to existing .env value
    $dbConnString = Resolve-EnvValue "" "DB_CONNECTION_STRING" ""
    if ([string]::IsNullOrWhiteSpace($dbConnString) -or $dbConnString -match "YOUR_SERVER") {
        Write-Host "  WARNING: No database connection configured!" -ForegroundColor Red
        Write-Host "  Re-run with: -DbServer '10.241.24.41,1433' -DbUser 'user' -DbPassword 'pass'" -ForegroundColor Yellow
        $dbConnString = "Server=UNCONFIGURED;Database=${resolvedDbName};TrustServerCertificate=True;"
    } else {
        Write-Host "  DB: Using existing connection string from server" -ForegroundColor Gray
    }
}

$envContent = @"
# Computer Inventory - Server Configuration
# Auto-generated by deploy-server.ps1 on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

# API Configuration
ASPNETCORE_ENVIRONMENT=Production
JWT_SECRET_KEY=$resolvedJwtKey
JWT_ISSUER=$(Resolve-EnvValue '' 'JWT_ISSUER' 'ComputerInventoryApi')
JWT_AUDIENCE=$(Resolve-EnvValue '' 'JWT_AUDIENCE' 'ComputerInventoryClient')
JWT_EXPIRATION_MINUTES=$(Resolve-EnvValue '' 'JWT_EXPIRATION_MINUTES' '60')

# Database Connection
DB_CONNECTION_STRING=$dbConnString

# Agent Configuration
AGENT_API_KEY=$resolvedAgentKey
AGENT_SERVER_URL=$agentServerUrl
AGENT_HEARTBEAT_INTERVAL=$(Resolve-EnvValue '' 'AGENT_HEARTBEAT_INTERVAL' '5')
AGENT_CHECKIN_INTERVAL=$(Resolve-EnvValue '' 'AGENT_CHECKIN_INTERVAL' '15')
AGENT_IDLE_TIMEOUT=$(Resolve-EnvValue '' 'AGENT_IDLE_TIMEOUT' '30')
AGENT_ENABLE_AUTO_CHECKOUT=$(Resolve-EnvValue '' 'AGENT_ENABLE_AUTO_CHECKOUT' 'true')
"@

Set-Content -Path $envFile -Value $envContent -NoNewline

# Patch API key into agent package so auto-updates deliver the correct key
$agentOutputDir = Join-Path $ProjectRoot "src\ComputerInventory.Api\agent"
$agentZipPath = Join-Path $agentOutputDir "ComputerInventoryAgent.zip"
$updateZipPath = Join-Path $agentOutputDir "update\agent-update.zip"

if ((Test-Path $agentZipPath) -and -not [string]::IsNullOrWhiteSpace($resolvedAgentKey)) {
    Write-Host "  Patching API key into agent package..." -ForegroundColor Gray
    $tempPatchDir = Join-Path $env:TEMP "AgentPatch_$(Get-Random)"
    Expand-Archive -Path $agentZipPath -DestinationPath $tempPatchDir -Force

    $patchConfig = Join-Path $tempPatchDir "appsettings.json"
    if (Test-Path $patchConfig) {
        $config = Get-Content $patchConfig | ConvertFrom-Json
        $config.ApiSettings.ApiKey = $resolvedAgentKey
        $config | ConvertTo-Json -Depth 10 | Set-Content $patchConfig

        Remove-Item $agentZipPath -Force
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        [System.IO.Compression.ZipFile]::CreateFromDirectory($tempPatchDir, $agentZipPath)

        # Also update the update package
        if (Test-Path $updateZipPath) {
            Copy-Item $agentZipPath -Destination $updateZipPath -Force
        }

        # Recalculate checksum for update.json
        $updateJsonPath = Join-Path $agentOutputDir "update\update.json"
        if (Test-Path $updateJsonPath) {
            $hash = Get-FileHash -Path $agentZipPath -Algorithm SHA256
            $updateJson = Get-Content $updateJsonPath | ConvertFrom-Json
            $updateJson.Checksum = $hash.Hash.ToLower()
            $updateJson.FileSize = (Get-Item $agentZipPath).Length
            $updateJson | ConvertTo-Json -Depth 10 | Set-Content $updateJsonPath
        }

        Write-Host "  Agent package patched with API key" -ForegroundColor Green
    }

    Remove-Item $tempPatchDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Copy source for Docker build (exclude bin/obj to avoid conflicts inside Docker)
$srcSource = Join-Path $ProjectRoot "src"
$srcDest = Join-Path $deployDir "src"
robocopy $srcSource $srcDest /E /XD bin obj | Out-Null

# Copy migration scripts (needed by Dockerfile for automatic migration at startup)
$migrationsSource = Join-Path $ProjectRoot "server\migrations"
$migrationsDest = Join-Path $deployDir "server\migrations"
if (Test-Path $migrationsSource) {
    robocopy $migrationsSource $migrationsDest /E | Out-Null
}

# Pre-publish Web project locally (Blazor WASM = static files, avoids NuGet issues in Docker)
Write-Host "  Publishing Web project locally..." -ForegroundColor Gray
$webProject = Join-Path $ProjectRoot "src\ComputerInventory.Web\ComputerInventory.Web.csproj"
$webPublishDir = Join-Path $deployDir "publish-web"
dotnet publish $webProject -c Release -o $webPublishDir 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "  Web publish failed, retrying..." -ForegroundColor Yellow
    dotnet publish $webProject -c Release -o $webPublishDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Web project publish failed!" -ForegroundColor Red
        exit 1
    }
}
Write-Host "  Web project published successfully" -ForegroundColor Green

# Copy .dockerignore if it exists
$dockerIgnorePath = Join-Path $ProjectRoot ".dockerignore"
if (Test-Path $dockerIgnorePath) {
    Copy-Item $dockerIgnorePath -Destination $deployDir
}

Write-Host "[4/6] Transferring files to server..." -ForegroundColor Green
# Create directory on remote server and fix permissions from any previous deploy
ssh $SshTarget "mkdir -p $ServerPath; chmod -R u+rwX $ServerPath 2>/dev/null"

# Transfer files using SCP
scp -r "$deployDir\*" "${SshTarget}:${ServerPath}/"

# SCP wildcard doesn't match dotfiles - deploy them explicitly
$envFilePath = Join-Path $deployDir ".env"
if (Test-Path $envFilePath) {
    scp "$envFilePath" "${SshTarget}:${ServerPath}/.env"
    Write-Host "  Deployed .env to server" -ForegroundColor Gray
}
$dockerIgnoreFile = Join-Path $deployDir ".dockerignore"
if (Test-Path $dockerIgnoreFile) {
    scp "$dockerIgnoreFile" "${SshTarget}:${ServerPath}/.dockerignore"
    Write-Host "  Deployed .dockerignore to server" -ForegroundColor Gray
}

Write-Host "[5/6] Setting up on remote server..." -ForegroundColor Green
# Copy setup script and execute
$setupScript = Join-Path $ProjectRoot "server\setup.sh"
scp $setupScript "${SshTarget}:${ServerPath}/setup.sh"

# Execute setup script on remote server
ssh $SshTarget "chmod +x ~/computer-inventory/setup.sh && ~/computer-inventory/setup.sh"

Write-Host "[6/6] Cleaning up local deployment files..." -ForegroundColor Green
Remove-Item $deployDir -Recurse -Force

$ServerIP = $ServerHost

# Record version history (after server is up)
Write-Host ""
Write-Host "Recording version history..." -ForegroundColor Gray
$apiUrl = "http://${ServerIP}:5010/api/version/record"

# Wait for server to be ready
Write-Host "  Waiting for server to be ready..." -ForegroundColor Gray
Start-Sleep -Seconds 10

try {
    # Record ThinClient version
    $thinClientBody = @{
        Type = "ThinClient"
        Version = $newVersion
        ChangeLog = "Auto-deployed version $newVersion"
        Checksum = $checksum
        FileSize = $fileSize
    } | ConvertTo-Json
    
    $headers = @{ "Content-Type" = "application/json" }
    # Try with empty API key (works if ApiKey not configured)
    $result = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $thinClientBody -ContentType "application/json" -Headers @{"X-API-Key"=""} -ErrorAction Stop
    Write-Host "  ThinClient v$newVersion recorded" -ForegroundColor Gray
    
    # Record Server version (same version for simplicity)
    $serverBody = @{
        Type = "Server"
        Version = $newVersion
        ChangeLog = "Auto-deployed version $newVersion"
    } | ConvertTo-Json
    
    $result = Invoke-RestMethod -Uri $apiUrl -Method Post -Body $serverBody -ContentType "application/json" -Headers @{"X-API-Key"=""} -ErrorAction Stop
    Write-Host "  Server v$newVersion recorded" -ForegroundColor Gray
} catch {
    Write-Host "  Note: Version history not recorded (API may require authentication)" -ForegroundColor Yellow
}

# Upload agent package to centralized package registry
Write-Host ""
Write-Host "Uploading agent package to package registry..." -ForegroundColor Gray
$packageApiBase = "http://${ServerIP}:5010"
$agentZipForUpload = Join-Path $ProjectRoot "src\ComputerInventory.Api\agent\ComputerInventoryAgent.zip"

if (Test-Path $agentZipForUpload) {
    try {
        # Login to get auth cookie
        $loginBody = @{ Username = "admin"; Password = "Admin123!" } | ConvertTo-Json
        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $loginResult = Invoke-RestMethod -Uri "$packageApiBase/api/auth/login" -Method Post -Body $loginBody -ContentType "application/json" -WebSession $session -ErrorAction Stop

        if ($loginResult.success) {
            Write-Host "  Authenticated as admin" -ForegroundColor Gray

            # Upload the package via multipart form
            $boundary = [System.Guid]::NewGuid().ToString()
            $fileBytes = [System.IO.File]::ReadAllBytes($agentZipForUpload)
            $fileEnc = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileBytes)

            $bodyLines = @(
                "--$boundary",
                "Content-Disposition: form-data; name=`"package`"; filename=`"agent-update.zip`"",
                "Content-Type: application/zip",
                "",
                $fileEnc,
                "--$boundary",
                "Content-Disposition: form-data; name=`"version`"",
                "",
                $newVersion,
                "--$boundary",
                "Content-Disposition: form-data; name=`"releaseNotes`"",
                "",
                "Auto-deployed version $newVersion",
                "--$boundary",
                "Content-Disposition: form-data; name=`"isRequired`"",
                "",
                "false",
                "--$boundary",
                "Content-Disposition: form-data; name=`"isBreakingChange`"",
                "",
                "false",
                "--$boundary",
                "Content-Disposition: form-data; name=`"channel`"",
                "",
                "stable",
                "--$boundary--"
            ) -join "`r`n"

            $uploadResult = Invoke-RestMethod -Uri "$packageApiBase/api/packages/upload" -Method Post -Body $bodyLines -ContentType "multipart/form-data; boundary=$boundary" -WebSession $session -ErrorAction Stop
            $packageId = $uploadResult.id
            Write-Host "  Package v$newVersion uploaded (ID: $packageId)" -ForegroundColor Green

            # Publish the package
            Invoke-RestMethod -Uri "$packageApiBase/api/packages/$packageId/publish" -Method Post -WebSession $session -ErrorAction Stop
            Write-Host "  Package published" -ForegroundColor Gray

            # Activate the package
            Invoke-RestMethod -Uri "$packageApiBase/api/packages/$packageId/activate" -Method Post -WebSession $session -ErrorAction Stop
            Write-Host "  Package activated" -ForegroundColor Green
        } else {
            Write-Host "  Warning: Login failed - package not registered in registry" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Warning: Package registry upload failed: $_" -ForegroundColor Yellow
        Write-Host "  The legacy update path (agent/update/) is still active" -ForegroundColor Yellow
    }
} else {
    Write-Host "  Warning: Agent zip not found at $agentZipForUpload - skipping package upload" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host " Deployment Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Server URL: http://${ServerIP}:5010" -ForegroundColor Cyan
Write-Host "Web UI: http://${ServerIP}:5011" -ForegroundColor Cyan
Write-Host "Swagger: http://${ServerIP}:5010/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "Default login: admin / Admin123!" -ForegroundColor Yellow
