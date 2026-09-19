# AIQuotaBar — Microsoft Store Listing & Certification Guide (v1.0)

**Product Name:** AIQuotaBar  
**Product ID:** 9NTTSH588BQ9  
**Publisher:** AGIFutures (CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24)<br />
**Package Version:** 1.0.4.0<br />
**Public App Version:** 1.0.4

---

## 1. Store Metadata

### Short Description (up to 100 characters)
> Lightweight, private desktop widget monitoring AI subscription quotas and rate limits across tools.

### Full Description
AIQuotaBar is a lightweight, local-first Windows 11 desktop widget designed for developers and AI power users. It monitors your active AI subscription quotas, rate limits, and reset countdowns across your locally installed developer tools—all from a clean, unobtrusive bar.

### Supported Providers
* **OpenAI Codex:** Named quota pools and reset windows exposed by the official app-server.
* **Google Antigravity:** Gemini, Claude, and GPT model rate limits with countdown timers.
* **Claude Code:** Official authentication status; quota details are viewed in Claude Code's own usage surface until a supported non-interactive quota interface is available.
* **Grok Build:** Finite weekly or monthly quota windows when exposed by Grok Build.
* **GitHub Copilot:** Finite entitlements where the official CLI quota interface exposes them.

### Key Features
* **Adaptive Floating & Docked Modes:** Place the widget anywhere on your desktop, resize horizontally with responsive label scaling, or dock it magnetically to the top or bottom of your screen with auto-hide.
* **Compact & Minimal Views:** Switch effortlessly between an expanded multi-line overview and a compact single-line bar.
* **System Tray Health & Notifications:** Dynamic tray icon reflects overall quota health at a glance, with optional alerts when active quotas drop below 10%.
* **Resilient Offline Architecture:** Seamlessly recovers from Windows sleep and wake cycles while retaining last-known-good quota data through temporary connection pauses.
* **Local-First & Private:** Zero telemetry, zero analytics, no cloud backend, and no advertising. AIQuotaBar never reads or stores your passwords, API keys, or session tokens. All communication is direct local IPC with the official provider tools already installed on your PC.

### Requirements
AIQuotaBar displays live data only from supported official developer tools installed and authenticated locally. The Google Antigravity integration requires the official `agy` CLI; the Antigravity desktop application alone is not sufficient. Claude Code currently contributes official authentication status while quota details remain in Claude Code's own usage view. No provider or account tier is guaranteed to expose a finite quota. On a clean machine without developer tools installed, AIQuotaBar provides a guided onboarding experience in Settings.

### Planned Screenshots
1. Floating mode (`docs/images/app-preview.png`)
2. Docked mode (`docs/images/app-docked.png`)

---

## 2. Store URLs

* **Support URL:** `https://github.com/MDoots/AIQuotaBar/issues`
* **Privacy Policy URL:** `https://github.com/MDoots/AIQuotaBar/blob/main/PRIVACY.md`
* **Repository / Homepage:** `https://github.com/MDoots/AIQuotaBar`

---

## 3. Release Notes (1.0.4)

* Settings now shows scan progress and a completion summary on every provider rescan.
* Improved Codex launch compatibility, quota-pool handling and recovery from temporary failures.
* Improved compact sizing, docking, scaling declarations and background process handling.
* Clarified official CLI prerequisites and provider limitations, including Claude Code authentication-only status.

### Restricted capability explanation

AIQuotaBar is a WPF desktop widget packaged as MSIX. It requires runFullTrust for its desktop window, notification-area icon and bounded child processes that query official locally installed Codex, Antigravity, Claude Code, Grok Build and GitHub Copilot tools. Claude Code supplies authentication status only. Child processes use local redirected input/output; the official tools own authentication and any service connections. AIQuotaBar does not request administrator elevation, install services or drivers, access credential files, or collect telemetry. Provider tools are installed and authenticated separately by the user.

---

## 4. Certification Notes for Microsoft App Reviewers

```
Notes for Certification (package 1.0.4.0):

The previous review (09/02/2026, policy 10.1.2.10) reported a Codex app-server launch error and no visible Rescan providers response. The candidate uses Codex's documented default stdio launch command and now reports scan progress plus a persistent completion result, including when provider availability is unchanged. This records the candidate change and does not assert the historical cause of the earlier error.

Verification journey:
1. On a clean Windows 11 VM with no .NET Desktop Runtime, launch the portable build and confirm the no-provider onboarding state. The exact Store package provides the same setup guidance and rescan feedback.
2. Install and authenticate an official provider CLI using its own documentation. A detected executable is distinct from successful quota access.
3. With official Codex CLI 0.148.0 signed out, rescan and confirm the provider shows “Sign in required” with the safe message “Codex is not authenticated”. Repeat rescans while connected and disconnected; each completes visibly.
4. If a provider cannot provide quota, the app retains an unavailable/error state and does not invent a percentage.

Antigravity clarification: AIQuotaBar integrates with the official `agy` CLI, not the separate Antigravity desktop IDE. The Setup Guide links to https://antigravity.google/docs/cli/install/; installing the desktop application alone does not install `agy`.

CLI prerequisites:
- OpenAI Codex: official Codex CLI (https://developers.openai.com/codex/cli/).
- Google Antigravity: official `agy` CLI (https://antigravity.google/docs/cli/install/).
- Claude Code: official Claude Code CLI (https://docs.anthropic.com/en/docs/agents-and-tools/claude-code/overview); authentication status is supported, while quota remains in Claude Code's own usage view.
- Grok Build: official Grok CLI (https://docs.x.ai/build/overview).
- GitHub Copilot: official Copilot CLI (https://docs.github.com/en/copilot/how-tos/copilot-cli/set-up-copilot-cli/install-copilot-cli).

Provider CLIs own authentication and any service connections. AIQuotaBar does not read credential files, request passwords or tokens, bundle accounts, or provide a cloud backend. On a clean machine, “No supported providers detected” and the Settings setup cards are expected; finite quota availability depends on the provider and account.

The Windows App Certification Kit 10.0.28000.2705 result for this candidate was **WARNING**, not PASS: twelve required checks passed, DPI Awareness Validation warned, and the optional blocked-executables check failed. This does not establish Store certification.
```

Internal release record (not part of the reviewer copy): signed-out Codex, disconnected signed-out rescans and host healthy-quota/rescan/tray smoke passed. Temporary host and guest packages and certificates were removed. WACK completed with WARNING as stated above. See `docs/release-acceptance-1.0.4.md`; no Store certification outcome is implied.
