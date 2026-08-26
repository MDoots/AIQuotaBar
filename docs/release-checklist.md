# AIQuotaBar Release Checklist

This document details the step-by-step verification process for approving and publishing releases of **AIQuotaBar**.

---

## 1. Pre-Release Engineering Verification (Automated & Offline)

Execute all checks from a clean working directory:

- [ ] **Working Tree Clean:** `git status` shows no untracked or uncommitted files.
- [ ] **Version Synchronization:**
  - `src/AIQuotaBar.App/AIQuotaBar.App.csproj` has `<Version>0.2.0</Version>` and `<AssemblyVersion>0.2.0.0</AssemblyVersion>`.
  - `src/AIQuotaBar.Core/AIQuotaBar.Core.csproj` has `<Version>0.2.0</Version>`.
  - `src/AIQuotaBar.Providers.Codex/AIQuotaBar.Providers.Codex.csproj` has `<Version>0.2.0</Version>`.
  - `src/AIQuotaBar.Providers.Antigravity/AIQuotaBar.Providers.Antigravity.csproj` has `<Version>0.2.0</Version>`.
- [ ] **Clean Build:** `dotnet build AIQuotaBar.slnx -c Release` completes with `0 Warning(s)` and `0 Error(s)`.
- [ ] **Automated Tests:** `dotnet test AIQuotaBar.slnx -c Release` passes 100% of offline unit tests (121 tests passing).
- [ ] **Portable Packaging:** `.\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64` succeeds and produces `artifacts\portable\win-x64\AIQuotaBar.exe`.
- [ ] **Binary Inspection:**
  - Executable properties report version `0.2.0.0`.
  - No debug symbols (`.pdb`), test assemblies, or temporary files are present in the output.
  - File size is self-contained (~165MB single-file bundle).

---

## 2. Owner Manual Verification & Smoke Testing (Live Accounts)

These checks require live provider installations and user interaction:

- [ ] **Clean Launch:** Launch `artifacts\portable\win-x64\AIQuotaBar.exe` on Windows 11. Confirm immediate UI rendering without runtime exceptions.
- [ ] **OpenAI Codex Live Smoke Test:** If `codex` is installed and authenticated, verify that OpenAI quota windows display realistic reset times and percentages.
- [ ] **Google Antigravity Live Smoke Test:** If `agy` is installed and authenticated, verify that Antigravity model quotas display valid remaining limits.
- [ ] **Compact / Expanded Toggle:** Click the mode toggle button. Verify smooth transition between expanded multi-provider view and compact single-line view.
- [ ] **Always On Top:** Toggle the pin button. Confirm the window stays above active applications.
- [ ] **Window Position Persistence:** Drag the widget to a non-default monitor location, close the app, relaunch, and verify position is restored from `%LOCALAPPDATA%\AIQuotaBar\settings.json`.
- [ ] **System Tray Integration:** Minimize to tray. Verify the `NotifyIcon` appears in the Windows notification area, and right-click context menu functions (Open, Exit).
- [ ] **Start with Windows Toggle:** Toggle startup setting. Verify `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` reflects the change cleanly.
- [ ] **Process Safety & Child Termination:** Close AIQuotaBar. Verify in Task Manager that no orphan `codex` or `agy` child processes remain running.

---

## 3. Owner Public Launch Sequence

The repository maintainer executes the following sequential steps for public release:

1. [ ] **Provide Application Screenshot:** Place final UI capture at `docs/images/app-preview.png`.
2. [ ] **Live Codex Smoke Test:** Verify quota display with active local Codex app-server.
3. [ ] **Live Antigravity Smoke Test:** Verify quota display with active local `agy` CLI.
4. [ ] **Visual Confirmation:** Verify UI in both expanded and compact modes.
5. [ ] **Confirm Unsigned Preview Decision:** Confirm shipping v0.2.0 unsigned with published SHA-256 checksums.
6. [ ] **Switch Repository Visibility:** Transition GitHub repository from **Private** to **Public**.
7. [ ] **Enable GitHub Private Vulnerability Reporting:** Navigate to *Settings* -> *Security* -> *Vulnerability alerts* and enable *Private vulnerability reporting*.
8. [ ] **Verify Public Rendering:** Confirm `README.md` and repository pages render correctly when public.
9. [ ] **Approve & Create Tag:**
   ```powershell
   git tag -a v0.2.0 -m "Release v0.2.0"
   ```
10. [ ] **Push Tag:**
    ```powershell
    git push origin v0.2.0
    ```
11. [ ] **Verify Release Workflow:** Confirm the automated `.github/workflows/release.yml` run completes successfully.
12. [ ] **Verify Assets:** Confirm `AIQuotaBar-v0.2.0-win-x64.zip` and `AIQuotaBar-v0.2.0-win-x64.zip.sha256` are attached to the release.
13. [ ] **Download & Smoke Test Release Asset:** Download the published ZIP from GitHub Releases, verify its SHA-256 hash, extract, and execute `AIQuotaBar.exe`.

*(Note: Enrolling in GitHub Sponsors is optional and does not block public release).*

---

## 4. Rollback & Stop Conditions

Immediately abort release publication if any of the following occur:

* Build or test failures (`dotnet test` fails any test or produces build warnings).
* Any secret, token, email, or credentials detected in code, tests, or documentation.
* Unhandled exception during startup or tray interaction.
* Child processes orphaned after application exit.
* Accidental network requests made directly by `AIQuotaBar.exe`.
