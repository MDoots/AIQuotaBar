# AIQuotaBar — Microsoft Store Listing & Certification Guide (v1.0)

**Product Name:** AIQuotaBar  
**Product ID:** 9NTTSH588BQ9  
**Publisher:** AGIFutures (CN=63F366FC-16FC-4C0B-99DF-7E5B40742F24)<br />
**Package Version:** 1.0.5.0<br />
**Public App Version:** 1.0.5

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

## 3. Release Notes (1.0.5)

* Restored live Codex quota monitoring with a verified helper configuration that avoids unnecessary marketplace startup work.
* Preserved automatic/manual refresh, reset countdowns and last-known-good quota through temporary failures.
* Normal Codex plugin settings remain unchanged. If safe helper configuration cannot be verified, Codex displays a clear compatibility-paused status.

### Restricted capability explanation

AIQuotaBar is a WPF desktop widget packaged as MSIX. It requires runFullTrust for its desktop window, notification-area icon and bounded child processes that query official locally installed provider tools. Child processes use redirected local stdio; official tools own authentication and any service connections. AIQuotaBar does not request administrator elevation, install services or drivers, access credential files, or collect telemetry. Provider tools are installed and authenticated separately by the user. Diagnostic tracing tools are developer-only and are not included in this package.

## 4. Certification Notes for Microsoft App Reviewers

Notes for certification (package 1.0.5.0):

This update restores Codex quota polling with a process-local plugins feature override and official effective-configuration verification. It does not change the user's normal Codex configuration, plugins or authentication. Live validation used codex-cli 0.159.2 with a signed-in profile and three configured marketplaces. Ten minute-spaced production-provider quota reads succeeded with conclusive filesystem attribution and no helper staging activity or new staging folders. A separate fresh widget and portable restart/manual-refresh/tray-exit check were observed by the user.

Verification journey:
1. On Windows 11 with no supported provider CLI, launch the app and use the no-provider setup guidance in Settings. A separately installed .NET runtime is not required.
2. Install and authenticate an official provider CLI through its own supported interface. For Codex, use a current native official CLI supporting app-server config/read and account/rateLimits/read. AIQuotaBar verifies features.plugins=false for its isolated quota helper before reading quota.
3. Use Settings > Rescan providers. The scan shows progress and a persistent completion result. A detected CLI is distinct from supported quota access. Missing authentication, unsupported helper configuration or unavailable quota is reported explicitly; no percentage is invented.
4. Where finite quota is exposed, check automatic and manual refresh, reset countdowns, and tray health. Temporary failures retain previous quota with a stale indicator.
5. Exit through the tray menu, then restart. Authentication remains provider-owned; AIQuotaBar never requests or reads credentials.

Provider prerequisites: official Codex CLI; official Antigravity agy CLI (the desktop IDE alone is insufficient); official Claude Code CLI (authentication status only, with quota viewed in Claude Code); official Grok CLI; official GitHub Copilot CLI. No provider/account tier is guaranteed to expose finite quota.

AIQuotaBar has no network client, telemetry or remote backend. Provider tools may contact their own services. Product support and privacy URLs remain unchanged.

Fresh installed-package, clean-machine/upgrade and Windows App Certification Kit checks could not run because the disposable Windows VM aborted twice before guest preflight. No 1.0.5 WACK pass is claimed; earlier 1.0.4 reports are not this package's result. The exact package hashes and limitations are recorded in docs/release-acceptance-1.0.5.md. Microsoft certification remains a separate decision. The submission is configured to publish automatically as soon as Microsoft certification passes, with explicit user approval.
