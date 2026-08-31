# Build and publish script
param(
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $false)]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

# Paths
$OutputDir = "..\publish"
$MainProjectPath = "..\src\StealthCode\StealthCode.csproj"
$LauncherProjectPath = "..\src\StealthCode.Launcher\StealthCode.Launcher.csproj"
$EmbeddedDir = "..\src\StealthCode.Launcher\Embedded"
$TempOutput = "..\publish\temp"
$FinalOutput = Join-Path $OutputDir $Runtime

# Step 0: Verify GPU pack versions and hashes.
Write-Host "Step 0: Verifying GPU pack constants..." -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "verify-gpu-packs.ps1")
if ($LASTEXITCODE -ne 0) {
    Write-Error "GPU pack constants are out of date"
    exit 1
}

# Clean output directories
Write-Host "Cleaning output directories..." -ForegroundColor Cyan
if (Test-Path $FinalOutput) {
    Remove-Item $FinalOutput -Recurse -Force
}
if (Test-Path $TempOutput) {
    Remove-Item $TempOutput -Recurse -Force
}
if (Test-Path $EmbeddedDir) {
    Remove-Item $EmbeddedDir -Recurse -Force
}

New-Item -ItemType Directory -Path $FinalOutput -Force | Out-Null
New-Item -ItemType Directory -Path $TempOutput -Force | Out-Null
New-Item -ItemType Directory -Path $EmbeddedDir -Force | Out-Null

# Step 1: Publish the main app
Write-Host ""
Write-Host "Step 1: Publishing main app ($Configuration for $Runtime)..." -ForegroundColor Yellow

dotnet publish $MainProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $TempOutput

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish main app"
    exit 1
}

# Step 2: Copy files to Launcher's Embedded folder
Write-Host ""
Write-Host "Step 2: Copying files to Launcher's Embedded folder..." -ForegroundColor Yellow

Copy-Item -Path "$TempOutput\*" -Destination $EmbeddedDir -Recurse -Force

# Remove debug files (.pdb) from Embedded folder
Get-ChildItem -Path $EmbeddedDir -Filter "*.pdb" -Recurse | Remove-Item -Force

# GZip compress all embedded files to reduce launcher size
Add-Type -AssemblyName System.IO.Compression
Write-Host "Compressing embedded files with GZip..." -ForegroundColor Cyan
$originalSize = 0
$compressedSize = 0

Get-ChildItem -Path $EmbeddedDir -File -Recurse | ForEach-Object {
    $originalSize += $_.Length
    $gzPath = $_.FullName + ".gz"
    $inputStream = [System.IO.File]::OpenRead($_.FullName)
    $outputStream = [System.IO.File]::Create($gzPath)
    $gzip = New-Object System.IO.Compression.GZipStream($outputStream, [System.IO.Compression.CompressionLevel]::Optimal)
    $inputStream.CopyTo($gzip)
    $gzip.Dispose()
    $outputStream.Dispose()
    $inputStream.Dispose()
    $compressedSize += (Get-Item $gzPath).Length
    Remove-Item $_.FullName -Force
}

$savedMB = [math]::Round(($originalSize - $compressedSize) / 1MB, 1)
Write-Host "Compressed embedded files: $([math]::Round($originalSize / 1MB, 1)) MB -> $([math]::Round($compressedSize / 1MB, 1)) MB (saved $savedMB MB)" -ForegroundColor Green

# Step 3: Publish the Launcher app
Write-Host ""
Write-Host "Step 3: Publishing Launcher app ($Configuration for $Runtime)..." -ForegroundColor Yellow

dotnet publish $LauncherProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $FinalOutput

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish Launcher app"
    exit 1
}

# Step 4: Sign the executable
Write-Host ""
Write-Host "Step 4: Signing the executable..." -ForegroundColor Yellow

# Clean up temporary directory
if (Test-Path $TempOutput) {
    Remove-Item $TempOutput -Recurse -Force
    Write-Host "Cleaned up temporary files." -ForegroundColor Gray
}

$FinalExe = Join-Path $FinalOutput "stealthcode.exe"
$FinalSizeMB = [math]::Round((Get-Item $FinalExe).Length / 1MB, 1)

Write-Host ""
Write-Host "Done: $FinalExe ($FinalSizeMB MB)" -ForegroundColor Green
