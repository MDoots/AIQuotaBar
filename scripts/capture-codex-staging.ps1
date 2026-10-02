[CmdletBinding()]
param(
    [ValidateRange(1,10)] [int] $Cycles = 10,
    [ValidateRange(20,120)] [int] $ControlSeconds = 60,
    [string] $Executable,
    [string] $ControlDirectory,
    [switch] $ActualProvider,
    [switch] $ValidateThenFull,
    [switch] $PreflightOnly
)
$ErrorActionPreference = 'Stop'
if ($ValidateThenFull -and $Cycles -ne 1) { throw 'ValidateThenFull requires -Cycles 1.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$tool = Join-Path $repo 'tools/AIQuotaBar.CodexTrace/bin/Release/net10.0-windows/AIQuotaBar.CodexTrace.exe'
if (-not (Test-Path -LiteralPath $tool)) { throw 'Build the CodexTrace tool in Release first.' }
& $tool preflight
if ($LASTEXITCODE -ne 0) { throw 'ETW access blocked. Run this script in an elevated PowerShell. No helper was launched.' }
if ($PreflightOnly) { return }
if (-not $Executable -and $env:AIQUOTABAR_CODEX_PATH -and (Test-Path -LiteralPath $env:AIQUOTABAR_CODEX_PATH)) {
    $Executable = $env:AIQUOTABAR_CODEX_PATH
}
if (-not $Executable) {
    # Same desktop-first ordering as CodexProcessLocator; no personal path is hard-coded.
    $Executable = Get-ChildItem -LiteralPath (Join-Path $env:LOCALAPPDATA 'OpenAI/Codex') -Filter codex.exe -Recurse |
        Sort-Object FullName | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Executable -or -not (Test-Path -LiteralPath $Executable) -or [IO.Path]::GetExtension($Executable) -ne '.exe') {
    throw 'Supply an existing native official Codex executable with -Executable.'
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$out = Join-Path $repo ('artifacts/hotfix-codex-trace/corrective-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$control = Join-Path $out 'control'
$probe = Join-Path $out 'probe'
New-Item -ItemType Directory -Path $control,$probe | Out-Null

# Record only bounded --version output and hash, never raw CLI/config/authentication output.
$versionInfo = [Diagnostics.ProcessStartInfo]::new($Executable)
$versionInfo.Arguments = '--version'
$versionInfo.UseShellExecute = $false
$versionInfo.CreateNoWindow = $true
$versionInfo.RedirectStandardInput = $true
$versionInfo.RedirectStandardOutput = $true
$versionInfo.RedirectStandardError = $true
$versionProcess = [Diagnostics.Process]::Start($versionInfo)
try {
    if (-not $versionProcess.WaitForExit(5000)) { $versionProcess.Kill($true); throw 'Version query timed out.' }
    $version = $versionProcess.StandardOutput.ReadToEnd().Trim()
    if ($versionProcess.ExitCode -ne 0 -or $version -notmatch '^codex-cli [0-9A-Za-z.\-]+$') { throw 'Unexpected version query result.' }
} finally { $versionProcess.Dispose() }
@{ Version=$version; SHA256=(Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash;
    CollectorSHA256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($tool,'.dll')) -Algorithm SHA256).Hash;
    Mode='Experiment only; no production candidate acceptance'; ActualProvider=[bool]$ActualProvider; Cycles=$Cycles; ControlSeconds=$ControlSeconds } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out 'identity.json') -Encoding utf8

# A completed current control can be re-analyzed without repeating the capture.
if ($ControlDirectory) {
    $ControlDirectory=(Resolve-Path -LiteralPath $ControlDirectory).Path
    $artifactRoot=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $ControlDirectory.StartsWith($artifactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Control must be a local repository artifact.' }
    $startData=Get-Content -LiteralPath (Join-Path $ControlDirectory 'capture-start.json') -Raw | ConvertFrom-Json
    $endData=Get-Content -LiteralPath (Join-Path $ControlDirectory 'capture-end.json') -Raw | ConvertFrom-Json
    $controlAge=([DateTimeOffset]::UtcNow - [DateTimeOffset]$endData.EndedUtc).TotalMinutes
    if ($startData.Format -ne 'FilteredRealtimeETW' -or $controlAge -gt 30 -or $controlAge -lt 0) {
        throw 'Reusable control must be the current real-time format and at most 30 minutes old.'
    }
    & $tool analyze $ControlDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Completed control did not validate; no quota helper launched.' }
    Copy-Item -LiteralPath (Join-Path $ControlDirectory 'summary.json') -Destination (Join-Path $control 'summary.json')
    @{ ControlEventSHA256=(Get-FileHash -LiteralPath (Join-Path $ControlDirectory 'events.jsonl')).Hash;
        SourceCapture=(Split-Path (Split-Path $ControlDirectory -Parent) -Leaf) } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $control 'reference.json') -Encoding utf8
} else {
    # Control validates process, descendant and filesystem attribution BEFORE any quota helper.
    & $tool capture $control $ControlSeconds
    if ($LASTEXITCODE -ne 0) { throw 'Control collector self-test failed or capture was inconclusive. No quota helper launched. Review the local summary.' }
}
$duration = [Math]::Min(900, 60 * ($Cycles - 1) + 60)
$psi = [Diagnostics.ProcessStartInfo]::new($tool)
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
foreach ($arg in @('capture',$probe,[string]$duration)) { $psi.ArgumentList.Add($arg) }
$collector = [Diagnostics.Process]::Start($psi)
try {
    $readyWatch = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $probe 'ready'))) {
        if ($collector.HasExited -or $readyWatch.Elapsed.TotalSeconds -ge 30) { throw 'Probe collector failed to become ready.' }
        Start-Sleep -Milliseconds 250
    }
    $probeMode = if ($ActualProvider) { 'provider' } else { 'probe' }
    & $tool $probeMode $probe $Executable $Cycles
    $probeCode = $LASTEXITCODE
    # Allow a short settling period to distinguish cleanup from persistent folders.
    Start-Sleep -Seconds 10
    Set-Content -LiteralPath (Join-Path $probe 'stop') -Value 'Owned collector stop request'
    if (-not $collector.WaitForExit(60000)) { throw 'Collector shutdown timed out; capture is inconclusive.' }
    if ($collector.ExitCode -ne 0 -or $probeCode -ne 0) { throw 'Probe or attribution gate was inconclusive. Review local results; no candidate is accepted.' }
    Write-Output ('Experiment captured: ' + $out)
    Write-Output 'Review control/probe summaries. This is not fresh-app, UI, tray-exit or release acceptance.'
} finally {
    # Cooperative stop only. Never kill unrelated Codex processes or cancel another ETW session.
    if (-not $collector.HasExited) {
        Set-Content -LiteralPath (Join-Path $probe 'stop') -Value 'Owned collector stop request'
        [void]$collector.WaitForExit(10000)
    }
    $collector.Dispose()
}
if ($ValidateThenFull) {
    Write-Output 'Single-probe functionality and exact-helper attribution passed; continuing with ten spaced probes.'
    $reuseControl = if ($ControlDirectory) { $ControlDirectory } else { $control }
    & $PSCommandPath -ControlDirectory $reuseControl -Executable $Executable -Cycles 10 -ActualProvider:$ActualProvider
}
