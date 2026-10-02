[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $TraceDirectory,
    [ValidateRange(20,900)] [int] $Seconds = 720
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$trace = [IO.Path]::GetFullPath($TraceDirectory)
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $trace.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Trace must be beneath repository artifacts.' }
$tool = Join-Path $repo 'tools/AIQuotaBar.CodexTrace/bin/Release/net10.0-windows/AIQuotaBar.CodexTrace.exe'
& $tool preflight
if ($LASTEXITCODE -ne 0) { throw 'Kernel tracing requires Windows elevation.' }
& $tool capture $trace $Seconds
$collectorCode = $LASTEXITCODE
@{ CompletedUtc=[DateTime]::UtcNow.ToString('o'); CollectorExitCode=$collectorCode;
   CandidateAccepted=$false; Mode='Fresh-widget capture; inspect UI and sanitized attribution separately' } |
   ConvertTo-Json | Set-Content -LiteralPath (Join-Path $trace 'collector-result.json') -Encoding utf8
exit $collectorCode
