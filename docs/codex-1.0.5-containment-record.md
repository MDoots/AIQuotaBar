# 1.0.5 Codex marketplace hotfix candidate

> **Corrective status — 2 October 2026: CONTAINMENT ONLY, NOT ACCEPTED.**
> The paused candidate below does not satisfy the feature-preserving repair: it performs no live Codex quota reads, and graceful tray exit was not tested. Preserve its bytes and historical evidence, but do not submit it. The single staging folder observed in the earlier functional experiment remains unattributed; it proves neither helper causation nor helper safety. See [the corrective investigation](codex-staging-corrective-investigation.md) for the current tracing prerequisite and next experiment. The user reports that the separate scheduled-task popup is now fixed; this has not been independently verified and no scheduled-task changes are part of this investigation.

Prepared on 23 September 2026 on `codex/hotfix-codex-marketplace-staging` from source commit `82f019151b5e39fb9c9337517b4d9bf150b80509` with the changes for this candidate uncommitted. This is a local test candidate. It has not been installed over the user's app, submitted to Microsoft, published, or released. The user reports that the existing Store app passed certification; the exact approved build was not established from the older repository notes.

## Baseline and diagnosis

- At inspection, the running AIQuotaBar process was the portable `artifacts/releases/v1.0.4-verified/app/AIQuotaBar.exe`, file version `1.0.4.0`, SHA-256 `229F85E061E9320470398D6CD87FA3455B8F7324EBE699B39A06535C7B35BFA8`. The repository started at app/package version `1.0.4.0`. `Get-AppxPackage -Name '*AIQuotaBar*'` returned no package for this Windows profile, so no installed Store upgrade could be tested here.
- The actual Codex executable selected by `CodexProcessLocator` was `%LOCALAPPDATA%\OpenAI\Codex\bin\d375f7df50d3b421\codex.exe`, version `codex-cli 0.155.0-alpha.16`, SHA-256 `97D4D67419D0AC2F71342F9A5E850F9468AA622618DE8EA823223EDB9A91926A`. The AIQuotaBar override was unset. The locator checks this desktop location before `PATH`; discovery only resolves a file path. The bounded probe used this exact executable under the signed-in Windows profile and existing configured Codex home; it did not redirect home or alter global Codex settings.
- The repository's pre-hotfix `CodexUsageProvider` started `codex app-server` for each quota read with a six-second default timeout. `StandardCodexProcessRunner` closed standard input, waited up to 500 ms, then killed its owned process tree; `ProcessIo` also killed the owned tree on timeout or cancellation. The app's timer requests refreshes every minute, with manual refresh and discovery as additional paths. The running 1.0.4 portable binary carried a different source commit, so this source inspection does not by itself establish its exact internal path. The launch and termination pattern is a plausible way to interrupt Codex marketplace maintenance. The exact OS-level parent of a stranded staging directory was not captured, so that mechanism is not proven on this laptop.
- A single bounded 72-second run of the unpatched portable app did not add a staging directory (652 before and after). Its ancestry collection failed due a PowerShell script error, so it is not a complete controlled reproduction. The user's earlier repeated stop/restart observations remain the strongest evidence linking AIQuotaBar activity to the growth.
- Version-matched Codex [app-server startup source](https://github.com/openai/codex/blob/rust-v0.155.0-alpha.16/codex-rs/app-server/src/message_processor.rs#L514), [plugin manager source](https://github.com/openai/codex/blob/rust-v0.155.0-alpha.16/codex-rs/core-plugins/src/manager.rs#L2644), and [CLI flag routing](https://github.com/openai/codex/blob/rust-v0.155.0-alpha.16/codex-rs/cli/src/main.rs#L1113) show plugin startup tasks on app-server start and a marketplace upgrade task gated by the `plugins` feature. The official `--disable plugins` process flag was accepted and ten one-minute-spaced signed-in probes all returned `Available` with one finite quota window. Nevertheless, staging directories rose from 652 to 653 during probe four. The new folder was created at about 07:01:07 UTC while the probe was active and occupied about 25 MB. Direct probe children had no visible main window and no owned helper remained afterward, but the monitor did not capture every possible descendant or unrelated Codex operation. This is insufficient evidence that process-local plugin disabling prevents staging growth in this profile. Logs: `artifacts/hotfix-codex-trace/live-check.json` and `cycle-*.json`.
- The separate console-like popup that continues when AIQuotaBar is closed remains outside this hotfix. There is no evidence that this candidate addresses it.

## Implemented safeguard

`CodexUsageProvider.GetUsageAsync` now returns a clear paused status without starting any Codex process. Both automatic and manual refresh therefore stop Codex quota polling. A cancelled refresh remains cancelled. The app retains previously observed Codex windows within the running session as stale, with a paused label and a tooltip saying that the current account and allowance could not be verified. Paused stale values do not drive the tray's lowest-quota health. A fresh app process has no saved Codex quota to display. Account changes cannot be detected while polling is paused; the stale values must not be treated as current account data. The other providers retain their existing behavior.

No Codex version is enabled for quota polling in 1.0.5. The signed-in live experiment used `0.155.0-alpha.16`; its process-local configuration was not accepted as safe. Other Codex versions were not live-tested for this issue.

The earlier process-local `--disable plugins` experiment and both preliminary candidates are superseded and marked `DO-NOT-SHIP` in `artifacts/candidates/2026-09-23-hotfix` and `artifacts/candidates/2026-09-23-hotfix-paused`. Their live logs are diagnosis evidence only, not validation of these final bytes.

## Final candidate identity

- App/file version `1.0.5.0`; x64 package version `1.0.5.0`.
- The executable's `ProductVersion` embeds the base commit (`1.0.5+82f019...`); it does not identify the uncommitted hotfix edits. Use the SHA-256 hashes below to identify the candidate bytes.
- Store package identity `AGIFutu.AIQuotaBar`; publisher `CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24`, unchanged from 1.0.4.
- Portable executable: `artifacts/candidates/2026-09-23-hotfix-final/portable/AIQuotaBar.exe`, 213,092,588 bytes, SHA-256 `4A58D83AFEB0C08CA2E12069D035BBF4E2DADB8D92884CE5DF517B113DDC0919`.
- Store upload: `artifacts/candidates/2026-09-23-hotfix-final/store/AIQuotaBar.Package_1.0.5.0_x64.msixupload`, 86,493,777 bytes, SHA-256 `5E5BFB177812E2587500A4360C91818A23DFF5C765C8ACD4273CA91BD8DB8347`.
- Nested unsigned MSIX in upload: SHA-256 `3EA4FF668699021B074BF01CDD1D551A78875B68661CD6A5E5F02A9848B0CFF2`.
- Standalone unsigned test MSIX: `artifacts/candidates/2026-09-23-hotfix-final/store/AIQuotaBar.Package_1.0.5.0_x64_Test/AIQuotaBar.Package_1.0.5.0_x64.msix`, 86,355,448 bytes, SHA-256 `B7D62005167B5769FBA010504855F21DB84033B7423D12EEDDFAB9B159AD1515`.

The existing portable script and WAP Store-upload build produced these artifacts in a separate candidate directory; existing release artifacts were preserved. `scripts/validate-store-upload.ps1` returned `Valid` for the upload's identity, publisher, app version, and package version. Packaging is not Store certification.

## Verification

- Before edits, the offline baseline tests passed. The offline NuGet vulnerability lookup emitted `NU1900` warnings. Restoring with `NuGetAudit=false --ignore-failed-sources` used cached dependencies; this is not a vulnerability-audit result.
- Final `dotnet build AIQuotaBar.slnf -c Release --no-restore -p:NuGetAudit=false`: **0 warnings, 0 errors**.
- Final `dotnet test AIQuotaBar.slnf -c Release --no-restore -p:NuGetAudit=false`: **640 passed, 0 failed**. These include the paused/no-process and cancelled-refresh tests, stale cached quota and tray behavior, and the existing provider, settings, tray, and docking tests.
- Final portable runtime observation: **PASS for the paused-process and staging-growth check** on the exact executable SHA-256 `4A58D83AFEB0C08CA2E12069D035BBF4E2DADB8D92884CE5DF517B113DDC0919`. In the signed-in Windows profile, the hidden app ran for 661 seconds (more than ten normal 60-second refresh intervals), then was stopped and restarted for another 90 seconds. The monitor saw 0 Codex descendants, 0 new marketplace staging folders (653 before and after), 0 visible main windows among observed descendants, and 0 owned descendants remaining after either stop. Other-provider descendants (`agy.exe`, `copilot.exe`, `grok.exe`, `conhost.exe`) were observed. The exact process and filesystem record is `artifacts/hotfix-codex-trace/final-candidate-check.json`. Because the app was launched hidden, `CloseMainWindow` did not exit it and the harness force-stopped only its own candidate process; graceful tray exit was **NOT TESTED**. No Codex quota reads occurred by design, so the preferred successful-quota-read gate is not met.
- Windows App Certification Kit 10.0.28000.2705 is present, but **NOT RUN** for these bytes. The historical 1.0.4 full run was **WARNING**, with 12 required checks passing, a DPI warning, and an optional blocked-executables failure. That report does not transfer to 1.0.5. Running a package certification workflow on this host could install the unsigned candidate or alter trust, which is outside the authorized test scope. Run WACK in a disposable test environment with an appropriate test certificate.
- Actual MSIX update and settings preservation: **NOT RUN**. No Store package was installed for this profile at inspection, and the user's current app must not be replaced. A disposable environment with a known predecessor package is required. Source identity, publisher, and settings location are unchanged, but that is not an upgrade test.
- Partner Center acceptance and version uniqueness: **NOT VERIFIED**. Confirm the exact currently approved/published version and that `1.0.5.0` is a valid higher submission version before upload.

## Draft release notes and known issue

**Draft notes:** AIQuotaBar 1.0.5 pauses Codex quota polling as a safeguard against marketplace scratch-folder growth seen with short-lived Codex helpers. Other providers continue to refresh. Existing Codex quota values in an open session appear as stale; check Codex itself for current quota.

**Known issue and workaround:** Codex quota is temporarily unavailable in AIQuotaBar 1.0.5. Open Codex's own usage view for current allowance. If the account changes, disregard any stale Codex values shown in the running widget and restart AIQuotaBar to clear the in-memory cache. The unrelated recurring popup has not been diagnosed or fixed by this update.

## Existing staging-folder recovery

This hotfix does not delete any existing `%USERPROFILE%\.codex\.tmp\marketplaces\.staging\marketplace-upgrade-*` directories. A folder may belong to an active upgrade or be the only surviving backup from an interrupted one. First close AIQuotaBar and normal Codex sessions, verify no Codex marketplace operation is active, and review age, contents, and any open handles. Preserve copies of data that may be needed for recovery. Only after distinguishing confirmed abandoned scratch from active operations and backups should the owner consider manual cleanup. No shared-directory deletion is part of the app.

## Remaining release gates

1. In a disposable Windows environment, run WACK on the exact candidate and test a real upgrade from the verified predecessor package, including preference retention, app launch, and graceful tray exit.
2. Check the Store's approved/published version and validate that 1.0.5.0 is the next acceptable update version.
3. Review the known issue and release notes; obtain explicit approval before installing, submitting, publishing, or releasing.
