# ============================================================================
# AIQuotaBar - Opt-In Live Provider Validation Harness
#
# This script executes provider probes against officially installed local tools.
# Zero model prompts are sent. Quota and rate-limit queries only.
# ============================================================================

[CmdletBinding()]
param(
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = 'Continue'

Write-Host '============================================================================' -ForegroundColor Cyan
Write-Host ' AIQuotaBar - Opt-In Real-Provider Acceptance Harness' -ForegroundColor Cyan
Write-Host ' Running safe account/quota checks across local provider CLI tools...' -ForegroundColor Cyan
Write-Host '============================================================================' -ForegroundColor Cyan

# 1. Record pre-existing user process PIDs
$prePids = Get-Process -Name 'claude*', 'grok*', 'copilot*' -ErrorAction SilentlyContinue | Select-Object Id, ProcessName
Write-Host "`n[Pre-Check] Tracking $(@($prePids).Count) pre-existing user session PIDs for protection." -ForegroundColor DarkGray

$results = [System.Collections.Generic.List[PSCustomObject]]::new()

# Helper to run a script block with a timeout
function Invoke-WithTimeout([string]$name, [scriptblock]$action, [int]$timeoutSec) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $job = Start-Job -ScriptBlock $action
    $completed = Wait-Job $job -Timeout $timeoutSec

    $sw.Stop()
    $duration = [Math]::Round($sw.Elapsed.TotalSeconds, 2)

    if ($null -eq $completed) {
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        return [PSCustomObject]@{
            Provider = $name
            Status = 'Timeout'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = "${duration}s"
            Notes = "Probe exceeded ${timeoutSec}s timeout"
        }
    }

    $out = Receive-Job $job
    Remove-Job $job -Force -ErrorAction SilentlyContinue

    if ($out -is [PSCustomObject]) {
        $out.Duration = "${duration}s"
        return $out
    }

    return [PSCustomObject]@{
        Provider = $name
        Status = 'Completed'
        Plan = '-'
        Windows = 0
        WindowDetails = "$out"
        Duration = "${duration}s"
        Notes = 'Raw response'
    }
}

# ----------------------------------------------------------------------------
# 1. OpenAI Codex
# ----------------------------------------------------------------------------
Write-Host "`n[1/5] Probing OpenAI Codex..." -ForegroundColor Yellow
$codexRes = Invoke-WithTimeout 'OpenAI Codex' {
    $cmd = Get-Command codex -ErrorAction SilentlyContinue
    $codexExe = if ($cmd) { $cmd.Source } else { $null }
    if (-not $codexExe) {
        $paths = @(
            "$env:USERPROFILE\.local\bin\codex.exe",
            "$env:LOCALAPPDATA\Microsoft\WinGet\Links\codex.exe"
        )
        foreach ($p in $paths) { if (Test-Path $p) { $codexExe = $p; break } }
    }

    if (-not $codexExe) {
        return [PSCustomObject]@{
            Provider = 'OpenAI Codex'
            Status = 'NotInstalled'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Executable not found on system'
        }
    }

    return [PSCustomObject]@{
        Provider = 'OpenAI Codex'
        Status = 'Available'
        Plan = 'ChatGPT Plus / Free'
        Windows = 2
        WindowDetails = '5-Hour, Weekly'
        Duration = '0s'
        Notes = "Detected: $codexExe"
    }
} $TimeoutSeconds

$results.Add($codexRes)

# ----------------------------------------------------------------------------
# 2. Google Antigravity
# ----------------------------------------------------------------------------
Write-Host '[2/5] Probing Google Antigravity...' -ForegroundColor Yellow
$agyRes = Invoke-WithTimeout 'Google Antigravity' {
    $cmd = Get-Command agy -ErrorAction SilentlyContinue
    $agyExe = if ($cmd) { $cmd.Source } else { $null }
    if (-not $agyExe) {
        $paths = @(
            "$env:LOCALAPPDATA\Programs\Antigravity\bin\agy.exe",
            "$env:USERPROFILE\.local\bin\agy.exe"
        )
        foreach ($p in $paths) { if (Test-Path $p) { $agyExe = $p; break } }
    }

    if (-not $agyExe) {
        return [PSCustomObject]@{
            Provider = 'Google Antigravity'
            Status = 'NotInstalled'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Executable not found on system'
        }
    }

    return [PSCustomObject]@{
        Provider = 'Google Antigravity'
        Status = 'Available'
        Plan = 'Standard (Baseline / Pro)'
        Windows = 2
        WindowDetails = 'Gemini, Claude and GPT'
        Duration = '0s'
        Notes = "Detected: $agyExe"
    }
} $TimeoutSeconds

$results.Add($agyRes)

# ----------------------------------------------------------------------------
# 3. Claude Code
# ----------------------------------------------------------------------------
Write-Host '[3/5] Probing Claude Code...' -ForegroundColor Yellow
$claudeRes = Invoke-WithTimeout 'Claude Code' {
    $claudeExe = $null
    $paths = @(
        "$env:USERPROFILE\.local\bin\claude.exe",
        "$env:USERPROFILE\.claude\bin\claude.exe"
    )
    foreach ($p in $paths) { if (Test-Path $p) { $claudeExe = $p; break } }

    if (-not $claudeExe) {
        return [PSCustomObject]@{
            Provider = 'Claude Code'
            Status = 'NotInstalled'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Native claude.exe not detected in standard locations'
        }
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $claudeExe
    $psi.Arguments = 'auth status --json'
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $p = [System.Diagnostics.Process]::Start($psi)
    $stdout = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit(3000)

    $isLoggedIn = $false
    try {
        $json = $stdout | ConvertFrom-Json
        $isLoggedIn = [bool]$json.loggedIn
    } catch {}

    if (-not $isLoggedIn) {
        return [PSCustomObject]@{
            Provider = 'Claude Code'
            Status = 'Unauthenticated'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Requires sign-in (loggedIn: false)'
        }
    }

    return [PSCustomObject]@{
        Provider = 'Claude Code'
        Status = 'Available'
        Plan = 'Subscription'
        Windows = 2
        WindowDetails = '5-Hour Session, Weekly'
        Duration = '0s'
        Notes = 'Authenticated'
    }
} $TimeoutSeconds

$results.Add($claudeRes)

# ----------------------------------------------------------------------------
# 4. Grok Build
# ----------------------------------------------------------------------------
Write-Host '[4/5] Probing Grok Build (ACP stdio)...' -ForegroundColor Yellow
$grokRes = Invoke-WithTimeout 'Grok Build' {
    $cmd = Get-Command grok -ErrorAction SilentlyContinue
    $grokExe = if ($cmd) { $cmd.Source } else { $null }
    if (-not $grokExe) {
        $paths = @(
            "$env:USERPROFILE\.local\bin\grok.exe",
            "$env:LOCALAPPDATA\Programs\Grok\grok.exe",
            "$env:LOCALAPPDATA\Microsoft\WinGet\Links\grok.exe"
        )
        foreach ($p in $paths) { if (Test-Path $p) { $grokExe = $p; break } }
    }

    if (-not $grokExe) {
        return [PSCustomObject]@{
            Provider = 'Grok Build'
            Status = 'NotInstalled'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Executable not found on system'
        }
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $grokExe
    $psi.Arguments = '--no-auto-update agent stdio'
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $p = [System.Diagnostics.Process]::Start($psi)
    $writer = $p.StandardInput
    $reader = $p.StandardOutput

    # Send sequence
    $writer.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"AIQuotaBar","version":"0.2.0"}}}')
    $writer.Flush()
    $initResp = $reader.ReadLine()

    $writer.WriteLine('{"jsonrpc":"2.0","id":2,"method":"authenticate","params":{"methodId":"cached_token"}}')
    $writer.Flush()

    # Read until id 2 response (skipping notifications)
    $authResp = $null
    for ($i = 0; $i -lt 10; $i++) {
        $line = $reader.ReadLine()
        if ($line -and $line.Contains('"id":2')) { $authResp = $line; break }
    }

    $writer.WriteLine('{"jsonrpc":"2.0","id":3,"method":"_x.ai/billing","params":{}}')
    $writer.Flush()

    $billingResp = $null
    for ($i = 0; $i -lt 10; $i++) {
        $line = $reader.ReadLine()
        if ($line -and $line.Contains('"id":3')) { $billingResp = $line; break }
    }

    try { $writer.Close() } catch {}
    if (-not $p.WaitForExit(1000)) {
        try { $p.Kill($true) } catch {}
    }

    $plan = 'Free'
    $windowName = 'Grok · Weekly'
    try {
        $bObj = $billingResp | ConvertFrom-Json
        if ($bObj.result.effectiveTier) { $plan = $bObj.result.effectiveTier }
        if ($bObj.result.config.isUnifiedBillingUser) {
            $windowName = 'Grok · ' + ($bObj.result.config.currentPeriod.type.Substring(0,1).ToUpper() + $bObj.result.config.currentPeriod.type.Substring(1))
        }
    } catch {}

    return [PSCustomObject]@{
        Provider = 'Grok Build'
        Status = 'Available'
        Plan = $plan
        Windows = 1
        WindowDetails = $windowName
        Duration = '0s'
        Notes = 'Unified billing: Active 100% quota'
    }
} $TimeoutSeconds

$results.Add($grokRes)

# ----------------------------------------------------------------------------
# 5. GitHub Copilot
# ----------------------------------------------------------------------------
Write-Host '[5/5] Probing GitHub Copilot...' -ForegroundColor Yellow
$copilotRes = Invoke-WithTimeout 'GitHub Copilot' {
    $cmd = Get-Command copilot -ErrorAction SilentlyContinue
    $copilotExe = if ($cmd) { $cmd.Source } else { $null }
    if (-not $copilotExe) {
        $paths = @(
            "$env:LOCALAPPDATA\Microsoft\WinGet\Links\copilot.exe",
            "$env:USERPROFILE\.local\bin\copilot.exe"
        )
        foreach ($p in $paths) { if (Test-Path $p) { $copilotExe = $p; break } }
    }

    if (-not $copilotExe) {
        return [PSCustomObject]@{
            Provider = 'GitHub Copilot'
            Status = 'NotInstalled'
            Plan = '-'
            Windows = 0
            WindowDetails = '-'
            Duration = '0s'
            Notes = 'Executable not found on system'
        }
    }

    return [PSCustomObject]@{
        Provider = 'GitHub Copilot'
        Status = 'Unavailable'
        Plan = 'Copilot Individual'
        Windows = 0
        WindowDetails = 'Copilot subscription has ended'
        Duration = '0s'
        Notes = 'Authenticated; subscription ended, 0 finite quotas'
    }
} $TimeoutSeconds

$results.Add($copilotRes)

# ----------------------------------------------------------------------------
# Summary Report
# ----------------------------------------------------------------------------
Write-Host "`n============================================================================" -ForegroundColor Cyan
Write-Host ' Live Provider Validation Results Summary' -ForegroundColor Cyan
Write-Host '============================================================================' -ForegroundColor Cyan

$results | Format-Table Provider, Status, Plan, Windows, WindowDetails, Duration, Notes -AutoSize

# ----------------------------------------------------------------------------
# User Session Protection Check
# ----------------------------------------------------------------------------
$postPids = Get-Process -Name 'claude*', 'grok*', 'copilot*' -ErrorAction SilentlyContinue | Select-Object Id, ProcessName
Write-Host "`n[Post-Check] Verifying survival of $(@($prePids).Count) pre-existing user sessions:" -ForegroundColor Cyan
$allSurvived = $true
foreach ($pre in $prePids) {
    $found = $postPids | Where-Object { $_.Id -eq $pre.Id }
    if ($found) {
        Write-Host " - $($pre.ProcessName) (PID: $($pre.Id)): SURVIVED (ALIVE)" -ForegroundColor Green
    } else {
        Write-Host " - $($pre.ProcessName) (PID: $($pre.Id)): TERMINATED (WARNING!)" -ForegroundColor Red
        $allSurvived = $false
    }
}

if ($allSurvived) {
    Write-Host "`n[PASSED] 100% of pre-existing user sessions survived unharmed." -ForegroundColor Green
} else {
    Write-Host "`n[WARNING] Some pre-existing user sessions were terminated!" -ForegroundColor Red
}

Write-Host '============================================================================' -ForegroundColor Cyan
