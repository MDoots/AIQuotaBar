namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.Windows.Input;
using AIQuotaBar.App.Settings;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly WidgetViewModel? _widgetViewModel;

    public Action? RequestClose { get; set; }

    public ObservableCollection<ProviderVisibilityItemViewModel> Providers { get; } = new();

    public ICommand ResetDefaultsCommand { get; }
    public ICommand CloseCommand { get; }

    public SettingsViewModel(
        AppSettings settings,
        SettingsManager settingsManager,
        WidgetViewModel? widgetViewModel = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _widgetViewModel = widgetViewModel;

        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

        PopulateProviders();
    }

    private void PopulateProviders()
    {
        Providers.Clear();

        if (_widgetViewModel != null && _widgetViewModel.Providers.Count > 0)
        {
            foreach (var providerSection in _widgetViewModel.Providers)
            {
                var providerItem = new ProviderVisibilityItemViewModel(
                    providerSection.ProviderId,
                    providerSection.ProviderName,
                    _settings.IsProviderVisible(providerSection.ProviderId),
                    OnProviderVisibilityChanged);

                var allWindows = providerSection.AllWindows;
                if (allWindows.Count > 0)
                {
                    foreach (var window in allWindows)
                    {
                        var isWindowVisible = _settings.IsQuotaWindowVisible(providerSection.ProviderId, window.Id);
                        providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                            providerSection.ProviderId,
                            window.Id,
                            window.RawDisplayName,
                            isWindowVisible,
                            OnWindowVisibilityChanged));
                    }
                }
                else
                {
                    AddFallbackWindows(providerItem, providerSection.ProviderId);
                }

                Providers.Add(providerItem);
            }
        }
        else
        {
            // Fallback default provider configuration if WidgetViewModel has no providers loaded yet
            var codexItem = new ProviderVisibilityItemViewModel(
                "codex",
                "OpenAI Codex",
                _settings.IsProviderVisible("codex"),
                OnProviderVisibilityChanged);
            AddFallbackWindows(codexItem, "codex");
            Providers.Add(codexItem);

            var antigravityItem = new ProviderVisibilityItemViewModel(
                "antigravity",
                "Google Antigravity",
                _settings.IsProviderVisible("antigravity"),
                OnProviderVisibilityChanged);
            AddFallbackWindows(antigravityItem, "antigravity");
            Providers.Add(antigravityItem);
        }
    }

    private void AddFallbackWindows(ProviderVisibilityItemViewModel providerItem, string providerId)
    {
        if (string.Equals(providerId, "codex", StringComparison.OrdinalIgnoreCase))
        {
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "codex", "primary", "5-Hour", _settings.IsQuotaWindowVisible("codex", "primary"), OnWindowVisibilityChanged));
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "codex", "secondary", "Weekly", _settings.IsQuotaWindowVisible("codex", "secondary"), OnWindowVisibilityChanged));
        }
        else if (string.Equals(providerId, "antigravity", StringComparison.OrdinalIgnoreCase))
        {
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "antigravity", "gemini_gemini-5h", "Gemini · 5-Hour", _settings.IsQuotaWindowVisible("antigravity", "gemini_gemini-5h"), OnWindowVisibilityChanged));
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "antigravity", "gemini_gemini-weekly", "Gemini · Weekly", _settings.IsQuotaWindowVisible("antigravity", "gemini_gemini-weekly"), OnWindowVisibilityChanged));
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "antigravity", "claude_and_gpt_3p-5h", "Claude & GPT · 5-Hour", _settings.IsQuotaWindowVisible("antigravity", "claude_and_gpt_3p-5h"), OnWindowVisibilityChanged));
            providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                "antigravity", "claude_and_gpt_3p-weekly", "Claude & GPT · Weekly", _settings.IsQuotaWindowVisible("antigravity", "claude_and_gpt_3p-weekly"), OnWindowVisibilityChanged));
        }
    }

    private void OnProviderVisibilityChanged(string providerId, bool isVisible)
    {
        _settings.SetProviderVisible(providerId, isVisible);
        _settingsManager.Save(_settings);
        _widgetViewModel?.UpdateVisibility(_settings);
    }

    private void OnWindowVisibilityChanged(string providerId, string windowId, bool isVisible)
    {
        _settings.SetQuotaWindowVisible(providerId, windowId, isVisible);
        _settingsManager.Save(_settings);
        _widgetViewModel?.UpdateVisibility(_settings);
    }

    public void ResetDefaults()
    {
        _settings.ResetVisibilityDefaults();
        _settingsManager.Save(_settings);

        foreach (var provider in Providers)
        {
            provider.SetIsVisibleSilently(true);
            foreach (var window in provider.Windows)
            {
                window.SetIsVisibleSilently(true);
            }
        }

        _widgetViewModel?.UpdateVisibility(_settings);
    }
}

public sealed class ProviderVisibilityItemViewModel : ViewModelBase
{
    private readonly Action<string, bool> _onChanged;
    private bool _isVisible;

    public string ProviderId { get; }
    public string DisplayName { get; }
    public ObservableCollection<QuotaWindowVisibilityItemViewModel> Windows { get; } = new();

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _onChanged(ProviderId, value);
            }
        }
    }

    public ProviderVisibilityItemViewModel(
        string providerId,
        string displayName,
        bool isVisible,
        Action<string, bool> onChanged)
    {
        ProviderId = providerId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        _isVisible = isVisible;
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    public void SetIsVisibleSilently(bool isVisible)
    {
        _isVisible = isVisible;
        OnPropertyChanged(nameof(IsVisible));
    }
}

public sealed class QuotaWindowVisibilityItemViewModel : ViewModelBase
{
    private readonly Action<string, string, bool> _onChanged;
    private bool _isVisible;

    public string ProviderId { get; }
    public string WindowId { get; }
    public string DisplayName { get; }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                _onChanged(ProviderId, WindowId, value);
            }
        }
    }

    public QuotaWindowVisibilityItemViewModel(
        string providerId,
        string windowId,
        string displayName,
        bool isVisible,
        Action<string, string, bool> onChanged)
    {
        ProviderId = providerId ?? string.Empty;
        WindowId = windowId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        _isVisible = isVisible;
        _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
    }

    public void SetIsVisibleSilently(bool isVisible)
    {
        _isVisible = isVisible;
        OnPropertyChanged(nameof(IsVisible));
    }
}
