# AIQuotaBar Engineering Rule

When working within this repository:

1. **Read `AGENTS.md`** at the repository root and treat all rules and principles within it as authoritative.
2. **Preserve Architectural Boundaries:** Keep `AIQuotaBar.Core` UI-agnostic, keep providers decoupled, and consume only normalized domain models in `AIQuotaBar.App`.
3. **Strict Credential Safety:** Never read `.codex\auth.json` or persist/expose auth tokens.
4. **Scope Discipline:** Strictly adhere to the approved v0.2 boundaries (OpenAI Codex and Google Antigravity, no telemetry, no cloud backend, no installers/updaters).
5. **Validation Rule:** Always verify that `dotnet build AIQuotaBar.slnx` and `dotnet test AIQuotaBar.slnx` pass with 0 errors and 0 warnings before completing any implementation.
