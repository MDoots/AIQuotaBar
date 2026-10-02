# AIQuotaBar 1.0.5 release and Store submission

Release date: 2 October 2026. The user approved the functional update and explicitly authorized commit, push, GitHub publication and Microsoft Store submission. GitHub v1.0.5 is published. Microsoft Store 1.0.5.0 was submitted for certification on 2 October 2026; approval and publication remain pending.

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

GitHub workflow artifacts and the new Store payload have separately recorded hashes below. Do not substitute the preserved paused 1.0.5 Store upload. Fresh StoreUpload rebuild and nested-payload identity/version validation are required before submission. Installed-package and WACK results must be recorded for the new bytes; the previous 1.0.4 WACK WARNING does not establish a 1.0.5 result.

## Publication results

- Source commit d22d011ebe09b4b9b67704805c48af146d5a5586 was pushed and merged through [PR #11](https://github.com/MDoots/AIQuotaBar/pull/11). The repository requires squash merges; the resulting main/tag revision is d4dde6e0f5896f7e126c19bb0bd08ae2652eea54. Its source tree is identical to the tested branch.
- [PR CI](https://github.com/MDoots/AIQuotaBar/actions/runs/36999111769) passed on one failed-job retry. The first run failed an existing Grok cancellation elapsed-time assertion under hosted-runner load; no production code or timeouts were changed. The packaging job's SDK-dependent build was skipped, so local packaging validation supplies that evidence.
- [Release workflow](https://github.com/MDoots/AIQuotaBar/actions/runs/37000333393) passed build, tests and publication. [GitHub v1.0.5](https://github.com/MDoots/AIQuotaBar/releases/tag/v1.0.5) contains the versioned ZIP and checksum, with final functional release notes.
- Downloaded archive SHA-256: 89B7D7D8940061B61623D70AD8131BA0A717CC3C9BAFECFE2B1937E5481D821A, matching the published checksum. Extracted executable SHA-256: 99DEB42454E58BA06BF164E57AC989A6DBB23B019F4884FBE98DE136E154D2F2, 213100780 bytes, file version 1.0.5.0, product version 1.0.5+d4dde6e0f5896f7e126c19bb0bd08ae2652eea54. A bounded launch smoke survived startup and was cleaned up by owned-tree termination; it is not an additional graceful-exit or UI quota test. Earlier manual acceptance applies to the separately hashed pre-commit candidate.
- Fresh unsigned StoreUpload was rebuilt from clean d22d011 source using Visual Studio MSBuild and SDK 10.0.22621. The merge/tag has identical source. Upload: AIQuotaBar.Package_1.0.5.0_x64.msixupload, SHA-256 CEFBBED2755D6932470936589BA1D7464E59FCB8F813AD86DD9F3441D13FE40F. Nested MSIX SHA-256 B4DA0EFD39027F793AF5B028FFCDAD72906711533572214A2262F51A671E22D6. The repository validator passed actual nested identity, version, executable and startup checks. Partner Center validated and saved the 1.0.5.0 x64 replacement package.
- Microsoft Store submission 1152921505702028367 (Submission 2) was accepted for certification on 2 October 2026. Partner Center shows "Update in certification": Submission complete, Pre-processing in progress, Certification and Publishing not started. English release notes describe 1.0.5, and the saved additional testing notes describe functional evidence, process-local configuration, provider prerequisites, runFullTrust and the new-package WACK limitation. Existing product description, screenshots, pricing and availability are preserved.
- Fresh installed-package and WACK validation could not run: the project-owned disposable Windows VM aborted after resuming an old saved guest session, and aborted again on one restart before guest preflight. Guest control returned RPC_S_CALL_FAILED/RPC_S_SERVER_UNAVAILABLE initially and "currently aborted" on the second preflight. A pre-test snapshot is preserved (9d7bc21d-c2e2-4fa0-ad50-4ab2bbcc32d3). No new package was installed on the host or guest. Current-package clean-machine, upgrade and WACK results remain unverified. The previous 1.0.4 WACK WARNING is not transferred to this package. The next local experiment requires repairing or replacing the disposable Windows validation environment.
- Automatic publication is enabled: Partner Center confirms "Your product will start publishing as soon as it passes certification." The initial submission used a manual hold after automatic approval review rejected saving auto-publication without explicit authorization. The user subsequently approved automatic publication and explicitly approved cancelling/resubmitting when the in-flight publishing editor did not open and options were read-only. Certification was cancelled, the automatic option saved, and the same submission 1152921505702028367 resubmitted with the same validated 1.0.5.0 package, listing and reviewer notes. The new review is in certification/pre-processing; approval and public rollout remain pending. The existing 1.0.4 Store release remains live. Initial and final confirmation screenshots and exact submitted notes are preserved locally under artifacts/releases/v1.0.5/.

No host installation over the user's existing app is included in this release work.