# AIQuotaBar — Engineering Guidance & Architecture Guardrails

This document establishes the permanent engineering rules, architectural boundaries, and quality standards for **AIQuotaBar**. All implementation agents operating in this repository must adhere strictly to these principles.

---

## 1. Product Principles

AIQuotaBar is a lightweight, Windows-first, local-first desktop widget designed to monitor AI subscription quotas and rate limits across providers.

### Core Promise
> **Local first. No telemetry. No cloud backend. Provider credentials stay with the provider whenever technically possible.**

* **Windows-First:** Built targeting Windows 11 using .NET 10 and WPF.
* **Local-First & Private:** All communication is direct local IPC with installed provider tools. No remote telemetry, analytics, tracking, or cloud services.
* **Lightweight & Portable:** Fast startup, minimal resource footprint, self-contained single-file deployment without requiring runtime installs.

---

## 2. Architecture Boundaries

The codebase is organized into strict, decoupled layers:

```
             [ AIQuotaBar.App (WPF / Presentation / Tray / Lifecycle) ]
                                      │
                                      ▼
                     [ AIQuotaBar.Core (Domain Abstractions) ]
                                      ▲
                         ┌────────────┴────────────┐
                         │                         │
[ AIQuotaBar.Providers.Codex ]          [ AIQuotaBar.Providers.Antigravity ]
```

### Invariant Rules
1. **Core Independence:** `AIQuotaBar.Core` must remain completely agnostic of WPF, Windows Forms, or any UI framework. It targets `net10.0` and defines models (`ProviderSnapshot`, `QuotaWindow`, `ProviderStatus`) and interfaces (`IUsageProvider`).
2. **Provider Independence:** `AIQuotaBar.Providers.<Provider>` (e.g., `AIQuotaBar.Providers.Codex`) targets `net10.0` and references only `AIQuotaBar.Core`. Providers must never reference WPF or UI layers.
3. **UI Decoupling:** `AIQuotaBar.App` consumes normalized `IUsageProvider`, `ProviderSnapshot`, and `QuotaWindow` models. **Provider-specific RPC DTOs and internal transport models must never leak into ViewModels or Views.**
4. **Zero Cross-Provider Leakage:** Providers must remain self-contained. Adding or modifying one provider must not affect others.

---

## 3. Credential & Privacy Rules

AIQuotaBar never manages, captures, stores, or transmits user authentication credentials.

* **Never Read Auth Files:** Never inspect or parse `.codex\auth.json` or credential stores of providers directly.
* **Never Copy or Persist Tokens:** Access tokens, refresh tokens, session cookies, and API keys must never be captured, copied, or written to disk.
* **Provider-Owned Authentication:** Provider authentication remains 100% owned by the provider tool (e.g. the official `codex` CLI via local stdio IPC).
* **Sanitize Errors & Logs:** Exception handlers must never expose sensitive paths, user emails, or tokens in UI status messages or test fixtures.
* **No Network Egress:** The application makes zero outbound HTTP/HTTPS requests on its own behalf.

---

## 4. Dependency Rules

Production dependencies must remain minimal:

* **Zero-Dependency Baseline:** Production libraries (`src/`) must avoid third-party NuGet packages whenever standard .NET Base Class Library (BCL) and Windows Desktop APIs suffice.
* **No Convenience Dependencies:** Do not add NuGet packages merely for convenience (e.g. third-party JSON parsers, MVVM frameworks, or logging libraries).
* **Justification Required:** Any new production dependency requires explicit architectural justification and approval.
* **Test Dependencies:** Test projects (`tests/`) are restricted to standard harnesses (`xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`).

---

## 5. Scope Discipline

Do not expand scope beyond approved milestones without architectural direction from the lead architect:

* **Current Provider Scope (v0.2):** OpenAI Codex and Google Antigravity. Do not add Claude, Gemini, Cursor, Copilot, OpenRouter, or other providers until architecturally specified.
* **Feature Boundaries:** Do not implement cloud synchronization, account systems, automatic updaters, MSI/MSIX installers, Windows shell/taskbar injections, or telemetry.
* **Retained v0.1 Daily-Driver Features:** System tray integration (`NotifyIcon`), compact/expanded mode toggle, always-on-top toggle, start-with-Windows toggle, and window position persistence in `%LOCALAPPDATA%\AIQuotaBar\settings.json` are approved parts of v0.1.

---

## 6. Testing & Validation

Before declaring any implementation task complete:

1. **Build Verification:** Run `dotnet build AIQuotaBar.slnx` — must produce `0 Warning(s)` and `0 Error(s)`.
2. **Test Execution:** Run `dotnet test AIQuotaBar.slnx` — 100% of tests must pass.
3. **Offline Testability:** All provider parsing, normalization, timeout handling, and RPC serialization logic must be testable offline using JSON fixtures without requiring a live user login or active process.
4. **Process Safety:** Process runners must guarantee termination of child processes on cancellation, error, or timeout.

---

## 7. Build & Distribution

* **Target:** Windows x64 self-contained portable executable (`net10.0-windows`).
* **Trimming:** Trimming is explicitly disabled (`PublishTrimmed=false`) to ensure WPF rendering and dynamic dispatch reliability.
* **Build Script:** Use `scripts/build-portable.ps1` to produce release-ready binaries in `artifacts/portable/win-x64/`.

---

## 8. Git Safety

* Never force push to `main` or public branches.
* Never commit secrets, tokens, or credential files.
* Never commit build outputs, temporary files, or portable binaries (`bin/`, `obj/`, `artifacts/`).
* Never change repository visibility or publish public GitHub releases without explicit instruction.
