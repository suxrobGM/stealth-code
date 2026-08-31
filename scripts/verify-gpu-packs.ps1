# Checks GPU pack hashes against NuGet without downloading packages.
#
# Run after changing <WhisperVersion> to check the pinned values.

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Split-Path -Parent $PSScriptRoot
$catalog = Get-Content (Join-Path $root "src\StealthCode.Audio\Services\GpuPackCatalog.cs") -Raw
$csproj = Get-Content (Join-Path $root "src\StealthCode.Audio\StealthCode.Audio.csproj") -Raw

if ($csproj -notmatch '<WhisperVersion>([^<]+)</WhisperVersion>') { Write-Error "No <WhisperVersion> in the csproj" }
$buildVersion = $Matches[1]

if ($catalog -notmatch 'WhisperVersion\s*=\s*"([^"]+)"') { Write-Error "No WhisperVersion in GpuPackCatalog.cs" }
$catalogVersion = $Matches[1]

$failures = 0

if ($buildVersion -ne $catalogVersion) {
    Write-Host "Version mismatch: csproj $buildVersion, GpuPackCatalog $catalogVersion" -ForegroundColor Red
    $failures++
}

Write-Host "Whisper $buildVersion" -ForegroundColor Cyan

# The package ID is followed by its pinned hash. One RuntimeLibrary per pack keeps the count honest,
# so reformatting the catalog cannot silently drop a pack from this check.
$expected = [regex]::Matches($catalog, 'RuntimeLibrary\.\w+').Count
$packs = [regex]::Matches($catalog, '"(whisper\.net\.runtime[^"]*)",\s*\r?\n\s*"([^"]+)"')
if ($packs.Count -ne $expected) {
    Write-Error "Found $($packs.Count) of $expected packs in GpuPackCatalog.cs. Has the record shape changed?"
}

foreach ($pack in $packs) {
    $id = $pack.Groups[1].Value
    $pinned = $pack.Groups[2].Value

    try {
        $leaf = Invoke-RestMethod "https://api.nuget.org/v3/registration5-semver1/$id/$buildVersion.json" -UseBasicParsing
        $published = (Invoke-RestMethod $leaf.catalogEntry -UseBasicParsing).packageHash
    }
    catch {
        Write-Host "  $id -- not published at $buildVersion" -ForegroundColor Red
        $failures++
        continue
    }

    if ($published -ceq $pinned) {
        Write-Host "  $id OK" -ForegroundColor Green
    }
    else {
        Write-Host "  $id MISMATCH" -ForegroundColor Red
        Write-Host "    pinned:    $pinned"
        Write-Host "    published: $published"
        $failures++
    }
}

if ($failures -gt 0) {
    Write-Host "$failures problem(s)." -ForegroundColor Red
    exit 1
}

Write-Host "GPU packs match NuGet." -ForegroundColor Green
exit 0
