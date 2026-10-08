# AIQuotaBar project context

AIQuotaBar is a local-first Windows 11 x64 desktop widget for developers using
multiple AI coding assistants. It makes exposed subscription quota, rate-limit
windows and reset countdowns visible without repeatedly opening provider tools.
Provider-owned CLIs handle authentication and their own service communication.

## User journeys and implemented features

1. Launch the portable executable or Store app. Local discovery identifies
   supported installed CLIs; a clean machine receives an empty state and Settings guidance.
2. View finite remaining quotas and countdowns in expanded cards or a compact bar;
   choose provider/window visibility, width and always-on-top behavior.
3. Float the widget or dock it to the top/bottom screen edge, with optional auto-hide
   and horizontal alignment. Use the tray to hide, restore, refresh or exit.
4. Refresh manually or let provider timers refresh. Transient failures retain
   last-known-good values as stale; cancellation does not create false low-quota alerts.
5. Configure notifications and startup, rescan tools, and recover after sleep or a
   disconnected monitor. Preferences persist locally between app launches.

The five integrations are Codex, Antigravity, Claude Code, Grok Build and GitHub
Copilot. Claude exposes authentication status only in this implementation. Missing
tools, unsupported interfaces and unknown quota are legitimate states, never full
or exhausted capacity. See [README](../../README.md) for provider prerequisites and controls.

## Stack and stage

- C#/.NET 10; WPF presentation with Windows Desktop APIs and a WinForms tray icon.
- UI-independent Core and five provider assemblies using local CLI/stdio interfaces.
- BCL production dependencies, with one isolated exception:
  `GitHub.Copilot.SDK` `1.0.13-preview.2` in the Copilot provider.
- xUnit offline tests; PowerShell build/probe scripts; Windows GitHub Actions CI.
- Self-contained portable win-x64 executable and separate WAP/MSIX Store packaging.
- No database, ORM, migration system, web frontend, backend or hosted app service.

The project has a released 1.0.5 maintenance version. Further work is primarily
validation, compatibility and reliability. [STATUS](STATUS.md) records current
evidence; code presence alone does not establish runtime acceptance.

## Scope and constraints

Preserve local-only app operation, zero telemetry, provider-owned authentication,
bounded non-interactive processes, offline tests and strict layer separation.
Never inspect credential files, add a network backend, capture interactive usage,
or introduce another production dependency without architectural authorization.
The diagnostic ETW tool has a separate developer-only dependency and is not shipped.

WPF trimming remains disabled. Store compilation, installed-package testing and
Microsoft certification are separate gates. Linux agents cannot validate the full
Windows product. The current product targets Windows 11 x64; other platforms,
remote account aggregation and infrastructure migrations are outside agreed scope.

No new feature programme is inferred from this audit. Proposed or blocked work in
[ROADMAP](ROADMAP.md) must retain its evidence and approval boundaries.
