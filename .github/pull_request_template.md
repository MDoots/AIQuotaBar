## Description

A clear and concise description of the changes proposed in this pull request.

---

## Architectural & Privacy Checklist

Please confirm your PR complies with all core architectural guardrails before submitting:

- [ ] **Architecture Boundaries:** `AIQuotaBar.Core` remains UI-agnostic; `AIQuotaBar.App` consumes only normalized models (`IUsageProvider`, `ProviderSnapshot`, `QuotaWindow`).
- [ ] **No Provider DTO Leaks:** Provider-specific RPC/JSON models are isolated within the provider assembly and not exposed to ViewModels/Views.
- [ ] **Zero Production Third-Party Dependencies:** No new NuGet packages have been added to `src/` without prior architectural approval.
- [ ] **Privacy & Credential Safety:** No tokens, API keys, personal emails, or credentials are read, stored, logged, or included in test fixtures.
- [ ] **Offline Tests:** All new logic and providers include offline unit tests with JSON fixtures.
- [ ] **Process Safety:** Process runners enforce bounded timeouts and guarantee child process tree cleanup on cancellation or exit.
- [ ] **Verification:**
  - `dotnet build AIQuotaBar.slnx -c Release` passes with 0 warnings and 0 errors.
  - `dotnet test AIQuotaBar.slnx -c Release` passes 100% of unit tests.
