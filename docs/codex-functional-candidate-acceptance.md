# Codex functional portable candidate — 2 October 2026

Status: **Local functional candidate verified for the tested binary/profile. The user authorized commit, push, GitHub publication and Windows Store submission on 2 October 2026.** See [the release record](release-acceptance-1.0.5.md) for subsequent results.

Candidate: `artifacts/candidates/2026-10-02-codex-functional/portable/AIQuotaBar.exe` (1.0.5, self-contained Windows x64, trimming disabled).

SHA-256: `ACB64DC306BABDE2F5DE4E1485B929A27225471AD9B973291795325454025B81`.

Size: 213,096,684 bytes. The old paused candidate remains preserved and explicitly marked as containment only.

## Repair and bounded cause

Official Codex source shows that the global plugins feature enables marketplace upgrade startup in app-server. The quota helper now launches `--disable plugins app-server`, verifies the official effective `config.features.plugins` value is false, then performs the normal account and quota reads. This setting belongs only to that helper process. No global Codex configuration, installed binary, credential store, scheduled task or shared staging folder was modified.

If the configuration is missing, unsafe, or the verification method is unsupported, the provider contains that selected executable for its remaining app session. It does not fall back to an unsafe launch or repeatedly retry it. Ordinary transient errors retain last-known-good behavior and permit the next normally requested refresh. Original account scopes, quota normalization, process timeout and owned-tree cleanup remain intact.

The earlier alpha.16 staging folder's writer remains unknown. This repair is supported by checked source and new attributed live evidence; it does not retrospectively establish that historical writer. See [the investigation](codex-staging-corrective-investigation.md) for preserved unsuccessful captures and source references.

## Evidence

| Check | Result and evidence type |
| --- | --- |
| Release solution build | Automated: zero warnings and errors. |
| Full offline suite | Automated: 661 passed, including restored original checks and successful provider-session, account-transition, cancellation, timeout/crash recovery and refresh-overlap coverage. |
| Attribution rejection rules | Automated: 22 offline cases plus runtime writer/child/create/write/delete self-tests. |
| Ten fresh production-provider refreshes | Live automated: ten Available results, each completing a new official quota RPC at 60-second intervals with the actual default six-second budget. All had three configured marketplaces; no retries or cached quota substitutions. Capture `corrective-20261002-103047-d2124958`. |
| Filesystem attribution | Live automated: global and exact-helper attribution conclusive, zero lost/unresolved events, zero staging activity, zero new persistent or transient folders, zero owned processes alive at capture end. |
| Automatic source-widget polling | Live process trace: fifteen minute-spaced Codex starts, ten clean exits and five exit-status -1 events, no overlapping Codex helpers or staging activity. Individual widget snapshot statuses were not logged; forced helper cleanup cannot alone distinguish a successful read from a failure. Capture `widget-20261002-100759-8ede112a`. |
| Widget display and console behavior | Manual: user reported healthy visible widget, manual refresh looking fine and no console pops during the observed period. UI automation could not target the saved bottom-docked auto-hide widget. |
| Manual refresh and graceful source-widget exit | Manual action corroborated by live trace: three Codex helpers exited cleanly without overlap; actual app root exited with code 0; every owned descendant ended; no staging activity. Sanitized `graceful-exit-result.json` is retained in the separate exit capture. |
| Plugin coexistence | Live: Context7 returned a normal read-only result in this Codex session while polling was active. User's usual plugin workflows were not exercised; do not describe them as manually verified. |
| Portable provider identity | Read-only bundle inspection: embedded provider SHA-256 `3E0D91751EE23FA9DE1DE49B0A57932A93EF32C1CD8A9DC4C4E76353B81C37CC`, identical to the tested provider, with verified launch string. |
| Portable app identity | Embedded app SHA-256 `C53D8CCF46832F8299C8320140D82DD364C4EE290735B25C456F82625D0599B7`, identical to its Windows x64 publish assembly. The framework-dependent source-widget assembly differs; no app source changed between these builds. |
| Portable restart and manual refresh | Manual: user reported "closed - all good" after the requested quota-display, refresh, console observation and tray-exit check. Live trace captured five clean Codex exits without overlap or helper staging activity. |
| Portable exit and cleanup | User-confirmed tray exit, corroborated by absence of the app, all captured helpers and current direct children. The bounded trace ended shortly before actual exit, so the portable root exit code was not captured. The source-widget root exit code was directly captured as 0. |
| Portable attribution limits | Zero lost events and zero unresolved owned writes; exact-helper attribution conclusive. One unresolved write belongs to an identified unrelated process, so global attribution in this short trace is incomplete. No new folders appeared. The longer source-widget and ten actual-provider captures had conclusive global attribution. |

All live checks used the existing signed-in Windows user and installed **codex-cli 0.159.2**, selected by the ordinary desktop-first locator. CLI SHA-256: `CBAFB6422BCA005B94C12D105B1A16A0474219E24EA4893F85846409C464F5A1`. The earlier standalone ten-read experiment independently verified signed-in ChatGPT authentication, three marketplaces and the effective disabled plugins feature on this binary. No empty or signed-out profile was substituted.

Raw evidence remains local and Git-ignored. Sanitized identity/result files record capture and analysis hashes. Portable trace: `portable-restart-20261002-104221-00c524a3`; `portable-restart-result.json` preserves the capture-end state, and `post-exit-process-check.json` records the later actual closure. The capture-end root was still alive; it must not be rewritten as a captured exit. Trace duration and storage were bounded; no diagnostic monitor persists after completion.

## Limitations and release boundary

Live validation covers this installed binary/profile. Other versions must pass effective-configuration verification; they are not covered by these live results. The previous alpha.16 installation is no longer present. A Codex-desktop-closed comparison was not run because this task is hosted inside Codex; the standalone capture script can perform that separately. Broader user plugin workflows remain untested.

The isolated portable candidate did not replace the installed app. At completion of this local validation, no commit, push, tag, install, publication, Store rebuild or submission had been performed. The user subsequently authorized release on 2 October 2026; broader Windows/Store certification checks and their actual results are tracked separately in the release record.
