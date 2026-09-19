<#
.SYNOPSIS
Queries selected official local providers through AIQuotaBar's production adapters.
.DESCRIPTION
Requires a restored .NET 10 developer checkout. No model prompts, interactive
sessions, account changes, raw CLI output or private account data are emitted.
Unavailable/auth/unsupported results are observations, never invented quotas.
#>
[CmdletBinding()]
param(
    [ValidateSet('codex','antigravity','claude-code','grok-build','github-copilot')]
    [string[]]$Provider = @('codex','antigravity','claude-code','grok-build','github-copilot'),
    [ValidateRange(1,30)][int]$TimeoutSeconds = 6,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'tools/AIQuotaBar.ProviderProbe/AIQuotaBar.ProviderProbe.csproj'
$assembly = Join-Path $repo 'tools/AIQuotaBar.ProviderProbe/bin/Release/net10.0/AIQuotaBar.ProviderProbe.dll'
$dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
if (-not $NoBuild) {
    & $dotnet build $project -c Release --no-restore --nologo *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed. Restore/build the solution before running the probe.' }
}
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build the provider probe first.' }
$results = foreach ($id in $Provider) {
    $info = [System.Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $dotnet
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = $repo
    $info.ArgumentList.Add($assembly)
    $info.ArgumentList.Add($id)
    $info.ArgumentList.Add([string]$TimeoutSeconds)
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw 'Probe did not start.' }
        $process.StandardInput.Close()
        $outputBuffer = [char[]]::new(8192)
        $output = $process.StandardOutput.ReadBlockAsync($outputBuffer, 0, $outputBuffer.Length)
        $errors = $process.StandardError.BaseStream.CopyToAsync([System.IO.Stream]::Null)
        # SDK shutdown has its own finite grace/kill budget; supervise the entire
        # owned probe tree as an additional outer deadline.
        if (-not $process.WaitForExit(($TimeoutSeconds + 25) * 1000)) {
            $process.Kill($true)
            [void]$process.WaitForExit(1000)
            [pscustomobject]@{ Provider=$id; Status='Timeout'; QuotaObserved=$false; WindowCount=0; DurationMilliseconds=($TimeoutSeconds+25)*1000 }
        } elseif (-not $output.Wait(1000) -or -not $errors.Wait(1000)) {
            [pscustomobject]@{ Provider=$id; Status='Error'; QuotaObserved=$false; WindowCount=0 }
        } else {
            try {
                $count = $output.GetAwaiter().GetResult()
                if ($count -eq $outputBuffer.Length) { throw 'Probe output exceeded limit.' }
                [string]::new($outputBuffer, 0, $count) | ConvertFrom-Json
            }
            catch { [pscustomobject]@{ Provider=$id; Status='Error'; QuotaObserved=$false; WindowCount=0 } }
        }
    } catch {
        [pscustomobject]@{ Provider=$id; Status='Error'; QuotaObserved=$false; WindowCount=0 }
    } finally {
        try { if (-not $process.HasExited) { $process.Kill($true); [void]$process.WaitForExit(1000) } } catch { }
        $process.Dispose()
    }
}
$results | ConvertTo-Json -Depth 4
if (@($results | Where-Object { $_.Status -in @('Error','Timeout') }).Count -gt 0) { exit 1 }
