# Contributing to AIQuotaBar

Thank you for your interest in contributing to **AIQuotaBar**!

AIQuotaBar is a lightweight, local-first Windows 11 desktop widget designed to monitor AI subscription quotas and rate limits across providers. We welcome bug reports, suggestions, and pull requests that align with our core principles.

---

## 1. Core Principles

Before proposing changes, please keep our core architectural rules in mind:

1. **Local-First & Private:** AIQuotaBar makes zero outbound network requests of its own, sends no telemetry or analytics, and operates without any cloud backend.
2. **Provider-Owned Authentication:** We never capture, parse, store, or transmit user credentials. Never inspect credential stores (e.g. `.codex\auth.json`) directly. All usage is queried through local official CLI / app-server processes owned by each provider.
3. **Zero Production Third-Party Dependencies:** Production projects in `src/` must rely strictly on the standard .NET Base Class Library (BCL) and Windows Desktop APIs. Do not add NuGet dependencies to `src/` without prior architectural approval.
4. **Offline Testability:** All provider parsing, normalization, timeout handling, and transport layers must be unit testable offline using JSON fixtures without requiring a live user login or active network connection.

---

## 2. Architecture & Layer Boundaries

The repository is structured into strict, decoupled layers:

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

* **`AIQuotaBar.Core`:** Defines domain abstractions (`IUsageProvider`, `ProviderSnapshot`, `QuotaWindow`, `ProviderStatus`). Targets `net10.0` and must remain completely agnostic of WPF, WinForms, or UI frameworks.
* **`AIQuotaBar.Providers.<Name>`:** Implements `IUsageProvider` for a specific provider. References only `AIQuotaBar.Core`. Internal RPC DTOs and transport protocols must remain private to the provider assembly.
* **`AIQuotaBar.App`:** WPF application layer consuming only normalized domain models. Provider-specific transport DTOs must never leak into ViewModels or Views.

---

## 3. Adding a New Provider

If you are interested in adding support for another AI provider:

1. **Open an Issue First:** Discuss the provider and its official local interface before writing code.
2. **Implement `IUsageProvider`:** Create a new project `src/AIQuotaBar.Providers.<Name>` targeting `net10.0` and referencing `AIQuotaBar.Core`.
3. **Process Safety:** Ensure process runners use `ProcessStartInfo` with redirected streams, enforce bounded timeouts, and guarantee termination of child process trees on cancellation or exit.
4. **Provide Unit Tests:** Add unit tests in `tests/AIQuotaBar.Providers.<Name>.Tests` covering protocol parsing, fixture normalization, timeout handling, and failure modes.

---

## 4. Development Workflow

### Prerequisites
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
* Windows 11 x64 (for running the WPF presentation layer)

### Build & Test Commands

* **Build Solution:**
  ```powershell
  dotnet build AIQuotaBar.slnx -c Release
  ```
  *All builds must complete with `0 Warning(s)` and `0 Error(s)`.*

* **Run Test Suite:**
  ```powershell
  dotnet test AIQuotaBar.slnx -c Release
  ```
  *100% of tests must pass offline.*

* **Build Portable Single-File Executable:**
  ```powershell
  .\scripts\build-portable.ps1 -Configuration Release -Runtime win-x64
  ```

---

## 5. Pull Request Guidelines

1. **Focus:** Keep PRs focused on a single feature or bug fix. Avoid combining unrelated changes.
2. **No Secret Leaks:** Ensure test fixtures and logs contain zero personal data, tokens, emails, or file paths.
3. **Verification:** Confirm `dotnet build` and `dotnet test` pass cleanly before submitting.
4. **Code Style:** Follow standard C# coding conventions and existing repository patterns (file-scoped namespaces, nullable reference types enabled, explicit null checks).
