# Changelog

All notable changes to **AIQuotaBar** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [0.2.0] - Unreleased

This release candidate represents the initial public preview of AIQuotaBar, introducing multi-provider quota tracking for OpenAI Codex and Google Antigravity.

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
