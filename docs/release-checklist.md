# AIQuotaBar — Release & Certification Checklist

This document details the repeatable end-to-end verification and quality checklist for approving, packaging, and publishing releases of **AIQuotaBar** across GitHub Releases and the Microsoft Store.

For the current candidate's completed checks, exact hashes and remaining limitations, see [1.0.4 acceptance](release-acceptance-1.0.4.md). This checklist remains a reusable verification template.

---

## 1. Engineering Verification (Automated & Offline)

Execute from a clean working directory on `main` (or the approved release branch):

- [ ] **Working Tree and Provenance:** Record the exact source revision and intentional dirty state used for the candidate.
- [ ] **Version and Identity Synchronization:** Compare project/package versions, identity, publisher, architecture, entry point, and StartupTask against the authoritative product association and unused version selected for this submission. Do not invent or reuse an earlier submission version.
- [ ] **Clean Build:** `dotnet build AIQuotaBar.slnf -c Release` completes with `0 Warning(s)` and `0 Error(s)`.
- [ ] **Automated Offline Tests:** `dotnet test AIQuotaBar.slnf -c Release` passes 100% of offline unit tests without network access or live provider logins.
- [ ] **Live Provider Harness:** `pwsh scripts/test-live-providers.ps1` executes safely, protects existing user processes, and records observed statuses. Do not claim quota when the supported interface exposes only authentication status or no finite allowance.
- [ ] **Git Diff Check:** `git diff --check` passes with no whitespace, line-ending, or formatting errors.

---

## 2. Portable Binary Packaging

- [ ] **Build Portable:** `powershell -ExecutionPolicy Bypass -File scripts/build-portable.ps1` completes successfully.
- [ ] **Inspect Portable Output:**
  - Executable exists at `artifacts/portable/win-x64/AIQuotaBar.exe`.
  - File/Product version matches the selected candidate and its recorded source state.
  - File size is self-contained (~203 MB single-file bundle).
  - SHA-256 hash is computed and recorded.
- [ ] **Standalone Smoke Test:** Run `AIQuotaBar.exe` directly on Windows 11 without runtime dependencies installed.
- [ ] **Frozen Artifact Provenance:** Record SHA-256 hashes for the exact portable archive, executable, package, and generated manifest tested and handed off. Repeat evidence if bytes change.

---

## 3. UI & Feature Verification

- [ ] **Floating Mode:** Verify smooth layout transitions across Full, Compact, and Minimal modes.
- [ ] **Adaptive Resizing:** Verify horizontal drag resize from left and right edges (170px to 580px width) with proportional text truncation.
- [ ] **Docked Mode:** Drag widget to top or bottom screen edge to dock; verify magnetic snapping, auto-hide on mouse leave, and expand on mouse enter.
- [ ] **Settings Window:** Open Settings; verify all five provider setup cards (Codex, Antigravity, Claude Code, Grok Build, GitHub Copilot), checkboxes, notifications toggle, docking options, and About section.
- [ ] **System Tray:** Right-click tray icon; verify "Open AIQuotaBar", status summary, refresh, docking sub-menu, and exit items.
- [ ] **Sleep / Resume:** Verify that Windows sleep and wake recovery cleanly updates provider status and retains last-known-good quota.
- [ ] **Installed MSIX Observation:** Test the exact frozen MSIX on a clean supported Windows environment, including first launch, onboarding, settings, startup identity, upgrade, and uninstall behavior. Local installed quota/tray/DPI smoke was user-observed on 2026-09-17; this is not a clean-machine or upgrade result.

---

## 4. Microsoft Store Certification & Window Restore Verification

- [ ] **Latest Functionality Rejection (review dated 09/02/2026):** On the final installed candidate, reproduce first launch and Settings **Rescan providers** with missing, signed-out, disconnected and healthy Codex CLI states. A scan must show progress and a persistent result even if nothing changes. Missing tools must show setup guidance; failed quota access must remain unavailable/error, never fabricated quota. Verify recovery after correcting the local CLI setup. Retain CLI version and exact package hash with the evidence.
- [ ] **Zero-Provider Onboarding State:** On a clean machine without provider CLIs installed, confirm the widget opens with:
  - Header: `"No supported providers detected"`
  - Description: `"Install or sign in to a supported provider, then rescan in Settings."`
  - Button: `"Set up providers"` opening Settings.
- [ ] **Notification Area Reopen:**
  - Launch AIQuotaBar.
  - Minimize/hide to tray with the minus (`-`) button.
  - Right-click tray icon and select **"Open AIQuotaBar"** (or left-click / double-click icon).
  - Verify the main window immediately becomes visible in the foreground.
- [ ] **Repeated Reopen Idempotency:** Repeat hide and Open at least 5 consecutive times without error or position drift.
- [ ] **Off-Screen Recovery:** If the window was previously positioned on a disconnected monitor or off-screen, confirm "Open AIQuotaBar" recovers the window to a valid on-screen working area.
- [ ] **Docked Mode Reopen:** While docked with auto-hide active, verify "Open AIQuotaBar" restores the window expanded and properly anchored.

---

## 5. Store Packaging & Metadata

- [ ] **Fresh Store Upload:** Build the WAP project with `/t:Rebuild` and `UapAppxPackageBuildMode=StoreUpload` into a new candidate directory. An incremental build can retain a stale upload manifest even when the output filename and standalone test MSIX carry the new version.
- [ ] **Inspect Actual Upload Payload:** Run `powershell -ExecutionPolicy Bypass -File scripts/validate-store-upload.ps1 -UploadPath <candidate.msixupload>`. This must pass against the current source manifest. Keep the upload's nested MSIX identity and hash in the acceptance record; the standalone test package is not sufficient evidence.
- [ ] **Package Identity:** Verify `Package.appxmanifest` matches the authoritative Partner Center identity, publisher, unused next version, architecture, and entry point. Partner Center on 2026-09-19 showed the failed 1.0.3.0 package; the next candidate is 1.0.4.0 with app version 1.0.4. This selection is not Store acceptance.
- [ ] **Startup Task Configuration:** Confirm `windows.startupTask` executable is set to `AIQuotaBar.App\AIQuotaBar.exe` and matches package entry point.
- [ ] **Store Listing Draft:** Check `store-assets/listing-v1.0.md` for descriptions, keywords, support URL, privacy URL, and certification notes.
- [ ] **Certification Evidence:** Latest two-page report received on 2026-09-17: policy 10.1.2.10, Codex connection error on launch and unresponsive-looking Rescan. Partner Center confirms submission 1152921505701753706 contains `AIQuotaBar.Package_1.0.3.0_x64.msixupload`; its exact hash and supporting ZIP remain unverified. Map each failure to final-candidate acceptance evidence before closure.
- [ ] **WACK:** Run the installed kit's full applicable Windows App Certification Kit suite against the exact eligible package and retain the complete report, including optional findings. Both 10.0.22621.5040 and 10.0.28000.2705 returned WARNING despite runtime per-monitor awareness. A 2026-09-19 direct parser probe reproduced E_FAIL reading DPI from WindowsApps while recognizing identical executable bytes outside that directory. Retain this diagnostic alongside the actual report; do not infer certification from exit code alone.
- [ ] **Privacy Policy:** Ensure `PRIVACY.md` is updated and accessible publicly at `https://github.com/MDoots/AIQuotaBar/blob/main/PRIVACY.md`.
- [ ] **Product Screenshots:**
  - [ ] Floating screenshot exists at `docs/images/app-preview.png`
  - [ ] Docked screenshot exists at `docs/images/app-docked.png`
  - [ ] Both show the current v1.0 UI using real application data
  - [ ] Both contain no credentials, account identifiers, or unrelated private content
  - [ ] Store screenshot upload accepted by Partner Center

---

## 6. GitHub Release Sequence

1. [ ] **Changelog:** Finalize `CHANGELOG.md` for the explicitly selected release version and date.
2. [ ] **Tag Commit:** Only after explicit publication approval, tag the exact tested commit with the selected unused version.
3. [ ] **Push Tag:** Only after explicit approval, push that tag; this triggers the release workflow.
4. [ ] **Automated GitHub Release:** Confirm `.github/workflows/release.yml` completes and generates the versioned ZIP and SHA-256 checksum matching the approved release record.
5. [ ] **Download & Verify Release Asset:** Download published archive, verify SHA-256 hash against checksum file, extract, and execute.

---

## 7. Security & Privacy Safeguards

Abort release publication immediately if any of the following occur:

* Any secret, access token, API key, password, or private email exposed in source, tests, or documentation.
* Direct reading or parsing of provider credential stores (e.g. `.codex/auth.json`).
* Telemetry, analytics, tracking beacons, or outbound network calls made by `AIQuotaBar.exe`.
* Failure to cleanly terminate child processes upon exit or timeout.
