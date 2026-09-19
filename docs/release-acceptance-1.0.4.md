# 1.0.4 release candidate acceptance

Local acceptance completed on 19 September 2026. The rebuilt candidate is accepted for the next Store submission with the documented WACK warning; Microsoft certification remains a separate decision.

## Candidate identity

- App version: 1.0.4; package version: 1.0.4.0, x64.
- Package: AGIFutu.AIQuotaBar; publisher: CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24.
- Rebuilt portable executable SHA-256: `2028FF1A7FB5C28DF152EFE02CE032ACC033D6E4A2B3EB510A218AF078577B2B`.
- Rebuilt Store upload SHA-256: `86DFCEE5094A53764D6A62976C923FB12C48EC6CA3993DF3F18CF58CE4D286E5`.
- Rebuilt nested unsigned MSIX SHA-256: `0CFFFC1CF8788782C46FE78015AD2B65649699E9A3709BEAC12FC669B71E35BC`.
- Rebuilt temporarily signed test MSIX SHA-256: `C3599B64346FED783BB364E6CCFF4765034082094BE34ED90F42769D41E79139`.

The only source change from the prior candidate is Codex `SafeErrorMessage` ordering: recognized authentication messages now take precedence over the generic JSON-RPC `-32600` label. No provider credentials were copied. The official Codex CLI used for validation was version 0.148.0, SHA-256 `2AD2CF8A732DA68B8F141634F92DB1A03016C5FAF533A7225FBC0FB740130410`.

## Rebuilt candidate checks

- Release build completed with zero warnings and zero errors.
- All 644 offline tests passed. Tests used cached dependencies with `NuGetAudit=false` because the offline NuGet vulnerability feed was unavailable; no dependencies changed and this is not a new vulnerability-audit result.
- The actual nested upload passed identity/version/startup validation.
- The rebuilt Store payload installed and launched in the VM without a .NET Desktop Runtime. Signed-out Codex correctly displayed “Codex is not authenticated”; Settings showed “Sign in required” with setup guidance.
- Repeated rescans visibly completed, including with the VM network disconnected. These were signed-out tests; they do not establish authenticated offline/reconnection behavior.
- The rebuilt portable executable launched in the same no-runtime VM, displayed the corrected authentication status and exited cleanly. Its transferred hash matched the frozen executable.
- Final guest cleanup verified zero packages, app processes, portable startup entries and temporary test certificates. Local preferences remained unchanged.
- Full WACK 10.0.28000.2705 completed on 19 September at 13:11 UTC with `PARTIAL_RUN=FALSE`, exit code 0 and overall **WARNING**. Twelve required checks passed; DPIAwarenessValidation warned and the optional Blocked executables check failed. This is not a certification pass.
- The executable carrying the DPI manifest is byte-identical to the prior diagnostic executable (SHA-256 `4E65EF6BABC44C0D9F18E18711BD3BC31D58DBD7E2D68727574F8A64E15D51E3`). The earlier installed-path parser failure therefore applies to identical executable bytes; the complete new WACK report is retained separately.
- The host installed signed copy has SHA-256 `23CB91BEA11A5E0C30C618A758CE842941B620AD0A71B891159D5E64D192E550`; its unsigned payload is the frozen candidate above. Installed process identity was independently verified. The user confirmed healthy quota display, two completed rescans, hide/tray-open and exit.
- Independent host cleanup confirmed zero packages and app processes, no exact temporary certificate in either trust/signing store, and unchanged user settings.

Local evidence is under `artifacts/validation/2026-09-19-auth-message` and the guest records in `artifacts/validation/2026-09-19-store-ready/vm-setup`. A transient terminal-shaped window was observed once; later instrumented rescans showed no visible guest console, with only a hidden console belonging to VirtualBox's test helper recorded. The original transient window was not conclusively attributed. VM-window captures include an unrelated host quota strip and are not guest live-quota screenshots.

During testing of the superseded installed candidate, signed-out Codex displayed the generic request-rejection message even though Settings correctly showed “Sign in required”; rescans completed in both connected and disconnected conditions. The rebuilt candidate contains the message-ordering fix and must be validated separately.

## Superseded frozen candidate evidence

The following hashes and installed acceptance results belong to the earlier frozen candidate and must not be presented as validation of the rebuilt bytes:

- Store upload SHA-256: `8B841C8C51320CA4ED26900C7E06C2293DE96B9668887BEE8561D34E263ADDC1`.
- Nested unsigned MSIX SHA-256: `DFFC1B7E300FA0DC1A5AF7AF5EA5AADC00D72C394DEBD3A10F2900DB37C39216`.
- Temporarily signed test MSIX SHA-256: `71790D61E517B31F2C2C1C4B4DD620490F25B3DE7FE31BB3C00B3F2A317101A5`.
- Portable executable SHA-256: `97391167F410C04C18C342704D744886B77E25D50A0C17DB0503BDE2DBC9C616`.

That candidate passed the recorded upload identity/version/startup validation. Its host and disposable-VM checks covered quota display, repeated rescans, clean no-provider onboarding, hide/tray-open, uninstall cleanup, and an upgrade from the retained locally signed 1.0.3.0 test candidate. The predecessor was not a retrieved Store-delivered package. Settings retained the tested preferences across upgrade and reboot recovery. These remain superseded-candidate results.

The earlier WACK run (10.0.28000.2705) reported **WARNING**, not PASS: twelve required checks passed, DPI awareness warned, and the optional blocked-executables check failed on process-launch API references. A separate diagnostic recognized DPI awareness from identical executable bytes outside WindowsApps but returned E_FAIL at the installed path. This result is retained for the superseded bytes and does not establish Store certification for the rebuilt candidate.

## Release gate

The host and VM results support resubmission for the reported Codex/rescan functionality failure. Retain the full WACK WARNING report with its diagnostic rather than representing it as PASS. A release-workflow rebuild still needs a downloaded-asset checksum and launch check. Do not claim a complete manual DPI, multi-monitor, sleep/resume, or five-cycle tray matrix until separately recorded for the rebuilt candidate. No Store certification outcome is implied by local acceptance.
