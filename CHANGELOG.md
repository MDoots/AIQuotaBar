# Changelog

All notable changes to **AIQuotaBar** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

* **Provider Expansion Pack:** Added official local integrations for Claude Code, Grok Build, and GitHub Copilot, expanding the supported provider count from two to five.
* **Adaptive Resizable Floating Widget:** Responsive width adjustment with both Expanded and Compact layout modes.
* **Soft Docked Mode:** Optional Top or Bottom edge screen docking with auto-hide and horizontal anchoring support.
* **Provider & Row Visibility Controls:** Customize which providers and individual quota rows are displayed in the widget and tray.
* **Quota-Aware System Tray Health:** Color-coded tray icon reflecting overall quota health, lowest remaining capacity, and quick-status tooltips.
* **Low & Exhausted Quota Notifications:** Configurable Windows desktop notifications when quota drops below warning or exhaustion thresholds, with automatic baseline re-arming.
* **Provider Discovery & Onboarding:** Automatic local executable discovery and onboarding status in Settings with one-click refresh.
* **Windows Sleep/Resume Recovery:** Coordinated background refresh recovery after PC wake with native power event handling.
* **Live Provider Test Harness:** Opt-in developer script (`scripts/test-live-providers.ps1`) for non-destructive live verification.

### Changed

* **Plan-Agnostic Quota Semantics:** Support displaying legitimate finite quotas across all account tiers (Free, trial, paid, promotional, or enterprise).
* **Last-Known-Good Quota Resilience:** Retain and visually flag last successful quota rows during transient network/process timeouts or errors without clearing data.
* **Control-Flow Cancellation Safety:** Cancelled refresh attempts preserve valid existing quota without marking rows stale or generating false alerts.
* **Centralized Provider Catalog:** Unified provider descriptors, CLI locators, metadata, and brand styling in `ProviderCatalog`.
* **Dynamic Work-Area Sizing:** Viewport-aware vertical constraint handling and first-run centring across expanding provider lists.

---

## [0.2.0] - 2026-08-27

Initial public release of AIQuotaBar, introducing local-first multi-provider quota tracking for OpenAI Codex and Google Antigravity.

### Added

* **OpenAI Codex Provider:** Live subscription quota and rate-limit tracking via local JSON-RPC over stdio with the official `codex app-server`.
* **Google Antigravity Provider:** Quota monitoring via local process execution of the official `agy` CLI (`agy -p "/usage" --output-format json`).
* **Multi-Provider WPF Widget:** Windows 11 desktop widget supporting simultaneous monitoring of multiple AI subscription quotas with per-provider section isolation.
* **Semantic Quota Progress Visualization:** Color-coded remaining quota bars (Teal/Green for healthy, Amber for warning under 20%, Red for nearly exhausted under 5%) with reset-time indicators.
* **Compact & Expanded Modes:** Toggle between full multi-window quota details and a minimal single-line status bar.
* **Always on Top:** Pin widget above active editor and coding workspace windows.
* **System Tray Integration:** Minimize to Windows system tray (`NotifyIcon`) with interactive context menu, show/hide toggle, and exit commands.
* **Settings Persistence:** Automatic persistence of window coordinates, display mode, and always-on-top state in `%LOCALAPPDATA%\AIQuotaBar\settings.json`.
* **Start with Windows:** Optional startup registration via Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
* **Self-Contained Portable Build:** Single-file Windows x64 executable requiring no pre-installed .NET runtime.
* **Local-First & Zero-Telemetry Architecture:** Provider-owned authentication with zero telemetry, zero analytics, zero cloud backend, and zero third-party production dependencies.
