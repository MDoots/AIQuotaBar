# Current project status

Updated: 8 October 2026. This documentation branch is provisional until merged.

## Position and provenance

- Stage: released Windows desktop application, 1.0.5 maintenance/verification.
- Last implementation milestone: functional Codex plugin-safeguarded quota polling,
  merged through [PR #11](https://github.com/MDoots/AIQuotaBar/pull/11) at `d4dde6e`.
- Audit base: `main` / `origin/main` at `45d850f`; fetch found zero commits ahead/behind.
- Task branch: `codex/agent-context-sync`. Obtain its documentation revision with
  `git log -1 --format="%h %s" -- docs/agent`; no self-referential commit hash is stored.
- Canonical repository: [MDoots/AIQuotaBar](https://github.com/MDoots/AIQuotaBar),
  **public**, default branch `main`. Working tree was clean at audit start; no
  pre-existing untracked work. Generated artifacts/settings/publish profiles remain ignored.
- GitHub v1.0.5 publication verified through release metadata. Store 1.0.5 certification
  pending/auto-publication enabled is the last **2 October** record, not a current observation.

## Evidence and current checks

- Implemented: five local provider integrations (Claude auth only), discovery,
  preferences, adaptive floating/compact/expanded/docked UI, tray alerts and recovery.
- Prior 1.0.5 evidence: 661 offline tests; ten actual Codex reads on CLI 0.159.2;
  user-confirmed source/portable display, manual refresh and tray exit. Read
  [release acceptance](../release-acceptance-1.0.5.md) for exact artifacts and limits.
- 8 October baseline `dotnet test AIQuotaBar.slnf`: **660 passed, 1 failed, 0 skipped**.
  `CodexTimeoutKillsOwnedChild` failed with file-sharing IOException while reading
  the fixture PID file. All other projects passed. Recorded as AQ-001; no code changed.
- 8 October Release build: **0 warnings, 0 errors**. Full Release suite
  (`dotnet test AIQuotaBar.slnf -c Release --no-restore`): **661 passed, 0 failed,
  0 skipped**. The baseline PID-file failure did not recur; AQ-001 remains unresolved.
- Documentation local links/paths, original AGENTS prefix, verbatim routing snapshot
  and ignore/template examples passed. Initial tracked-file obvious-secret scan found no matches;
  no credential stores or ignored private-file contents were inspected. Not a full security audit.

## Unverified and blocked

- AQ-002: exact 1.0.5 MSIX clean-machine/upgrade/WACK validation; prior disposable VM
  aborted. Historical 1.0.4 WACK WARNING is not current-package evidence.
- AQ-003: current Store certification/public rollout status; no Partner Center check today.
- AQ-004/005: full DPI/multi-monitor/sleep/tray matrix, broader Codex versions/plugin
  workflows and other currently authenticated provider capabilities.
- AQ-006: Linux/cloud portable subset execution. Full WPF/process/UI/package validation
  requires Windows; cloud clone/restore access alone does not establish readiness.
- Existing GitHub legacy protection endpoint returned "Branch not protected", but
  active rules for main prevent deletion and non-fast-forward updates. No required
  PR/check rule was exposed by the applicable-rules endpoint. Project rules still
  require authorization before merging; do not infer permission from GitHub settings.

## Active work and decisions

- AQ-DOC-01: audit, durable context documents and requested focused commit/PR.
  No feature implementation, deployment or infrastructure work is included.
- Publishing decision: owner explicitly approved pushing this documentation branch
  and opening a PR in the existing public repository on 8 October. Identity and
  visibility are preserved. No merge, release or deployment is authorized.
- No architecture/product decision is required for the documentation itself.

## Next work

1. AQ-001: investigate/reproduce fixture handoff and process-test timing reliability
   without weakening child-cleanup safeguards; today's baseline failure makes this the
   first engineering task. Recommended model: GPT-6.1 Sol Medium for a bounded test
   investigation; High if evidence exposes coupled runtime/process behavior.
2. AQ-002: restore a disposable Windows validation environment and complete the exact
   submitted 1.0.5 package gates, preserving the owner's host installation.
3. AQ-003: inspect current Store evidence and reconcile the release record/status.

Use [ROADMAP](ROADMAP.md) for priorities/dependencies and [HANDOVER](HANDOVER.md)
for this session's result. Reconcile newer `main` evidence before updating this snapshot.
