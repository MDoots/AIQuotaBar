# AIQuotaBar 1.0.5 release and Store submission

Release date: 2 October 2026. The user approved the functional update and explicitly authorized commit, push, GitHub publication and Microsoft Store submission. Publication and submission are in progress; Microsoft certification is a separate decision.

## Version and product

- App version: 1.0.5; package version: 1.0.5.0, Windows x64.
- GitHub's latest release was v1.0.4 and v1.0.5 was unused at inspection.
- Partner Center showed the published 1.0.4.0 package and no open update draft. The published submission was 1152921505701753706, last modified on 22 September 2026.
- Product: AIQuotaBar, Store ID 9NTTSH588BQ9.
- Identity: AGIFutu.AIQuotaBar; publisher CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24.

## Functional acceptance

The [functional candidate record](codex-functional-candidate-acceptance.md) contains frozen artifact hashes and separates automated, live and manual findings. Release build passed with zero warnings/errors; all 661 offline tests passed again before release preparation. Twenty-two offline diagnostic attribution cases also passed.

Ten actual-provider quota reads succeeded at sixty-second intervals with the normal six-second budget, official fresh quota RPCs and three configured marketplaces. Global and helper attribution were conclusive, with zero lost/unresolved events, no staging activity or new folders and no owned processes remaining. Live coverage used codex-cli 0.159.2 with SHA-256 CBAFB6422BCA005B94C12D105B1A16A0474219E24EA4893F85846409C464F5A1.

The user confirmed healthy source-widget and portable display/manual refresh without console pops, and actual tray exit. The source-widget exit code was captured as 0. Portable closure was confirmed by the user and process absence after its bounded trace; its root exit code was not captured. Exact-helper attribution in that short trace was conclusive, but one unrelated unresolved write prevented conclusive global attribution. Context7 worked concurrently; broader user plugin workflows and desktop-closed operation were not tested.

The original alpha.16 folder writer remains unknown. Enabled global plugins can initiate marketplace upgrade startup in checked official source. The repaired helper disables that feature for its own process and verifies the effective setting before normal quota reads. Global settings and shared staging folders remain unchanged. The earlier paused candidate's [historical record](codex-1.0.5-containment-record.md) and artifacts are preserved; they are not this release.

## Artifacts and publication

The accepted pre-commit portable candidate is artifacts/candidates/2026-10-02-codex-functional/portable/AIQuotaBar.exe, SHA-256 ACB64DC306BABDE2F5DE4E1485B929A27225471AD9B973291795325454025B81. It was built from the recorded working tree based on 82f0191, so its informational commit suffix is not the subsequent release commit.

GitHub workflow artifacts and the new Store payload will have their own recorded hashes. Do not substitute the preserved paused 1.0.5 Store upload. Fresh StoreUpload rebuild and nested-payload identity/version validation are required before submission. Installed-package and WACK results must be recorded for the new bytes; the previous 1.0.4 WACK WARNING does not establish a 1.0.5 result.

## Remaining results

- Commit/push and GitHub release: in progress.
- Fresh Store upload and payload validation: in progress.
- Current-package installed/WACK validation: pending.
- Store listing/reviewer notes and submission: in progress.
- Microsoft certification: not yet submitted or approved.

No host installation over the user's existing app is included in this release work.
