# AGENTS.md: Developer & AI Agent Guide for AIQuotaBar

> [!CAUTION]
> **LOCAL-FIRST & ZERO-CREDENTIAL POLICY**:
> - **Zero Outbound Network Egress:** AIQuotaBar makes zero outbound network requests of its own. It operates without a remote backend, telemetry, tracking, or analytics.
> - **Never Access Credential Stores Directly:** Never inspect, read, parse, or capture authentication files (such as `.codex\auth.json`, `.claude.json`, or provider token vaults).
> - **Provider-Owned Authentication:** All authentication remains strictly owned by each provider's official CLI or application.
> - **Sanitize Logs & Status Messages:** Exception handlers, status messages, and test fixtures must never expose user identities, file paths, tokens, or email addresses.

> [!IMPORTANT]
> **READ BEFORE CODING**:
> Review this document thoroughly before modifying any code. AIQuotaBar enforces strict architectural boundaries across its projects (`Core`, `Providers`, and `App`), requires `0 Warning(s)` and `0 Error(s)` on builds, and mandates 100% offline testability.

---

## 1. Project Nature & Scope

**AIQuotaBar** is a lightweight, Windows-first, local-first desktop widget designed to monitor AI subscription quotas and rate limits across coding agent providers in real time.

### The Problem It Solves
Developers increasingly juggle multiple AI coding assistants (OpenAI Codex, Google Antigravity, Claude Code, Grok Build, GitHub Copilot). Each provider has unique, opaque rate-limit reset windows (5-hour rolling limits, weekly caps, request allotments) that interrupt development workflows when exhausted unexpectedly.

**AIQuotaBar solves this by:**
- Providing an always-visible, minimal desktop bar or screen-edge docked strip.
- Tracking real-time quota remaining percentages with color-coded health indicators (Green/Teal, Amber, Red).
- Showing rolling countdown timers to the next quota reset.
- Querying local official CLI/app-server tools directly without requiring browser dashboards or extra credentials.

### Supported Providers (v1.0+)

| Provider | CLI / Tool Required | Query Mechanism | Transport / Protocol |
| :--- | :--- | :--- | :--- |
| **OpenAI Codex** | `codex` | `codex app-server` | Local stdio JSON-RPC (`account/rateLimits/read`) |
| **Google Antigravity** | `agy` | `agy -p "/usage" --output-format json` | Non-interactive CLI process execution |
| **Claude Code** | `claude` | `claude auth status --json` | Authentication status; automatic quota unsupported until a safe official route is verified. View `/usage` in Claude Code. |
| **Grok Build** | `grok` | `grok --no-auto-update agent stdio` | Local stdio ACP server (`x.ai/billing`) |
| **GitHub Copilot** | `copilot` | Official `GitHub.Copilot.SDK` | Local stdio RPC (`account.getQuota`) |

### Plan-Agnostic Quota Semantics
AIQuotaBar is **plan-agnostic**. It displays finite quotas where exposed by a supported provider CLI/version. Actual `ProviderStatus` values are `Available`, `Unauthenticated`, `Unavailable`, `Error`, `Timeout` and `Cancelled`; discovery separately identifies missing tools. Unknown or unsupported quota must not be presented as full or exhausted. Provider-owned tools may contact their own services; AIQuotaBar has no network client/backend of its own.

---

## 2. Architecture & Layer Boundaries

The repository is organized into strict, decoupled layers:

```
                  ┌──────────────────────────────────────────────┐
                  │       AIQuotaBar.Package (WAP / MSIX)         │
                  └──────────────────────┬───────────────────────┘
                                         │ packages
                                         ▼
                  ┌──────────────────────────────────────────────┐
                  │      AIQuotaBar.App (WPF / Presentation)     │
                  │   - Views & ViewModels (MVVM)                │
                  │   - System Tray & Health Evaluator           │
                  │   - Docking & Window Restoration Engine      │
                  │   - Settings & Local Storage Persistence     │
                  │   - Windows Power / Sleep Recovery           │
                  └──────────────────────┬───────────────────────┘
                                         │ references
                                         ▼
                  ┌──────────────────────────────────────────────┐
                  │       AIQuotaBar.Core (Domain Abstractions)  │
                  │   - Models: ProviderSnapshot, QuotaWindow    │
                  │   - Enums: ProviderStatus, QuotaWindowStatus │
                  │   - Interfaces: IUsageProvider               │
                  │   - Formatters: Countdown, Duration          │
                  └──────────────▲────────────────▲──────────────┘
                                 │                │ references
                     ┌───────────┴───┐        ┌───┴───────────┐
                     │ Providers.*   │  ...   │ Providers.*   │
                     │ (Codex, AGY)  │        │ (Claude, Grok)│
                     └───────────────┘        └───────────────┘
```

### Invariant Rules
1. **Core Independence:**
   - `AIQuotaBar.Core` targets `net10.0` and must remain completely agnostic of WPF, WinForms, or any UI framework.
   - It defines domain models (`ProviderSnapshot`, `QuotaWindow`, `ProviderStatus`, `QuotaWindowStatus`), utility formatters, and provider interfaces (`IUsageProvider`).
2. **Provider Independence:**
   - Each provider project (`AIQuotaBar.Providers.<Name>`) targets `net10.0` and references **only** `AIQuotaBar.Core`.
   - Providers must **never** reference WPF, XAML, or `AIQuotaBar.App`.
   - Keep new transport implementation details internal. Existing public protocol DTOs are compatibility debt, not an App binding contract; views and view models consume only Core models.
3. **UI Decoupling:**
   - `AIQuotaBar.App` targets `net10.0-windows10.0.22621.0` and consumes only normalized domain models (`IUsageProvider`, `ProviderSnapshot`, `QuotaWindow`).
   - ViewModels and Views must never bind to or inspect provider-specific DTOs.
4. **Zero Cross-Provider Leakage:**
   - Provider assemblies are isolated from one another. Modifying or adding one provider must have zero side-effects on others.
5. **Zero Production Third-Party Dependencies:**
   - Production projects in `src/` must rely strictly on the standard .NET Base Class Library (BCL) and Windows Desktop APIs. Third-party packages (e.g. Newtonsoft.Json, CommunityToolkit, Prism) must **not** be introduced. (The sole isolated exception is `GitHub.Copilot.SDK` encapsulated inside `AIQuotaBar.Providers.GitHubCopilot`).

---

## 3. Critical Architectural Invariants & Gotchas

> [!WARNING]
> **PAY ATTENTION TO THESE NINE GOTCHAS BEFORE IMPLEMENTING CHANGES:**

### 1. Zero Direct Credential Access
Never attempt to find, read, or parse provider auth files (e.g., `%USERPROFILE%\.codex\auth.json`, `%USERPROFILE%\.claude.json`, or Git credentials). Querying quotas is done strictly by running the provider's official CLI or local RPC server in a non-interactive mode.

### 2. Non-Interactive CLI Process Safety
When launching provider tools (`codex`, `agy`, `claude`, `grok`, `copilot`):
- Set `ProcessStartInfo.UseShellExecute = false`.
- Set `ProcessStartInfo.CreateNoWindow = true`.
- Set `ProcessStartInfo.RedirectStandardInput`, `RedirectStandardOutput`, and `RedirectStandardError = true`.
- Enforce bounded timeouts on all process operations (e.g. 5–10 seconds).
- Always terminate the entire child process tree on timeout, cancellation, or error.
- Never trigger interactive prompts (e.g., avoid commands that pause for user confirmation or login prompts in stdio).

### 3. Last-Known-Good Quota Resilience
During transient network drops, CLI restarts, or brief process timeouts:
- **Never wipe existing valid quota data.**
- Retain the last successful `QuotaWindow` rows in the UI.
- Visually mark the provider status or individual window as stale/degraded (`IsDegraded` / `IsStale`) without blanking the numbers or shrinking the window layout unexpectedly.

### 4. Cancellation Safety in Refresh Loops
When a refresh cycle is cancelled (e.g. due to rapid manual refresh clicks or widget mode changes):
- The `OperationCanceledException` / `TaskCanceledException` must be caught gracefully.
- Cancelled attempts must **not** mark valid data as stale or trigger false low-quota warning alerts.

### 5. Window Restoration & Off-Screen Recovery
The "Open AIQuotaBar" action from the system tray must be idempotent and resilient:
- If the widget was placed on an external monitor that has been disconnected, the window coordinates must automatically clamp back into the primary active screen's work area (`PositionHelper.EnsureVisibleOnScreen`).
- Clicking the tray icon while the window is already active must bring it to the foreground rather than resetting its position.

### 6. Responsive Layout & Adaptive Typography
The floating widget supports horizontal resizing between 170 and 580 WPF DIP (150–560 content DIP). Docked width follows measured visible content and the work-area cap:
- Labels must adapt and truncate cleanly (`QuotaLabelFormatter`, `ResponsiveLayoutHelper`).
- Both Compact (single-line aggregate) and Expanded (per-provider card) modes must fit standard screen scaling factors (100% to 250% DPI).

### 7. Magnetic Soft-Docking Behavior
When dragged within snap distance of the top or bottom screen edge:
- The widget docks cleanly and switches into `WidgetDockMode.Top` or `WidgetDockMode.Bottom`.
- If Auto-Hide is enabled, the bar retracts when the mouse leaves and re-expands on mouse hover.
- Preserves the floating geometry in memory so that undocking restores the exact previous floating window position and dimensions.

### 8. Disabled Trimming Requirement
In `AIQuotaBar.App.csproj` and `build-portable.ps1`:
- `PublishTrimmed=false` is strictly mandatory. Trimming breaks WPF XAML reflection, template bindings, and dynamic resource dispatch.

### 9. Store Packaging vs Portable Separation
- Portable release: Built using `scripts/build-portable.ps1` as a self-contained single-file `.exe`.
- Store release: Packaged via `src/AIQuotaBar.Package/AIQuotaBar.Package.wapproj` into an MSIX bundle conforming to Microsoft Store certification guidelines.
- Clean-machine onboarding: On a PC with no provider CLIs installed, the app must display a clean "No supported providers detected" empty state with guidance to Settings.

---

## 4. Repository Layout

```
c:\Projects\AIQuotaBar\
├── AGENTS.md                  # This developer & AI agent guide
├── AIQuotaBar.slnx            # Full Visual Studio XML solution (includes WAP package)
├── AIQuotaBar.slnf            # Filtered solution (excludes WAP for universal dotnet CLI)
├── CHANGELOG.md               # Keep a Changelog format revision history
├── CONTRIBUTING.md            # Community contribution guidelines
├── LICENSE                    # MIT License
├── PRIVACY.md                 # Local-first privacy policy
├── README.md                  # User-facing documentation and features overview
├── SECURITY.md                # Security and vulnerability reporting policy
├── .agents/
│   └── rules/
│       └── aiquotabar-engineering.md # Core engineering rule pointer
├── artifacts/
│   └── portable/win-x64/      # Build output for self-contained executable
├── docs/
│   ├── code-signing.md        # Authenticode & Store signing instructions
│   ├── release-checklist.md   # Pre-flight release & Store certification checklist
│   └── images/                # Screenshots (app-preview.png, app-docked.png)
├── scripts/
│   ├── build-portable.ps1     # PowerShell script producing single-file portable win-x64
│   └── test-live-providers.ps1# Non-destructive live provider probe harness
├── src/
│   ├── AIQuotaBar.Core/       # Domain models, interfaces, formatters (net10.0)
│   │   ├── Interfaces/        # IUsageProvider
│   │   ├── Models/            # ProviderSnapshot, QuotaWindow, ProviderStatus
│   │   └── Utils/             # CountdownFormatter, DurationFormatter
│   ├── AIQuotaBar.App/        # WPF Presentation Layer (net10.0-windows10.0.22621.0)
│   │   ├── Controls/          # Custom WPF controls & templates
│   │   ├── Converters/        # Value converters (RemainingToBrush, etc.)
│   │   ├── Health/            # QuotaHealthHelper & health level models
│   │   ├── Layout/            # DockingHelper, ResponsiveLayoutHelper, QuotaLabelFormatter
│   │   ├── Platform/          # PackageIdentity (MSIX detection)
│   │   ├── Providers/         # ProviderCatalog, ProviderDiscoveryService
│   │   ├── Services/          # PowerResumeCoordinator (Sleep/Wake recovery)
│   │   ├── Settings/          # AppSettings, SettingsManager, PositionHelper, StartupManager
│   │   ├── Tray/              # TrayManager, TrayHealthCalculator, QuotaNotificationEvaluator
│   │   ├── ViewModels/        # WidgetViewModel, SettingsViewModel, ProviderSectionViewModel
│   │   └── Views/             # WidgetWindow.xaml, SettingsWindow.xaml
│   ├── AIQuotaBar.Package/    # Windows Application Packaging (MSIX) for Store
│   ├── AIQuotaBar.Providers.Codex/          # OpenAI Codex stdio JSON-RPC provider
│   ├── AIQuotaBar.Providers.Antigravity/    # Google Antigravity CLI process provider
│   ├── AIQuotaBar.Providers.ClaudeCode/     # Anthropic Claude Code CLI provider
│   ├── AIQuotaBar.Providers.GrokBuild/      # xAI Grok Build ACP stdio provider
│   └── AIQuotaBar.Providers.GitHubCopilot/  # GitHub Copilot SDK RPC provider
├── store-assets/              # Store listing metadata and assets
└── tests/
    ├── AIQuotaBar.Core.Tests/               # Domain model & formatter unit tests
    ├── AIQuotaBar.App.Tests/                # ViewModels, layout, docking, tray & settings tests
    ├── AIQuotaBar.Providers.Codex.Tests/    # Codex protocol & JSON fixture tests
    ├── AIQuotaBar.Providers.Antigravity.Tests/ # Antigravity CLI parsing tests
    ├── AIQuotaBar.Providers.ClaudeCode.Tests/  # Claude Code CLI parsing tests
    ├── AIQuotaBar.Providers.GrokBuild.Tests/   # Grok Build protocol tests
    └── AIQuotaBar.Providers.GitHubCopilot.Tests/ # Copilot normalization tests
```

---

## 5. Essential Commands & Verification Reference (PowerShell)

### 1. Build the Filtered Solution (Recommended for CLI)
To compile all production and test projects without requiring the Windows Packaging project:
```powershell
dotnet build AIQuotaBar.slnf -c Release
```
*Requirement: Must finish with `0 Warning(s)` and `0 Error(s)`.*

### 2. Run the Full Offline Test Suite
To execute all 600+ offline unit tests:
```powershell
dotnet test AIQuotaBar.slnf -c Release
```
*Requirement: 100% of unit tests must pass without any network connection.*

### 3. Run the Application Locally
To launch the WPF widget in development mode:
```powershell
dotnet run --project src/AIQuotaBar.App/AIQuotaBar.App.csproj
```

### 4. Build the Self-Contained Portable Executable
To generate the release single-file portable executable in `artifacts/portable/win-x64/AIQuotaBar.exe`:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-portable.ps1
```

### 5. Run Non-Destructive Live Provider Verification
To safely probe installed local provider tools without sending prompt tokens or modifying active sessions:
```powershell
pwsh scripts/test-live-providers.ps1
```

### 6. User Settings File Location
During testing, persistent user settings, monitor coordinates, and provider toggle states can be inspected at:
```powershell
Get-Content "$env:LOCALAPPDATA\AIQuotaBar\settings.json" | ConvertFrom-Json
```

---

## 6. Development Workflow Rules for AI Agents

When tasked with implementing features, fixing bugs, or refactoring in AIQuotaBar:

1. **Verify the Baseline First:**
   Run `dotnet test AIQuotaBar.slnf` before modifying code to confirm the test suite is green.

2. **Honor Architectural Boundaries:**
   - Need a new domain concept? Put it in `AIQuotaBar.Core`.
   - Need to adjust provider parsing? Modify only `AIQuotaBar.Providers.<Provider>` and keep all DTOs internal.
   - Need UI or tray enhancements? Work strictly inside `AIQuotaBar.App`.
   - Never reference UI classes from Core or Providers.

3. **Offline Testability First:**
   Whenever modifying provider parsing, normalization, or error handling, add or update JSON fixtures in `tests/AIQuotaBar.Providers.<Provider>.Tests/Fixtures/` and add corresponding unit tests. Never rely solely on live tools.

4. **Preserve Resilience & Last-Known-Good Data:**
   Ensure error paths do not clear existing quota data on transient failures. Always test timeout and cancellation behaviors.

5. **Clean Verification Before Completion:**
   Always run:
   - `dotnet build AIQuotaBar.slnf -c Release` (verify `0 Warning(s)`, `0 Error(s)`).
   - `dotnet test AIQuotaBar.slnf -c Release` (verify 100% passing).
