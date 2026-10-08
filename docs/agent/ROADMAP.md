# Roadmap and verification work

Audited 8 October 2026. This records existing implementation/evidence and bounded
follow-up work; it does not approve new features. See [STATUS](STATUS.md) for latest checks.
P1 = next reliability/release work, P2 = subsequent verification, P3 = optional/deferred.

## Completed and verified

| Area | Evidence and limits |
| --- | --- |
| Core/provider normalization, widget/settings/layout/tray helpers and resilience | Offline suites cover fixtures, cancellation, retained quota, account transitions and Windows helper behavior. Check STATUS for this session's failures and final results; tests are not full UI acceptance. |
| 1.0.5 Codex functional repair | Recorded ten fresh actual-provider reads on CLI 0.159.2, verified process-local plugin safeguard, no attributed staging activity and source/candidate manual acceptance. Coverage is version/profile-specific. |
| GitHub v1.0.5 distribution | Release metadata checked 8 October; published 2 October from `d4dde6e`. Separate archive/executable hashes and smoke limits are recorded in [release evidence](../release-acceptance-1.0.5.md). |
| Provider onboarding and portable/MSIX separation | Implemented in merged release history, fixture/helper tests and prior release acceptance; current exact-byte Store validation remains separate. |

## Implemented but requiring verification

| ID | Work | Priority / dependencies | Acceptance / current status |
| --- | --- | --- | --- |
| AQ-002 | Complete exact 1.0.5 MSIX installed-package, upgrade/settings-retention and WACK checks | P1; functioning disposable Windows environment and authorized install/signing workflow | Record hashes, clean-machine launch, real predecessor upgrade and full WACK outcome. Blocked in release work by aborted VM; earlier WACK WARNING does not transfer. |
| AQ-004 | Complete Windows runtime matrix | P2; controlled Windows/DPI/multi-monitor environment | Record 100–250% scaling, floating/docked auto-hide, monitor removal, sleep/resume and repeated tray restoration on exact tested bytes. Existing unit coverage/partial manual checks are not the complete matrix. |
| AQ-005 | Revalidate supported provider versions and account capabilities | P2; official installed/authenticated tools; no prompt inference | Record tool version, normalized status, finite/unknown quota and cleanup. Codex safeguard must remain fail-closed; broader plugin workflows and desktop-closed operation remain unverified. Do not generalize 0.159.2 evidence. |

## In progress

| ID | Work | Priority / dependencies | Acceptance / current status |
| --- | --- | --- | --- |
| AQ-DOC-01 | Durable agent context and GitHub synchronization | P1; repository publishing decision | Six entry documents, preserved instructions/routing, accurate setup, focused commit and PR. This task branch is provisional until merged with authorization. |
| AQ-003 | Reconcile 1.0.5 Store certification/publication status | P1; read access to Partner Center/current Store evidence | Update release record and STATUS with observed version/status/date. Last recorded 2 October: certification pending, automatic publication enabled. No current Store check performed in this audit. |

AQ-003 is an outstanding release follow-up, not evidence that an agent is currently running it.
No application implementation task is active in the audited checkout.

## Planned verification

| ID | Work | Priority / dependencies | Acceptance / current status |
| --- | --- | --- | --- |
| AQ-006 | Verify the documented portable test subset in an isolated Linux/cloud clone | P2; .NET 10 SDK and permitted NuGet restore | Record clone/revision, restore/build and six-project test results; preserve Windows full-suite gate. Not run; no cloud infrastructure change approved. |

## Known defects and technical debt

| ID | Work | Priority / dependencies | Acceptance / current status |
| --- | --- | --- | --- |
| AQ-001 | Investigate process-test fixture handoff and timing sensitivity | P1; bounded test-only investigation first | `CodexTimeoutKillsOwnedChild` baseline hit IOException reading a PID file; fixture publishes asynchronously and reader checks only existence. Cause is a hypothesis until reproduced. Preserve child cleanup assertions; repeat focused checks and full suite without weakening safeguards. Earlier release CI also recorded a Grok elapsed-time failure under load. Open; no fix in this docs task. |
| AQ-007 | Reduce existing public provider protocol DTO exposure if needed | P3; approved compatibility review | New transport stays internal, UI binds Core only; any later visibility change accounts for consumers/tests. Existing AGENTS records this debt, but no refactor is approved. |

## Deferred or optional

| ID | Work | Priority / dependencies | Acceptance / current status |
| --- | --- | --- | --- |
| AQ-008 | Automatic Claude quota through a safe official interface | P3; verified non-interactive official quota route | Offline fixtures, authentication/cancellation/cleanup checks and truthful unknown status before live acceptance. Deferred; auth-only behavior is intentional, never read credential stores or capture interactive usage. |
| AQ-009 | Portable Authenticode signing | P3; owner-approved provider, identity and budget | Sign/verify exact distributed bytes and update checksum/evidence. Existing [signing options](../code-signing.md) and workflow placeholder remain proposals; no service configured or spending authorized here. |

Preserve historical [CHANGELOG](../../CHANGELOG.md), release records and the rejected
[containment candidate](../codex-1.0.5-containment-record.md). Its paused Codex behavior
is superseded history, not a current roadmap target. The unrelated scheduled-task
popup was reported fixed by the owner; that report is not independent verification
or an approved app change.
