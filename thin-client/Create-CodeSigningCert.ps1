# ======================================
# Create Code Signing Certificate
# Run this ONCE on your development machine
# ======================================

param(
    [string]$CertName = "MKS Computer Inventory Code Signing",
    [string]$OutputPath = "$PSScriptRoot\certs",
    [string]$PfxPassword = $env:PFX_PASSWORD
)

if (-not $PfxPassword) {
    throw "PFX_PASSWORD environment variable is not set. Set it or pass -PfxPassword parameter."
}

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Code Signing Certificate Generator" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created directory: $OutputPath" -ForegroundColor Green
}

# Check if certificate files already exist (resilient to rebuilds)
$cerPath = Join-Path $OutputPath "MKSCodeSign.cer"
$pfxPath = Join-Path $OutputPath "MKSCodeSign.pfx"

if ((Test-Path $cerPath) -and (Test-Path $pfxPath)) {
    Write-Host "Certificate files already exist. Reusing existing certificate." -ForegroundColor Green
    
    # Load existing cert to display info
    $securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
    $cert = Get-PfxCertificate -FilePath $pfxPath -Password $securePassword
    
    Write-Host ""
    Write-Host "Existing Certificate Details:" -ForegroundColor White
    Write-Host "  Subject:    $($cert.Subject)" -ForegroundColor Gray
    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray
    Write-Host "  Expires:    $($cert.NotAfter)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To regenerate, delete the files in $OutputPath and run again." -ForegroundColor Yellow
    exit 0
}

# Check if certificate exists in store
$existingCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=$CertName" }
if ($existingCert) {
    Write-Host "Found certificate in store: $($existingCert.Thumbprint)" -ForegroundColor Yellow
    $cert = $existingCert
} else {
    Write-Host "Creating new code signing certificate..." -ForegroundColor Yellow
    
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
    
    Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green
}

# Export public certificate (for deployment to clients)
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
Write-Host "Exported public certificate: $cerPath" -ForegroundColor Green

# Export PFX with private key (for signing - PROTECT THIS!)
$securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
Write-Host "Exported PFX (with private key): $pfxPath" -ForegroundColor Green

# Save thumbprint for build scripts
$thumbprintPath = Join-Path $OutputPath "thumbprint.txt"
$cert.Thumbprint | Out-File -FilePath $thumbprintPath -NoNewline
Write-Host "Saved thumbprint: $thumbprintPath" -ForegroundColor Green

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host " Certificate Setup Complete!" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Certificate Details:" -ForegroundColor White
Write-Host "  Subject:    CN=$CertName" -ForegroundColor Gray
Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor Gray
Write-Host "  Expires:    $($cert.NotAfter)" -ForegroundColor Gray
Write-Host ""
Write-Host "Files Created:" -ForegroundColor White
Write-Host "  $cerPath (deploy to clients)" -ForegroundColor Gray
Write-Host "  $pfxPath (for signing - KEEP SECURE!)" -ForegroundColor Gray
Write-Host "  $thumbprintPath (thumbprint reference)" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Run build-package.ps1 to build and sign the agent" -ForegroundColor Gray
Write-Host "  2. The installer will automatically deploy the certificate to clients" -ForegroundColor Gray
Write-Host ""
Write-Host "PFX Password: $PfxPassword" -ForegroundColor Magenta
Write-Host "(Store this securely!)" -ForegroundColor Magenta
