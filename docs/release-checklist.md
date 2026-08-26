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
  - File size is self-contained (~180MB single-file bundle).

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

## 3. Owner Actions Required Before Public Launch

The repository maintainer must complete these actions prior to public release:

- [ ] **Add Final README Screenshot:** Capture a clean UI screenshot and place it at `docs/images/app-preview.png`.
- [ ] **Review Code Signing Strategy:** Confirm whether releasing unsigned for v0.2.0 preview or enrolling in Microsoft Trusted Signing (see [`docs/code-signing.md`](code-signing.md)).
- [ ] **Change Repository Visibility:** Transition GitHub repository from **Private** to **Public** when ready for public preview.
- [ ] **Enable GitHub Private Vulnerability Reporting:** In GitHub repo settings -> *Security* -> *Vulnerability alerts*, enable *Private vulnerability reporting*.
- [ ] **Configure GitHub Sponsors (Optional):** If accepting sponsorships, set up GitHub Sponsors and add `.github/FUNDING.yml`.
- [ ] **Approve & Push Tag:** Create and push the Git tag:
  ```powershell
  git tag -a v0.2.0 -m "Release v0.2.0"
  git push origin v0.2.0
  ```
- [ ] **Approve GitHub Release:** Verify the automated `.github/workflows/release.yml` workflow finishes and publishes the release archive.

---

## 4. Rollback & Stop Conditions

Immediately abort release publication if any of the following occur:

* Build or test failures (`dotnet test` fails any test or produces build warnings).
* Any secret, token, email, or credentials detected in code, tests, or documentation.
* Unhandled exception during startup or tray interaction.
* Child processes orphaned after application exit.
* Accidental network requests made directly by `AIQuotaBar.exe`.
