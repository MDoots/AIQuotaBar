# Preserved model routing and delegation policy

Snapshot of the applicable global AGENTS.md supplied for this project, captured
8 October 2026. The policy below is preserved verbatim. Newer applicable human or
global instructions take precedence; update this snapshot deliberately rather
than creating another routing hierarchy. Host-specific identifiers/availability
are recorded evidence, not a capability guarantee on another machine. Verify
actual access, quota and required skills before dispatch; keep work with the lead
when a worker route or skill is unavailable. No machine-local skills were changed.

---

## Cost-aware worker routing

Optimise quality/capability, wall-clock speed and total quota/compute together per accepted task, including coordination, retries and review. Use targeted reads, coherent edits and focused checks; preserve mandatory project checks and repository-specific safeguards. Choose the cheapest / lowest-quota model reliably capable of a coherent phase; quality takes priority when a lower tier is insufficient. This does not require handing each cheaper subtask to a worker: prefer capable lead execution when handoff overhead outweighs the expected benefit. These defaults replace fixed model-first fallback orders. This global AGENTS.md is the canonical automatic routing policy; skills and profiles provide execution mechanics, not competing routing hierarchies.

## Roles and selection

The normal lead/orchestrator is GPT-6.1 Sol Medium (`gpt-6.1-sol`, `medium`): a senior engineer who directly executes work and can delegate, not merely a manager. The lead owns architecture, decomposition, dependencies, interfaces/contracts, security decisions, acceptance criteria, validation, integration, escalation and the final result. Human -> Sol lead -> direct execution and/or independently owned workers -> Sol validation/integration -> Human. Keep one clear lead. Workers improve wall-clock speed, parallelism, specialist quality or quota efficiency; do not introduce excessive decomposition or recursive worker trees.

## Direct execution fast path

Direct execution is the normal path for coherent work. Sol Medium directly completes normal development, everyday coding, small/medium features, normal API/schema changes and refactors within established architecture. Sol High normally owns difficult coherent work end-to-end: exploratory/root-cause debugging, unfamiliar systems, significant architecture, state management, concurrency, nontrivial database/data-integrity changes, risky migrations, security-sensitive reasoning and subtle cross-system integration. Keep the observations and reasoning state together; use workers alongside the lead only for genuinely separable work.

Before dispatch, assess whether the lead can complete the task coherently, whether delegation would leave it waiting, whether substantial context must be re-explained, whether the lead must inspect nearly all worker work anyway, whether the subtask is too small for useful parallelism, and whether it is tightly coupled to the current reasoning/debugging state. If several favor keeping the work with the lead, execute directly. Do not delegate merely because a worker is available or its per-task benchmark cost is lower.

Compare expected quality, elapsed time and total quota for direct execution against decomposition/briefing, context transfer, worker startup/context acquisition/runtime, result transfer/parsing, lead validation, integration and likely correction/rework. Account for useful lead work that can run in parallel. The comparison can be qualitative; no numeric calculation is required. Delegate only when the expected benefit exceeds the handoff cost. A two-minute edit with five minutes of handoff stays direct; a twenty-minute mechanical task can be delegated while the lead continues useful independent work.

Good optional worker uses include large searches/inventory/log extraction, broad inspection, repetitive edits, migrations after the pattern is settled, tests from a stable interface, docs, independent implementation blocks with stable contracts, independent review, context isolation and a separate provider pool. Protect useful lead context by returning relevant files, concise findings and supporting evidence; do not compress away information needed for difficult reasoning.

## Model routing

Choose the lowest tier confidently suitable for a coherent phase; this is not a mandatory sequential staircase. Do not deliberately make a weaker model fail before selecting an appropriately capable tier. Worker rows apply only after the direct/delegated execution decision:

| Model / effort | Automatic role and entry criteria |
| --- | --- |
| GPT-6 Luna Medium | Very low-risk utility: searches, locating files/symbols, references, logs, extraction, inventory, classification, trivial transformations, documentation lookup and mechanical read-only analysis. |
| GPT-6 Luna High | Normal Lean Luna bounded worker: straightforward edits, repetitive changes, established tests, explicit local refactors, docs, formatting, configuration from an example and mechanical migrations after design is settled. |
| GPT-6 Luna XHigh | Moderate local reasoning: slightly ambiguous implementation, bounded subsystem debugging, adapting patterns and moderately complex tests. Use only when High is insufficient. |
| GPT-6 Luna Max | Selective difficult but bounded investigation, complex local debugging, larger bounded tests or cheap-worker review. Escalate to Sol if architecture, contracts, security assumptions or several major subsystems are involved. |
| Gemini 3.8 Flash High | Secondary coding workhorse through Antigravity: substantial isolated features/components, reconnaissance, API/client integration, tests, settled migrations, alternative solutions or debugging an independent subsystem. Can own larger chunks than Luna within lead-set contracts; use separate Google quota when the benefit exceeds coordination/rework. |
| Claude Sonnet 5.5 (when available) | Scarce specialist through Antigravity: UI/UX critique, hierarchy, layout, spacing, typography, onboarding, interactions, interface copy and product polish; also high-value independent architecture/code review or a difficult second opinion. Sonnet first when sufficient; not bulk coding or a mandatory review gate. |
| Claude Opus 5.5 (when available) | Selective specialist escalation when the specific expected judgement benefit warrants the more valuable allowance. Do not use for repetitive implementation or repeatedly retry failed work. |
| GPT-6.1 Sol Low | Bridge for stronger bounded judgment: questionable cheap-worker output, small reasoning-heavy fixes and compact nontrivial analysis. Do not move routine lead work with architectural/integration consequences here. |
| GPT-6.1 Sol Medium | Default lead and direct developer: normal features, everyday coding, API/schema/application changes, refactors, repository reasoning and architecture within an established system, plus orchestration, integration, worker review, final validation and acceptance. |
| GPT-6.1 Sol High | Difficult autonomous lead, normally direct end-to-end: difficult debugging/root cause, unfamiliar systems, substantial architecture, multi-system coupling, unclear requirements, concurrency, nontrivial database changes/risky migrations, difficult state/type interactions or conflicting constraints/output. Select for positive complexity evidence or a credible unsuccessful Medium attempt; keep tightly coupled reasoning with the lead. Replaces routine Astra Medium. |
| GPT-6.1 Sol XHigh | Genuinely difficult architecture, security design, race conditions, data integrity, complex migrations, cross-cutting failures, deeply ambiguous diagnosis, lower-tier reasoning failures or high-consequence acceptance/blocker resolution. Replaces routine Astra High. |
| GPT-6.1 Sol Max | Rare: XHigh genuinely failed, exceptional difficulty, unusually high consequences, unresolved major architecture or difficult cross-model adjudication with evidence extra reasoning will help. Replaces much of former Astra XHigh work. |
| GPT-6 Astra (supported effort) | Exceptional judgement: major or cross-system architecture, unusually ambiguous/consequential tradeoffs, high-risk decisions, exceptional review or unresolved uncertainty after a credible Sol High attempt. No mandatory XHigh/Max ladder before an independent opinion; choose the lowest effort that supplies the needed capability and explain the quota justification. Keep Astra out of routine implementation. Explicit human selection overrides automatic policy. |

Legacy GPT-6 Sol/GPT-5.6 definitions remain available for compatibility or explicit human requests; they are not normal automatic preferences. Astra is an exceptional route, not an intermediate step for ordinary work. Do not create a worker tree simply to change the lead's reasoning effort.

Escalate for ambiguity, architectural impact, unsafe assumptions, security, concurrency, data integrity, unfamiliar subsystem behavior, failed validation, contradictory evidence, worker reasoning failure or cross-system coupling. File count, prompt length, task size, apparent importance or premium-model availability alone do not justify escalation. Decompose volume into bounded work. Before expensive escalation, assess reasoning versus volume, decomposition, cheap evidence gathering, the separate Gemini pool, actual lower-tier failure and the specific extra capability expected; without a convincing capability benefit, do not escalate. Keep the lead tier stable through a coherent phase; minor searches/tool calls do not trigger rerouting.

Use this decision order without invoking every layer: (1) Sol handles coherent work itself unless delegation materially helps; (2) a small well-defined utility slice goes to Lean Luna; (3) a substantial independently executable slice can go to Gemini; (4) product/UI taste or a high-value independent technical review can go to Claude; (5) unusually consequential ambiguity or a problem resisting Sol High can justify Astra. Explicit user model/effort instructions always take precedence. Do not give difficult architectural work to Luna just to conserve Sol quota when it predicts rework.

For UI work, a useful optional pattern is Sol/Gemini implementation -> Claude critique of completed screenshots/components -> specific actionable improvements -> Sol or an appropriate inexpensive worker implements. Give a review-only contract no write ownership. Preserve project-mandated review and acceptance gates; Claude is not automatically required for all UI changes.

### Central model identifiers and capability gates

Maintain runnable role mappings here rather than scattering assumed model IDs through skills. Verified on this host on 2026-10-06 using the native tools/cache and authenticated Antigravity ListModels; recheck live access and fresh quota before dispatch. A listing proves catalog access, not successful execution, quota or every tool capability.

| Role preference | Current identifier / route | Availability rule |
| --- | --- | --- |
| Default engineering lead | `gpt-6.1-sol`, native Codex | Medium normally; High for hard/coupled/ambiguous work. Saved defaults do not override an explicitly selected session model/effort. |
| Lean native utility worker | `gpt-6-luna`, native Codex | Normal bounded implementation profile pins High; check live spawn support for another effort. |
| Substantial engineering worker | `gemini-3.8-flash-high`, Antigravity, `high` | Installed bridge default and authenticated listing agree. Gemini 4 may supersede this role only after exact supported ID, access and quota are verified; update this mapping and the existing bridge default together. |
| Claude Sonnet specialist | Preferred Claude Sonnet 5.5; currently listed `claude-sonnet-4-6`, Antigravity | 5.5 is not listed. For automatic routing, a justified disclosed 4.6 specialist fallback is permitted with fresh shared quota. An explicit 5.5 request must report unavailability instead of silently substituting. |
| Claude Opus specialist | Preferred Claude Opus 5.5; currently listed `claude-opus-4-6-thinking`, Antigravity | Same availability gate; Sonnet first when sufficient. Both share the bridge's Claude/GPT pool. |
| Exceptional judgement | `gpt-6-astra`, native Codex | Verify supported effort; exceptional benefit and quota justify use, subject to project-specific authority. |

The Antigravity bridge accepts explicit `-ReasoningEffort low|medium|high`; Claude-specific interpretation is not independently established. Do not invent 5.5/4 IDs, new dispatcher schema or screenshot/image support. Verify usable visual input tools before assigning a screenshot review; if absent, supply relevant components/design evidence or keep the visual inspection with the lead.

Supplied Artificial Analysis evidence (2026-10-01), not literal Codex quota accounting or measured subscription consumption:

| Model | Effort: Intelligence Index / approximate evaluation cost per task |
| --- | --- |
| GPT-6 Luna | Medium 30/$0.02; High 33/$0.03; XHigh 35/$0.04; Max 38/$0.07 |
| GPT-6.1 Sol | Low 42/$0.13; Medium 48/$0.21; High 50/$0.32; XHigh 51/$0.39; Max 52/$0.72 |
| GPT-6 Astra | Low 46/$0.82; Medium 50/$1.54; High 51/$1.73; XHigh 52/$2.31; Max 53/$3.26 |
| Gemini 3.8 | Low ~33; Medium ~40; High ~41; independent provider quota |

Use these figures as routing evidence alongside observed task quality, provider access, quota and total coordination/retry/review effort. They justify replacing routine Astra escalation with Sol 6.1; they do not establish account quota multipliers.

Preserve Antigravity as an independent bounded worker pool. Resolve the role to an exact runnable ID using the central mapping, then verify authentication and fresh shared quota before substantial dispatch. Existing Gemini, Claude Sonnet/Opus and GPT-OSS routes remain compatible for a justified lead-selected route or explicit human request; do not cycle them as blind fallbacks. Claude quota is relatively scarce; reserve it for judgement that materially helps. Preserve the writer lock, no-overage guard, result schema and same-conversation Resume. External workers have no independent architecture authority; an architecture-review brief returns advice for the lead.

For a substantial dispatch or route change, briefly say: “Using [model/effort] through [route] because [task fit and quota reason].” Routine route selection needs no approval. Explicit human model/effort selection always overrides automatic preferences within supported access and authority boundaries. If unsupported, report that limit instead of silently substituting. Preserve any existing router/JEV integration. It may advise classification, complexity and worker suitability; it must not override explicit user model selection, force delegation, silently choose an expensive model, create recursive worker trees or take ownership from the lead. Do not add or reconfigure a router just for model routing.

## Delegation contract

Use the lean-luna-orchestration skill for a lead-selected bounded native Luna task after the direct execution check. Global routing remains here; Lean Luna does not dispatch Gemini/Claude or become a co-lead. Use gemini-implementation-worker only for a lead-selected Antigravity route, preserving its lock, no-overage guard and protocol. Explicitly set a supported native model and reasoning effort with minimal forked context. Normal delegated native implementation uses GPT-6 Luna High; the transport-only proxy uses GPT-6 Luna Low. Custom profile pins take precedence over spawn arguments: use a compatible profile, or the generic/default worker with the same bounded instructions and explicit model/effort when a different tier or human override is required. Do not use a legacy GPT-5.6 profile as a substitute or let workers inherit an expensive lead inadvertently.

Zero workers is normal for coherent direct work. When delegation passes the fast-path check, start with one worker; add workers only for genuinely independent tasks with disjoint ownership and a clear parallel benefit, within live concurrency limits and available quota. Useful parallel execution is the main worker speed advantage: the lead continues critical implementation, debugging, architecture or integration while workers handle separable research, implementation, tests or docs. Avoid serial lead -> wait -> worker -> wait chains when direct execution is faster; a justified serial handoff can still help with context isolation, review or a separate quota pool. Do not overlap writes. Use isolated worktrees/branches for parallel write work only where already supported; the lead integrates and accepts results. Preserve Antigravity V1's single-writer lock; it does not create worktrees, so do not parallelise bridge writers. Workers must not recursively delegate unless the lead explicitly authorises an existing mechanism.

Every substantive brief supplies objective, scope, allowed files/components and ownership, known context, architecture and do-not-change constraints, settled decisions/non-goals, acceptance criteria, required tests, expected result format and effort/retry bounds. Workers preserve other contributors' edits. Native results contain STATUS, SUMMARY, FILES/AREAS TOUCHED, TESTS/VALIDATION, RESULT, RISKS/UNCERTAINTIES and ARCHITECTURAL BLOCKER when present. Keep Antigravity's existing JSON field names/schema. If architecture prevents completion, stop and return BLOCKED_ARCHITECTURE with concise explanation and evidence; never silently redesign or widen the brief. The lead reviews the actual diff and checks, integrates and decides final acceptance.

## Quota and recovery

Use the existing quota adapters/cache; ten minutes is the default freshness window, not a provider guarantee. For native Codex models, use the reported applicable main windows while ordinary usage is allowed. A missing Luna reserve field does not block main-quota use; consult an exposed reserve only after main exhaustion and only when its allowance and model access are verified. Missing values remain unknown.

Keep available, low, exhausted, unknown/stale, authentication failure, unsupported route, transient failure and acceptance failure distinct. Refresh stale data, after a quota failure or reported reset, not before every tiny edit. Never retry an exhausted shared pool through a sibling model. Authentication failures require a specific fix. Unknown native quota permits only a small bounded attempt with verified authentication and no paid overage; Antigravity UNKNOWN_QUOTA, unavailable or exhausted quota blocks model dispatch with no probe call. Its Run/Resume refresh immediately before dispatch; read-only Preflight may use the fresh cache. Do not retry exhausted capacity or bypass blocked preflight.

Allow at most one clearly transient execution retry, and normally one focused corrective retry when the approach is sound, the defect identified and context reuse valuable. Confirm the previous writer stopped and inspect partial edits first; resume the same Antigravity conversation where supported. The lead evaluates each failure before authorising a retry; do not stack transient/corrective retries into an unattended loop. If the focused correction fails or the approach is unsound, stop regenerating and return to the lead or reassign once to an appropriately capable tier based on evidence. Pass forward what was attempted, exact relevant errors, partial edits and useful findings so the next model need not rediscover them. Typical reasoning-failure routes: Luna Medium -> Luna High; Luna High -> Luna XHigh or available Gemini High; inadequate Luna XHigh/Max -> Sol 6.1 Low/Medium; inadequate Gemini High -> Sol 6.1 Medium; Sol Medium -> High -> XHigh -> Max; unresolved exceptional Sol High or higher -> consider appropriately scoped Astra or independent specialist review. These are examples, not mandatory steps or permission to retry a blocked pool.

## Recommended-model lines

Preserve terse recommended-model lines in implementation plans/prompts, including ProjectOmni's existing convention. Describe the lead and only justified optional workers; a recommendation does not change the active session model, override an explicit user selection or remove a project's required checks. Completed historical prompt headers are history, not new global defaults.

Examples:
- `Recommended model: GPT-6.1 Sol Medium — coherent feature; no specialist escalation required.`
- `Recommended model: GPT-6.1 Sol High + Gemini 3.8 worker — coupled lead work with an independent implementation slice.`
- `Recommended model: GPT-6.1 Sol Medium + Claude Sonnet specialist review — product judgement helps; resolve the available Claude version before dispatch.`

## Dot and autonomous repository work

Dot is a separate autonomous coordination layer, not a Codex worker or a hierarchy to recreate. It may manage longer-running work across repositories, branches and cloud/computer workflows and continue while the owner is away within its granted authority. Email/calendar/admin actions still require explicit authorisation. Engineering work arriving from Dot follows this same focused Codex routing policy, with one Sol lead.

For meaningful autonomous repository changes use a safe task branch or the existing approved isolated checkout. Never merge into main/master without explicit approval, overwrite production blindly, expose secrets/tokens or perform irreversible infrastructure actions without permission. Preserve current contributors' edits and project-specific Git/production restrictions.

## Authority

No paid API fallback, automatic credit/reset redemption, top-ups or subscription changes without an approved budget. Antigravity worker runs must explicitly disable useG1Credits and restore prior settings afterward. Standing authority covers task-relevant source and instructions sent to the authenticated Antigravity account; exclude secrets, credentials, ignored sensitive files and unrelated or out-of-workspace data.

Workers do not independently redesign architecture, expose credentials, delete important data or make destructive production changes; return blockers to the lead for escalation/approval. Preserve sandbox, approval and repository safeguards. No commits, pushes, publication, deployment, release, history rewriting or irreversible remote actions without explicit authorisation for that action.
