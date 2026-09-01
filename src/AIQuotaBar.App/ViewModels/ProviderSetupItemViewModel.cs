namespace AIQuotaBar.App.ViewModels;

using System.Diagnostics;
using System.Windows.Input;
using AIQuotaBar.App.Providers;
using AIQuotaBar.Core.Models;

public sealed class ProviderSetupItemViewModel : ViewModelBase
{
    private readonly Action<Uri> _urlLauncher;
    private string _statusLabel = "Checking";
    private string _statusBrush = "#71717A";
    private string _detailText = "Checking local installation...";

    public string ProviderId { get; }
    public string DisplayName { get; }
    public string ShortDisplayName { get; }
    public Uri SetupUri { get; }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => SetProperty(ref _statusLabel, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    public ICommand OpenGuideCommand { get; }

    public ProviderSetupItemViewModel(
        ProviderDescriptor descriptor,
        Action<Uri>? urlLauncher = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        ProviderId = descriptor.Id;
        DisplayName = descriptor.DisplayName;
        ShortDisplayName = descriptor.ShortDisplayName;
        SetupUri = descriptor.SetupUri;
        _urlLauncher = urlLauncher ?? DefaultLaunchUrl;
        OpenGuideCommand = new RelayCommand(SafeLaunchGuide);
    }

    private void SafeLaunchGuide()
    {
        try
        {
            _urlLauncher(SetupUri);
        }
        catch
        {
            // Do not crash the application if browser launcher fails
        }
    }

    public void UpdateStatus(ProviderDiscoveryStatus discoveryStatus, ProviderSectionViewModel? section, string? safeDiscoveryMessage = null)
    {
        if (discoveryStatus is ProviderDiscoveryStatus.Checking or ProviderDiscoveryStatus.Unknown)
        {
            StatusLabel = "Checking";
            StatusBrush = "#71717A";
            DetailText = "Checking local installation...";
            return;
        }

        if (discoveryStatus == ProviderDiscoveryStatus.NotDetected)
        {
            StatusLabel = "Not detected";
            StatusBrush = "#71717A";
            DetailText = ProviderId.ToLowerInvariant() switch
            {
                "codex" => "AIQuotaBar requires the Codex CLI. Install it, then rescan.",
                "antigravity" => "AIQuotaBar requires the Antigravity CLI (agy). Install it, then rescan.",
                "claude-code" => "AIQuotaBar requires the Claude Code CLI. Install it, then rescan.",
                "grok-build" => "AIQuotaBar requires the Grok CLI. Install it, then rescan.",
                "github-copilot" => "AIQuotaBar requires the GitHub Copilot CLI. Install it, then rescan.",
                _ => $"AIQuotaBar requires {DisplayName}. Install it, then rescan."
            };
            return;
        }

        if (discoveryStatus == ProviderDiscoveryStatus.Error)
        {
            StatusLabel = "Needs attention";
            StatusBrush = "#EF4444";
            DetailText = !string.IsNullOrWhiteSpace(safeDiscoveryMessage)
                ? safeDiscoveryMessage
                : (!string.IsNullOrWhiteSpace(section?.StatusMessage)
                    ? section.StatusMessage
                    : "Unable to check the local provider installation.");
            return;
        }

        // Discovery status is Detected
        if (section == null || (section.LastRefreshedAt == null && (section.IsLoading || (section.Status == ProviderStatus.Available && !section.HasWindows && !section.HasStatusMessage))))
        {
            StatusLabel = "Detected";
            StatusBrush = "#38BDF8";
            DetailText = "Checking quota access...";
            return;
        }

        if (section.Status == ProviderStatus.Unauthenticated)
        {
            StatusLabel = "Sign-in required";
            StatusBrush = "#F59E0B";
            DetailText = ProviderId.ToLowerInvariant() switch
            {
                "codex" => "Sign in via the Codex CLI, then rescan.",
                "antigravity" => "Sign in via the Antigravity CLI (agy), then rescan.",
                "claude-code" => "Sign in via the Claude Code CLI, then rescan.",
                "grok-build" => "Sign in via the Grok CLI, then rescan.",
                "github-copilot" => "Sign in via the GitHub Copilot CLI, then rescan.",
                _ => $"Open {ShortDisplayName} and sign in, then rescan."
            };
            return;
        }

        if (section.Status == ProviderStatus.Available && section.HasWindows)
        {
            StatusLabel = "Connected";
            StatusBrush = "#10B981";
            DetailText = "Detected locally";
            return;
        }

        if (section.Status is ProviderStatus.Error or ProviderStatus.Timeout or ProviderStatus.Unavailable)
        {
            StatusLabel = "Needs attention";
            StatusBrush = "#EF4444";
            DetailText = !string.IsNullOrWhiteSpace(section.StatusMessage)
                ? section.StatusMessage
                : "Provider query failed.";
            return;
        }

        StatusLabel = "Detected";
        StatusBrush = "#38BDF8";
        DetailText = "Checking quota access...";
    }

    private static void DefaultLaunchUrl(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Do not crash the application if the default browser fails to launch
        }
    }
}
