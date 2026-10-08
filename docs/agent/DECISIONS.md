# Decision log

This is a short durable log, not a transcript. Entries reconstructed from repository
evidence identify that evidence; unknown historical rationale is not invented.

## D-001 — Local-first and provider-owned authentication

Recorded 8 October 2026; existing constraint, present in the initial 27 August
release and [AGENTS](../../AGENTS.md)/[PRIVACY](../../PRIVACY.md).

Use official local CLI/stdio interfaces; no app network backend, telemetry or direct
credential-store access. This preserves the stated privacy model and provider ownership.
Provider CLIs can use their own networks. No historical comparison with a hosted
alternative is recorded. Tests must remain offline and logs sanitized.

## D-002 — Core/provider/presentation separation and dependency limit

Recorded 8 October 2026; pre-existing AGENTS and project-reference constraints.

Core is UI-independent; each provider references only Core; App binds normalized
models. Production uses BCL/Windows APIs except the isolated Copilot SDK. New
transport details stay internal; existing public DTOs remain compatibility debt.
This supports isolated provider changes/offline testing. Earlier alternatives and
decision date are not recorded. Developer-only ETW tooling is outside shipped code.

## D-003 — Distinct portable and Store release gates; no WPF trimming

Existing 29 August 2026 packaging milestone, source/scripts and release checklist.

Use a self-contained single-file portable executable and separate WAP/MSIX package.
`PublishTrimmed=false` preserves XAML reflection/bindings. Windows CLI CI is
authoritative for build/tests; UAP-dependent packaging can skip on hosted runners.
Compilation/identity checks do not establish install/upgrade, WACK or certification.
Exact-byte hashes and separate evidence are required; historical warnings do not
transfer to new builds. No unified deployment alternative is documented.

## D-004 — Last-known-good data with truthful account/status boundaries

Existing release behavior, expanded/hardened by 1.0.4 on 19 September 2026.

Keep successful quota through transient failures and mark it stale; cancellation
does not degrade valid data or alert. Clear old quota after observed account scope
changes. Unknown values cannot imply full/exhausted quota. This avoids misleading
numbers while maintaining layout continuity; account data is in memory, not history.

## D-005 — Claude authentication only until an official quota route is verified

Recorded in 19 September 2026 1.0.4 source/changelog and current README.

Use `claude auth status --json`; decline interactive usage capture and unsupported
automatic quota. Show provider-owned usage guidance. This preserves non-interactive
process/credential boundaries at the cost of automatic Claude quota. Do not turn the
missing capability into zero/full quota or work around it through credential parsing.

## D-006 — Verify process-local Codex plugins safeguard before quota polling

2 October 2026, [PR #11](https://github.com/MDoots/AIQuotaBar/pull/11) and
[corrective investigation](../codex-staging-corrective-investigation.md).

Launch the helper with `--disable plugins app-server` and verify the official
effective configuration before normal account/quota reads. Contain the selected
executable for the provider instance if the safeguard cannot be verified; retain
appropriate stale UI and do not classify transient failures as incompatibility.

The rejected alternative paused every Codex query and failed feature-preserving
acceptance. A prior staging-folder writer remained unattributed. The accepted
0.159.2/profile evidence supports the checked safeguard, not universal CLI safety.
Do not change global plugin configuration or delete shared staging folders.

## D-007 — GitHub default branch as durable agent context

8 October 2026, requested repository documentation/synchronization policy.

Use merged `main`, AGENTS and six small agent documents for project context, current
state, roadmap, decisions and rolling handover. Preserve existing detailed release
evidence through links. Branch documents are provisional and must reconcile newer
state before merging. This reduces reliance on private sessions without creating
another status backend. The existing global routing policy is preserved as a
repository snapshot; newer applicable human/global instructions take precedence.

The existing remote is public. On 8 October the owner explicitly approved pushing
the documentation branch/opening a PR there. Identity and visibility remain unchanged;
no new repository, protected/default branch merge, deployment or release was authorized.
