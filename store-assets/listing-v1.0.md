# AIQuotaBar — Microsoft Store Listing & Certification Guide (v1.0)

**Product Name:** AIQuotaBar  
**Product ID:** 9NTTSH588BQ9  
**Publisher:** AGIFutures (CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24)<br />
**Package Version:** 1.0.3.0<br />
**Public App Version:** 1.0.1

---

## 1. Store Metadata

### Short Description (up to 100 characters)
> Lightweight, private desktop widget monitoring AI subscription quotas and rate limits across tools.

### Full Description
AIQuotaBar is a lightweight, local-first Windows 11 desktop widget designed for developers and AI power users. It monitors your active AI subscription quotas, rate limits, and reset countdowns across your locally installed developer tools—all from a clean, unobtrusive bar.

### Supported Providers
* **OpenAI Codex:** 5-hour session and weekly account rate limits.
* **Google Antigravity:** Gemini, Claude, and GPT model rate limits with countdown timers.
* **Claude Code:** 5-hour session and weekly model allowances.
* **Grok Build:** Finite weekly or monthly quota windows when exposed by Grok Build.
* **GitHub Copilot:** Premium interactions, chat, and completion entitlements.

### Key Features
* **Adaptive Floating & Docked Modes:** Place the widget anywhere on your desktop, resize horizontally with responsive label scaling, or dock it magnetically to the top or bottom of your screen with auto-hide.
* **Compact & Minimal Views:** Switch effortlessly between an expanded multi-line overview and a compact single-line bar.
* **System Tray Health & Notifications:** Dynamic tray icon reflects overall quota health at a glance, with optional alerts when active quotas drop below 10%.
* **Resilient Offline Architecture:** Seamlessly recovers from Windows sleep and wake cycles while retaining last-known-good quota data through temporary connection pauses.
* **Local-First & Private:** Zero telemetry, zero analytics, no cloud backend, and no advertising. AIQuotaBar never reads or stores your passwords, API keys, or session tokens. All communication is direct local IPC with the official provider tools already installed on your PC.

### Requirements
AIQuotaBar displays live quota data from developer tools that you have installed and authenticated locally. A third-party provider installation is required for live quota display; on a clean machine without developer tools installed, AIQuotaBar provides a guided onboarding experience in Settings.

### Planned Screenshots
1. Floating mode (`docs/images/app-preview.png`)
2. Docked mode (`docs/images/app-docked.png`)

---

## 2. Store URLs

* **Support URL:** `https://github.com/MDoots/AIQuotaBar/issues`
* **Privacy Policy URL:** `https://github.com/MDoots/AIQuotaBar/blob/main/PRIVACY.md`
* **Repository / Homepage:** `https://github.com/MDoots/AIQuotaBar`

---

## 3. Release Notes (v1.0.1 / Package 1.0.3.0)

* Hardened provider onboarding guidance across Settings cards to explicitly require official command-line interfaces.
* Corrected Google Antigravity setup URL to the official CLI installation and authentication page (`https://antigravity.google/docs/cli/install/`).
* Directed GitHub Copilot CLI setup URL directly to the official CLI installation instructions (`https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli`).
* Enhanced reviewer certification notes and onboarding diagnostics for clean-machine testing.

---

## 4. Certification Notes for Microsoft App Reviewers

```
Notes for Certification (Package 1.0.3.0):

Resolution for Recent Certification Feedback:
In the previous review, the reviewer reported 'No supported providers detected' after downloading the Antigravity desktop application.

Important Clarification on Antigravity:
- AIQuotaBar integrates with the official Antigravity CLI ('agy'), not the standalone Antigravity desktop IDE application.
- The Antigravity desktop application and the Antigravity CLI are separate installations.
- In this build, AIQuotaBar's Setup Guide button links directly to the official Antigravity CLI Installation & Auth page:
  https://antigravity.google/docs/cli/install/
- On Windows, the official installer places agy under:
  %LOCALAPPDATA%\agy\bin\agy.exe
- The official Windows installation command is:
  irm https://antigravity.google/cli/install.ps1 | iex
- After installing and authenticating the CLI, return to AIQuotaBar Settings and click 'Rescan providers' to immediately detect the installation.

Equivalent Exact CLI Guidance for All Providers:
AIQuotaBar provides equivalent explicit CLI requirements and direct official installation links for all 5 supported providers:
1. OpenAI Codex: requires official Codex CLI (https://developers.openai.com/codex/cli/)
2. Google Antigravity: requires official Antigravity CLI 'agy' (https://antigravity.google/docs/cli/install/)
3. Claude Code: requires official Claude Code CLI (https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview; Claude Desktop alone is not supported)
4. Grok Build: requires official Grok CLI (https://docs.x.ai/build/overview; browser-only accounts are not supported)
5. GitHub Copilot: requires official GitHub Copilot CLI 'copilot.exe' (https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli; VS Code extension alone is not supported)

How to Test Window Restore:
1. Launch AIQuotaBar from the Start Menu.
2. Click the minus button (-) on the title bar to minimize/hide the widget to the Windows notification area (system tray).
3. Confirm the main window is hidden.
4. Right-click the AIQuotaBar system tray icon (or left-click / double-click it).
5. Click 'Open AIQuotaBar'.
6. The main window will immediately become visible and activate in the foreground.

Note on Clean-Machine Onboarding:
AIQuotaBar reads quota and rate-limit statistics locally via inter-process communication with supported developer tools installed and authenticated on the user's computer. AIQuotaBar does not bundle third-party accounts or cloud backends.

On a clean test machine with no third-party AI provider CLIs installed, the expected and correct behavior is the clean onboarding state:
- Header: 'No supported providers detected'
- Description: 'Install or sign in to a supported provider, then rescan in Settings.'
- Button: 'Set up providers' (opens the Settings window showing all 5 provider setup cards with direct CLI installation links).

This onboarding state represents full, expected functionality on a device without pre-installed AI developer tools.
```
