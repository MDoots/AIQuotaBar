# AIQuotaBar — Release & Certification Checklist

This document details the repeatable end-to-end verification and quality checklist for approving, packaging, and publishing releases of **AIQuotaBar** across GitHub Releases and the Microsoft Store.

---

## 1. Engineering Verification (Automated & Offline)

Execute from a clean working directory on `main` (or the approved release branch):

- [ ] **Working Tree Clean:** `git status` shows 0 untracked files and working tree is clean.
- [ ] **Version Synchronization:**
  - `src/AIQuotaBar.App/AIQuotaBar.App.csproj` has `<Version>1.0.0</Version>` and `<AssemblyVersion>1.0.0.0</AssemblyVersion>`.
  - `src/AIQuotaBar.Core/AIQuotaBar.Core.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Providers.Codex/AIQuotaBar.Providers.Codex.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Providers.Antigravity/AIQuotaBar.Providers.Antigravity.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Providers.ClaudeCode/AIQuotaBar.Providers.ClaudeCode.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Providers.GrokBuild/AIQuotaBar.Providers.GrokBuild.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Providers.GitHubCopilot/AIQuotaBar.Providers.GitHubCopilot.csproj` has `<Version>1.0.0</Version>`.
  - `src/AIQuotaBar.Package/Package.appxmanifest` has `Version="1.0.2.0"`.
- [ ] **Clean Build:** `dotnet build AIQuotaBar.slnf -c Release` completes with `0 Warning(s)` and `0 Error(s)`.
- [ ] **Automated Offline Tests:** `dotnet test AIQuotaBar.slnf -c Release` passes 100% of offline unit tests without network access or live provider logins.
- [ ] **Live Provider Harness:** `pwsh scripts/test-live-providers.ps1` executes safely, protects existing user processes, and passes all probes.
- [ ] **Git Diff Check:** `git diff --check` passes with no whitespace, line-ending, or formatting errors.

---

## 2. Portable Binary Packaging

- [ ] **Build Portable:** `powershell -ExecutionPolicy Bypass -File scripts/build-portable.ps1` completes successfully.
- [ ] **Inspect Portable Output:**
  - Executable exists at `artifacts/portable/win-x64/AIQuotaBar.exe`.
  - File/Product version in Windows file properties reports `1.0.0`.
  - File size is self-contained (~203 MB single-file bundle).
  - SHA-256 hash is computed and recorded.
- [ ] **Standalone Smoke Test:** Run `AIQuotaBar.exe` directly on Windows 11 without runtime dependencies installed.

---

## 3. UI & Feature Verification

- [ ] **Floating Mode:** Verify smooth layout transitions across Full, Compact, and Minimal modes.
- [ ] **Adaptive Resizing:** Verify horizontal drag resize from left and right edges (170px to 580px width) with proportional text truncation.
- [ ] **Docked Mode:** Drag widget to top or bottom screen edge to dock; verify magnetic snapping, auto-hide on mouse leave, and expand on mouse enter.
- [ ] **Settings Window:** Open Settings; verify all five provider setup cards (Codex, Antigravity, Claude Code, Grok Build, GitHub Copilot), checkboxes, notifications toggle, docking options, and About section.
- [ ] **System Tray:** Right-click tray icon; verify "Open AIQuotaBar", status summary, refresh, docking sub-menu, and exit items.
- [ ] **Sleep / Resume:** Verify that Windows sleep and wake recovery cleanly updates provider status and retains last-known-good quota.

---

## 4. Microsoft Store Certification & Window Restore Verification

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

- [ ] **Package Identity:** Verify `Package.appxmanifest` matches official Partner Center identity:
  - Name: `AGIFutu.AIQuotaBar`
  - Publisher: `CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24`
  - Version: `1.0.2.0`
  - PublisherDisplayName: `AGIFutures`
- [ ] **Startup Task Configuration:** Confirm `windows.startupTask` executable is set to `AIQuotaBar.App\AIQuotaBar.exe` and matches package entry point.
- [ ] **Store Listing Draft:** Check `store-assets/listing-v1.0.md` for descriptions, keywords, support URL, privacy URL, and certification notes.
- [ ] **Privacy Policy:** Ensure `PRIVACY.md` is updated and accessible publicly at `https://github.com/MDoots/AIQuotaBar/blob/main/PRIVACY.md`.
- [ ] **Product Screenshots:**
  - [ ] Floating screenshot exists at `docs/images/app-preview.png`
  - [ ] Docked screenshot exists at `docs/images/app-docked.png`
  - [ ] Both show the current v1.0 UI using real application data
  - [ ] Both contain no credentials, account identifiers, or unrelated private content
  - [ ] Store screenshot upload accepted by Partner Center

---

## 6. GitHub Release Sequence

1. [ ] **Changelog:** Finalize `CHANGELOG.md` with release date and feature summary under `## [1.0.0]`.
2. [ ] **Tag Commit:**
   ```powershell
   git tag -a v1.0.0 -m "Release v1.0.0"
   ```
3. [ ] **Push Tag:**
   ```powershell
   git push origin v1.0.0
   ```
4. [ ] **Automated GitHub Release:** Confirm `.github/workflows/release.yml` completes and generates:
   - `AIQuotaBar-v1.0.0-win-x64.zip`
   - `AIQuotaBar-v1.0.0-win-x64.zip.sha256`
5. [ ] **Download & Verify Release Asset:** Download published archive, verify SHA-256 hash against checksum file, extract, and execute.

---

## 7. Security & Privacy Safeguards

Abort release publication immediately if any of the following occur:

* Any secret, access token, API key, password, or private email exposed in source, tests, or documentation.
* Direct reading or parsing of provider credential stores (e.g. `.codex/auth.json`).
* Telemetry, analytics, tracking beacons, or outbound network calls made by `AIQuotaBar.exe`.
* Failure to cleanly terminate child processes upon exit or timeout.
