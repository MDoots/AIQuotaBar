# AIQuotaBar Engineering Rule

When working within this repository:

1. **Read `AGENTS.md`** at the repository root and treat all rules and principles within it as authoritative.
2. **Preserve Architectural Boundaries:** Keep `AIQuotaBar.Core` UI-agnostic, keep providers decoupled, and consume only normalized domain models in `AIQuotaBar.App`.
3. **Strict Credential Safety:** Never read `.codex\auth.json` or persist/expose auth tokens.
4. **Scope Discipline:** Preserve the five existing provider integrations and the separate portable/MSIX release workflows. No telemetry, cloud backend or credential-file access. Follow the user's approved bounded implementation scope.
5. **Validation Rule:** Verify `dotnet build AIQuotaBar.slnf -c Release` with 0 warnings/errors and `dotnet test AIQuotaBar.slnf -c Release` with all offline tests passing. WAP packaging and installed-artifact/WACK validation are separate release gates; CLI test success is not certification evidence.
