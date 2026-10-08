# Architecture and development setup

This describes the implementation audited on 8 October 2026 at `45d850f`.
Read [AGENTS](../../AGENTS.md) for mandatory invariants and [STATUS](STATUS.md) for validation.

## Layers and directory map

| Area | Responsibility and dependencies |
| --- | --- |
| `src/AIQuotaBar.Core` | `net10.0` domain models, provider interface and time formatters; no UI dependencies. |
| `src/AIQuotaBar.Providers.*` | Five isolated `net10.0` implementations of `IUsageProvider`; reference Core only. Transport/normalization stay here. Existing public protocol DTOs are compatibility debt, never an App binding contract. |
| `src/AIQuotaBar.App` | `net10.0-windows10.0.22621.0`; WPF/MVVM, provider composition, discovery, layout, settings, tray and lifecycle. References Core/providers; UI consumes normalized Core models. |
| `src/AIQuotaBar.Package` | WAP project and manifest for separate x64 MSIX packaging; full-trust desktop entry point and packaged startup task. |
| `tests/` | Eight test projects plus a child-process fixture; domain, UI helpers/view models, provider fixtures and process safety. |
| `tools/AIQuotaBar.ProviderProbe` | Bounded live checks using production provider adapters; results are sanitized statuses, not guaranteed quota. |
| `tools/AIQuotaBar.LayoutProbe` | Windows layout diagnostic. |
| `tools/AIQuotaBar.CodexTrace` | Developer-only elevated Windows ETW diagnostic, outside the filtered solution; isolated `Microsoft.Diagnostics.Tracing.TraceEvent` dependency. See its [README](../../tools/AIQuotaBar.CodexTrace/README.md). |
| `scripts/`, `.github/workflows/` | Local packaging/validation/probe scripts and Windows CI/release automation. |
| `docs/`, `store-assets/`, `assets/` | Release evidence, signing guidance, listing assets and branding. Raw local artifacts are ignored. |

## Startup and data flow

`App.xaml.cs` loads settings, creates `WidgetViewModel`/`WidgetWindow`, wires the
tray and persistence, and starts `PowerResumeCoordinator`. `ProviderCatalog`
centralizes descriptors, locators, provider factories and setup links.

`ProviderDiscoveryService` resolves installed executable presence separately from
authentication/quota capability. `WidgetViewModel` schedules provider refreshes
(Codex: 60 seconds; other providers: 180 seconds) and a 30-second countdown
timer, coordinates discovery
and manual refresh, and updates observable sections on the WPF dispatcher.
`ProviderSectionViewModel` cancels superseded refreshes and applies only the current attempt's snapshot.

```text
official CLI -> provider transport -> provider normalizer -> Core ProviderSnapshot
             -> ProviderSectionViewModel/QuotaWindowViewModel -> WPF and tray health
```

`ProviderSnapshot` carries provider status, quota windows, observation time,
plan information and an opaque account scope. View models track stale/degraded display state. `QuotaWindow` holds remaining percentage,
reset/duration and quota status. Unknown finite values remain unknown. Account
scope changes clear obsolete cached quota; transient failures preserve valid
rows with stale indicators. Cancellation preserves valid data without alerts.
Tray evaluation excludes unsuitable stale/paused observations.

## Integrations and security

| Provider | Current official-tool path |
| --- | --- |
| Codex | `--disable plugins app-server`, initialize, verify `config/read` reports `features.plugins=false`, then `account/read` and `account/rateLimits/read`. Default six-second budget. Missing/unsafe verification pauses that selected executable for the provider instance, without fallback launches; transient failures are not cached as incompatibility. |
| Antigravity | `agy -p "/usage" --output-format json`; structured CLI capture/normalization. |
| Claude Code | `claude auth status --json`; authenticated quota is unavailable. Interactive usage capture is unsupported. |
| Grok Build | `grok --no-auto-update agent stdio`; ACP billing query `x.ai/billing` and implemented fallback. |
| Copilot | Official SDK adapter against local `copilot.exe`, `account.getQuota`; no model session created. |

Provider-owned tools may contact their services. AIQuotaBar has no HTTP/network
client or backend of its own. Authentication and authorization remain provider-owned;
there is no application login, shared account database or API-key configuration.
Never read provider auth stores or persist raw RPC/config/account responses.

Runners use redirected streams, no shell execution/visible console, bounded
output and deadlines, cancellation and owned-process-tree cleanup. Logs/statuses
must omit identities, credentials and private paths. See [PRIVACY](../../PRIVACY.md),
[SECURITY](../../SECURITY.md) and [DECISIONS](DECISIONS.md).

## Storage and background work

`SettingsManager` serializes preferences to `%LOCALAPPDATA%\AIQuotaBar\settings.json`
with `System.Text.Json`, normalizes visibility settings and falls back to defaults
on unreadable data. Quota/account tracking is in memory, not a persistent history.
Portable startup uses the per-user Registry Run key; packaged startup uses the
manifest StartupTask through `StartupManager`/its platform handlers.

Refresh/countdown dispatcher timers and an eight-second delayed power-resume
refresh run while the app is alive. There is no server scheduler, cron job, queue,
database, ORM, schema, migrations, seed data, Supabase or other hosted persistence.
There is no development database to initialize/reset.

## Windows development

Install Git and the .NET 10 SDK from the [official download page](https://dotnet.microsoft.com/download).
Windows 11 x64 is the supported runtime; Store packaging additionally requires
Visual Studio MSBuild/DesktopBridge and Windows SDK/UAP `10.0.22621.0`.
No `global.json`, package lockfile, separate lint command or cloud setup script is
currently checked in; project files specify dependencies and existing patterns govern style.

```powershell
git clone https://github.com/MDoots/AIQuotaBar.git
cd AIQuotaBar
dotnet --list-sdks
dotnet restore AIQuotaBar.slnf
dotnet test AIQuotaBar.slnf --no-restore
dotnet build AIQuotaBar.slnf -c Release --no-restore
dotnet test AIQuotaBar.slnf -c Release --no-restore
dotnet run --project src/AIQuotaBar.App/AIQuotaBar.App.csproj
git diff --check
```

Use the SDK's installer appropriate to the environment. Initial restore needs
NuGet access (or populated caches); subsequent `--no-restore` tests use local
fixtures and require no provider login/service. The app launch uses existing user
preferences and discovers local tools; use a disposable Windows user/VM for clean-machine tests.
Build must report zero warnings/errors and all full-suite tests must pass.
Do not hide failed checks or disable NuGet auditing to claim vulnerability verification.

No environment variables are required. Optional executable overrides are
`AIQUOTABAR_CODEX_PATH` and `AIQUOTABAR_ANTIGRAVITY_PATH` (alias
`AIQUOTABAR_AGY_PATH`), each a full path to an already-installed
official executable. Other providers use their existing known locations/PATH discovery. For example, in a disposable shell only:

```powershell
$env:AIQUOTABAR_CODEX_PATH = 'C:\Tools\Codex\codex.exe' # replace harmless example
```

Unset overrides for normal discovery. The app does not load dotenv files; no
`.env.example` is needed. Never add provider tokens to environment templates or CI.
Provider CLI installation/sign-in is optional for development tests and required
only for the corresponding live functionality; follow README setup guidance.

## Isolated/cloud agents

An agent can clone/read/edit this repository without Windows. Provision a .NET 10
SDK and restore packages during permitted setup; verify `dotnet --list-sdks` rather
than assuming an image includes it. The official [Codex cloud environment guide](https://learn.chatgpt.com/docs/environments/cloud-environment)
describes selected-branch checkout and setup/maintenance scripts. No project cloud
environment has been configured or executed by this audit.

For a Bash environment with .NET 10 installed, this is the portable subset:

```bash
for project in tests/AIQuotaBar.Core.Tests/AIQuotaBar.Core.Tests.csproj tests/AIQuotaBar.Providers.*.Tests/*.csproj; do
  dotnet restore "$project" || exit 1
  dotnet build "$project" -c Release --no-restore || exit 1
  dotnet test "$project" -c Release --no-build --no-restore || exit 1
done
```

These six projects contain 190 tests at the audited revision. Linux execution is
not verified here. They are not the full acceptance suite. `AIQuotaBar.App.Tests`
and LayoutProbe require Windows/WPF; process-safety tests currently locate a
Windows `.exe` fixture. The unchanged filtered solution does not opt into Linux
Windows-targeting builds. WPF runtime/UI, tray, monitors, power events, Registry,
MSIX/WACK and authenticated local provider checks require a suitable Windows
environment. Missing Windows/services are unavailable checks, never passes.
Return cloud changes to Windows CI/local validation for the full required suite.

## Build and release environments

- `AIQuotaBar.slnf` excludes WAP; `AIQuotaBar.slnx` includes it. Normal CI runs
  restore, Release build and full tests on `windows-latest` for `main` pushes and PRs.
- MSIX CI compiles only when the UAP SDK exists; its documented skip is not
  packaging/certification evidence. Equipped local Windows builds remain necessary.
- `scripts/build-portable.ps1` produces self-contained single-file win-x64 with
  `PublishTrimmed=false`; `-OutputDirectory` supports isolated candidates beneath
  `artifacts/`. Preserve frozen evidence; the default output can be cleaned/rebuilt.
- The release workflow runs on `v*` tags, tests and publishes the portable ZIP plus
  checksum to GitHub. Its signing block is only a future insertion point.
- Store packaging/submission remains separate. Follow [release checklist](../release-checklist.md),
  [signing guidance](../code-signing.md) and [1.0.5 acceptance](../release-acceptance-1.0.5.md).
  Exact-byte provenance, installed-package/upgrade tests and WACK are separate gates.

Only publish, tag, submit, install over an existing app or change production after
authorization for that action. This documentation task changes no deployment configuration.
