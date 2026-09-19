<#
.SYNOPSIS
    Builds and packages AIQuotaBar as a self-contained portable Windows x64 application.

.DESCRIPTION
    Compiles AIQuotaBar.App in Release configuration for win-x64 as a single-file,
    self-contained executable with trimming disabled. Outputs to artifacts/portable/win-x64/.

.PARAMETER Configuration
    Build configuration (default: Release).

.PARAMETER Runtime
    Target runtime identifier (default: win-x64).

.PARAMETER Clean
    Whether to clean stale artifacts before building (default: $true).

.PARAMETER NoLogo
    Whether to suppress standard banner.

.PARAMETER OutputDirectory
    Optional isolated output directory beneath the repository artifacts directory.

.PARAMETER NoRestore
    Use previously restored dependencies, for example after an offline restore.
#>

[CmdletBinding()]
param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$Clean = $true,
    [switch]$NoLogo,
    [string]$OutputDirectory,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path "$scriptDir\..").Path
$projectPath = Join-Path $repoRoot "src\AIQuotaBar.App\AIQuotaBar.App.csproj"
$outputDir = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $repoRoot "artifacts\portable\$Runtime"
} elseif ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}
$outputDir = [IO.Path]::GetFullPath($outputDir)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $outputDir.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Portable output must be a subdirectory of the repository artifacts directory.'
}

if (-not $NoLogo) {
    Write-Host "========================================================" -ForegroundColor Cyan
    Write-Host " AIQuotaBar - Portable Build Pipeline [$Runtime]" -ForegroundColor Cyan
    Write-Host "========================================================" -ForegroundColor Cyan
}

if (-not (Test-Path $projectPath)) {
    throw "Project file not found at: $projectPath"
}

# 1. Clean stale artifacts if requested
if ($Clean -and (Test-Path $outputDir)) {
    Write-Host "Cleaning output directory: $outputDir" -ForegroundColor Yellow
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}

# 2. Ensure output directory exists
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Write-Host "Publishing AIQuotaBar [$Configuration, $Runtime, Self-Contained, Single-File]..." -ForegroundColor Green

# 3. Execute dotnet publish
[string[]]$restoreArgs = @()
if ($NoRestore) { $restoreArgs += '--no-restore' }
& dotnet publish $projectPath `
    @restoreArgs `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    "-p:PublishSingleFile=true" `
    "-p:PublishTrimmed=false" `
    "-p:IncludeNativeLibrariesForSelfExtract=true" `
    "-p:IncludeAllContentForSelfExtract=true" `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# 4. Verify resulting artifact
$exePath = Join-Path $outputDir "AIQuotaBar.exe"
if (-not (Test-Path $exePath)) {
    throw "Expected executable not found at: $exePath"
}

$exeItem = Get-Item $exePath
$sizeMb = [Math]::Round($exeItem.Length / 1MB, 2)
$allFiles = Get-ChildItem -Path $outputDir -File

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host " Build Succeeded!" -ForegroundColor Green
Write-Host (" Output Executable: " + $exePath) -ForegroundColor White
Write-Host (" File Size:         " + $exeItem.Length + " bytes (" + $sizeMb + " MB)") -ForegroundColor White
Write-Host (" Total Files:       " + $allFiles.Count) -ForegroundColor White
Write-Host "========================================================" -ForegroundColor Green

foreach ($file in $allFiles) {
    $fileMb = [Math]::Round($file.Length / 1MB, 2)
    Write-Host (" - " + $file.Name + " (" + $file.Length + " bytes, " + $fileMb + " MB)") -ForegroundColor Gray
}

Write-Host ""
