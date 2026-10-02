# Bounded Codex staging diagnostic

Developer-only Windows ETW tool. No production project references this tool or its Microsoft TraceEvent dependency. It does not change Codex installation, global configuration, plugins, credentials or scheduled tasks. It never saves RPC/account/config response bodies. The experimental probe uses the production transport and normalizer with an explicit `--disable plugins app-server` argument string. `-ActualProvider` instead exercises the restored production provider itself with its default six-second budget and exact handshake. It is evidence gathering, not an accepted hotfix or portable app.

Build from the repository root:

```powershell
dotnet restore tools/AIQuotaBar.CodexTrace/AIQuotaBar.CodexTrace.csproj -p:NuGetAudit=false
dotnet build tools/AIQuotaBar.CodexTrace/AIQuotaBar.CodexTrace.csproj -c Release --no-restore -p:NuGetAudit=false
```

Run from an **elevated PowerShell under the same signed-in Windows user**, with the normal Codex home and configured marketplaces:

```powershell
Set-Location C:\Projects\AIQuotaBar
pwsh -NoProfile -File scripts/capture-codex-staging.ps1
```

The default honors an existing `AIQUOTABAR_CODEX_PATH` override, then selects the first desktop native executable in the same ordering as the current locator. If neither is available, supply `-Executable` to explicitly test an already-installed official native binary. No binary is installed or replaced. The script records the version and SHA-256. An elevated shell is needed for Windows kernel ETW, not provider-owned sign-in. Do not run under a different administrator account.

Use `-ControlSeconds 20 -Cycles 1` for a short collector/probe validation. A completed real-time control at most thirty minutes old can be re-analyzed with `-ControlDirectory <existing-control-directory>`; the new run records the original control event hash. This avoids repeating a validated control, while every probe capture still emits its own runtime self-test. Preserve each run separately.

First, a 60-second control capture runs a known writer and its short-lived child, checking create/open, write, deletion and ancestry. If collector validation fails, **no quota helper is launched**. Then a second capture runs up to ten sequential quota reads at 60-second intervals. Each helper queries official `config/read` and requires the returned `config.features.plugins` field to be explicitly false before `account/read` and `account/rateLimits/read`. The profile gate also requires an authenticated ChatGPT account and at least one configured marketplace in the official config response; only the count is saved. Missing/unsupported fields are inconclusive, with no retry or unsafe fallback. The request budget is ten seconds. The unchanged production runner closes stdin, waits up to 500 ms within the request budget, then cleans up only its owned process tree. Shutdown suitability still needs review in the trace and subsequent app acceptance.

Each capture uses a unique session name, 64 MB ETW buffers and at most 900 seconds. The real-time consumer saves only process lifetimes, staging/self-test file events and metadata for pathless IO. Filtered output stops at 240 MB or one million rows; reaching a bound invalidates evidence. A cooperative stop marker stops only the owned session. This creates no persistent monitor. Unexpected hard termination of the collector is inconclusive; its recorded session name can be stopped explicitly by an administrator, never by cancelling all WPR/ETW sessions.

The first file-based control's raw `trace.etl` and indexed `trace.etlx` are preserved locally under Git-ignored `artifacts/hotfix-codex-trace/corrective-*`. They contain system process/file metadata and may include personal paths and command lines; do not upload them or feed them to a model. New captures use filtered `events.jsonl`, omitting unrelated paths and all command lines. No trace contains file contents. Read only `summary.json` and sanitized `probe-*.json` for review; summarize process identities and relevant staging events before sharing. The summary deliberately omits command lines, executable paths, identity and configuration bodies.

Analysis tracks PID lifetimes and parentage, maps file IO to issuing processes through kernel thread events, and separately reports global and exact-probe-tree staging activity. It records new folders still present after a settling period and folders observed then removed. Create/open events include their creation disposition; an open is not automatically a newly created directory. A folder remaining at the end is **persistent at that point**, not proof of permanent abandonment. No shared staging directory is deleted or recursively traversed.

`CollectorValidated` requires the actual writer/descendant self-test, zero lost events and a complete unbounded capture. `AttributionConclusive` describes global coverage and remains false for global pathless IO. `HelperAttributionConclusive` separately requires the exact probe root, no unresolved owned IO, no file events with missing writer lifetimes, complete attribution of observed staging events and no new folder without a writer. Thus unresolved IO by an identified unrelated process is reported as global uncertainty; it is not falsely assigned to the helper. No lost event or missing writer is excused. Other Codex activity may stay open during this coexistence experiment. A desktop-closed run requires a later user-controlled session.

Run `pwsh -NoProfile -File tools/AIQuotaBar.CodexTrace/test-analysis.ps1` for offline synthetic checks of attribution and rejection gates. These do not replace the runtime kernel self-test.

Kernel keywords include Process, Thread, FileIO, FileIOInit and DiskFileIO (filename-to-object mapping). An unresolved write retains only its process, timestamp, operation and kernel object/key identifiers. The tool rejects unresolved owned writes; it does not presume they are pipes or unrelated logs. Probe failures persist a sanitized category and last RPC/shutdown stage, never raw exception or response text.

Name correlation retains key lifetimes and only the per-open object snapshots needed by unresolved IO; the in-memory object table is capped at 250,000 entries. This avoids logging every unrelated open/close. Unrelated names are recorded only as `OutsideStaging`. Ordinary mappings require a preceding name and honor close boundaries. Microsoft TraceEvent's end-rundown semantics supply the initial lifetime of an otherwise unmapped key: a consistent rundown-only key, or its first named closure before subsequent reuse. Ordinary future creation, conflicting mappings, unknown names, closed gaps, missing writers and lost events remain rejection cases. Twenty-two offline cases cover these rules. Re-analysis retains prior summary snapshots and records the analysis collector hash separately from capture identity.

`scripts/capture-codex-widget.ps1 -TraceDirectory <new-artifact-directory>` captures a fresh widget without launching it elevated. Start this collector through UAC, then launch the ordinary app separately and save its actual PID/start time and binary hashes as `probe-root.json`. The same lifetime-based analysis applies to its exact tree. The bounded collector writes `collector-result.json`; it never declares candidate acceptance. UI display, manual refresh and tray exit require independent observations. Record manual findings as manual evidence, not automated assertions.

`-ActualProvider` saves `provider-*.json` with the actual normalized status, sanitized status message, completed official RPC method names, last request stage, marketplace count, fresh-read flag and finite quota values/reset times. An observer delegates to the unchanged production runner; it never retains account identities, configuration bodies or stderr. All requested cycles must return Available from a completed quota RPC with configured marketplaces for this gate to pass. Helper process exit status alone does not prove the snapshot outcome: forced cleanup may follow a completed read.

Passing either experiment does not alone satisfy application acceptance. Record successful provider-path tests, a fresh widget displaying quota, ten automatic refreshes, manual refresh concurrency checks, visible-console observation, graceful tray exit/restart and no owned helpers after exit, normal plugin use, timeout/cancellation/crash and account-transition coverage, and identity/hash of the actual portable candidate separately. Build no Store package while these gates remain outstanding.
