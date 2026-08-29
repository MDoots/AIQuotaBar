namespace AIQuotaBar.App.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;

public sealed class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly AppSettings _settings;
    private readonly SettingsManager _settingsManager;
    private readonly WidgetViewModel? _widgetViewModel;
    private readonly Action<Uri>? _urlLauncher;
    private bool _lowQuotaNotificationsEnabled;
    private bool _isRescanning;
    private bool _disposed;

    public Action? RequestClose { get; set; }

    public ObservableCollection<ProviderSetupItemViewModel> ProviderSetupItems { get; } = new();
    public ObservableCollection<ProviderVisibilityItemViewModel> Providers { get; } = new();

    public bool IsRescanning
    {
        get => _isRescanning;
        private set
        {
            if (SetProperty(ref _isRescanning, value))
            {
                OnPropertyChanged(nameof(CanRescan));
            }
        }
    }

    public bool CanRescan => !IsRescanning && (_widgetViewModel == null || !_widgetViewModel.IsDiscoveringProviders);

    public WidgetDockMode DockMode
    {
        get => _settings.DockMode;
        set
        {
            if (_settings.DockMode != value)
            {
                _settings.DockMode = value;
                _settingsManager.Save(_settings);
                if (_widgetViewModel != null && _widgetViewModel.DockMode != value)
                {
                    _widgetViewModel.DockMode = value;
                }
                OnPropertyChanged(nameof(DockMode));
                OnPropertyChanged(nameof(IsFloatingDockMode));
                OnPropertyChanged(nameof(IsTopDockMode));
                OnPropertyChanged(nameof(IsBottomDockMode));
            }
        }
    }

    public bool IsFloatingDockMode
    {
        get => DockMode == WidgetDockMode.Floating;
        set
        {
            if (value)
            {
                DockMode = WidgetDockMode.Floating;
            }
        }
    }

    public bool IsTopDockMode
    {
        get => DockMode == WidgetDockMode.Top;
        set
        {
            if (value)
            {
                DockMode = WidgetDockMode.Top;
            }
        }
    }

    public bool IsBottomDockMode
    {
        get => DockMode == WidgetDockMode.Bottom;
        set
        {
            if (value)
            {
                DockMode = WidgetDockMode.Bottom;
            }
        }
    }

    private bool _autoHideDockedBar;

    public bool AutoHideDockedBar
    {
        get => _autoHideDockedBar;
        set
        {
            if (SetProperty(ref _autoHideDockedBar, value))
            {
                _settings.AutoHideDockedBar = value;
                _settingsManager.Save(_settings);
                if (_widgetViewModel != null)
                {
                    _widgetViewModel.AutoHideDockedBar = value;
                }
            }
        }
    }

    public bool LowQuotaNotificationsEnabled
    {
        get => _lowQuotaNotificationsEnabled;
        set
        {
            if (SetProperty(ref _lowQuotaNotificationsEnabled, value))
            {
                _settings.LowQuotaNotificationsEnabled = value;
                _settingsManager.Save(_settings);
            }
        }
    }

    public string AppVersionText
    {
        get
        {
            var version = typeof(SettingsViewModel).Assembly.GetName().Version;
            if (version != null)
            {
                return $"AIQuotaBar {version.Major}.{version.Minor}.{version.Build}";
            }
            return "AIQuotaBar 1.0.0";
        }
    }

    public string PublisherText => "Publisher: AGIFutures";

    public ICommand ResetDefaultsCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand OpenGitHubCommand { get; }
    public ICommand OpenPrivacyCommand { get; }

    public SettingsViewModel(
        AppSettings settings,
        SettingsManager settingsManager,
        WidgetViewModel? widgetViewModel = null,
        Action<Uri>? urlLauncher = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _widgetViewModel = widgetViewModel;
        _urlLauncher = urlLauncher;
        _lowQuotaNotificationsEnabled = _settings.LowQuotaNotificationsEnabled;
        _autoHideDockedBar = _settings.AutoHideDockedBar;

        if (_widgetViewModel != null)
        {
            _widgetViewModel.DockModeChanged += OnWidgetDockModeChanged;
            _widgetViewModel.ProviderDiscoveryUpdated += OnProviderDiscoveryUpdated;
            _widgetViewModel.QuotaStateUpdated += OnQuotaStateUpdated;
            foreach (var provider in _widgetViewModel.Providers)
            {
                provider.PropertyChanged += OnProviderSectionPropertyChanged;
            }
        }

        ResetDefaultsCommand = new RelayCommand(ResetDefaults);
        RescanCommand = new RelayCommand(async () => await RescanProvidersAsync(), () => CanRescan);
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        OpenGitHubCommand = new RelayCommand(() => OpenUrl("https://github.com/MDoots/AIQuotaBar"));
        OpenPrivacyCommand = new RelayCommand(() => OpenUrl("https://github.com/MDoots/AIQuotaBar/blob/main/PRIVACY.md"));

        PopulateSetupItems();
        PopulateProviders();
        UpdateProviderSetupStatus();
    }

    private void OpenUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            if (_urlLauncher != null)
            {
                _urlLauncher(uri);
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // Ignored
        }
    }

    private void OnWidgetDockModeChanged(WidgetDockMode mode)
    {
        _settings.DockMode = mode;
        OnPropertyChanged(nameof(DockMode));
        OnPropertyChanged(nameof(IsFloatingDockMode));
        OnPropertyChanged(nameof(IsTopDockMode));
        OnPropertyChanged(nameof(IsBottomDockMode));
    }

    private void OnProviderDiscoveryUpdated()
    {
        UpdateProviderSetupStatus();
        OnPropertyChanged(nameof(CanRescan));
    }

    private void OnQuotaStateUpdated()
    {
        UpdateProviderSetupStatus();
    }

    private void OnProviderSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProviderSectionViewModel.Status)
            or nameof(ProviderSectionViewModel.StatusMessage)
            or nameof(ProviderSectionViewModel.IsLoading)
            or nameof(ProviderSectionViewModel.HasWindows)
            or nameof(ProviderSectionViewModel.DiscoveryStatus))
        {
            UpdateProviderSetupStatus();
        }
    }

    public async Task RescanProvidersAsync()
    {
        if (IsRescanning || (_widgetViewModel != null && _widgetViewModel.IsDiscoveringProviders))
        {
            return;
        }

        IsRescanning = true;
        try
        {
            if (_widgetViewModel != null)
            {
                await _widgetViewModel.RescanProvidersAsync();
            }
        }
        finally
        {
            IsRescanning = false;
            UpdateProviderSetupStatus();
        }
    }

    public void UpdateProviderSetupStatus()
    {
        foreach (var item in ProviderSetupItems)
        {
            var section = _widgetViewModel?.Providers.FirstOrDefault(p => string.Equals(p.ProviderId, item.ProviderId, StringComparison.OrdinalIgnoreCase));
            var discoveryStatus = section?.DiscoveryStatus ?? ProviderDiscoveryStatus.Unknown;
            if (_widgetViewModel?.IsDiscoveringProviders == true && discoveryStatus == ProviderDiscoveryStatus.Unknown)
            {
                discoveryStatus = ProviderDiscoveryStatus.Checking;
            }
            item.UpdateStatus(discoveryStatus, section);
        }
    }

    private void PopulateSetupItems()
    {
        ProviderSetupItems.Clear();
        var descriptors = _widgetViewModel?.Descriptors ?? ProviderCatalog.All;
        if (descriptors.Count == 0)
        {
            descriptors = ProviderCatalog.All;
        }

        foreach (var descriptor in descriptors)
        {
            var setupItem = new ProviderSetupItemViewModel(descriptor, _urlLauncher);
            ProviderSetupItems.Add(setupItem);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_widgetViewModel != null)
        {
            _widgetViewModel.DockModeChanged -= OnWidgetDockModeChanged;
            _widgetViewModel.ProviderDiscoveryUpdated -= OnProviderDiscoveryUpdated;
            _widgetViewModel.QuotaStateUpdated -= OnQuotaStateUpdated;
            foreach (var provider in _widgetViewModel.Providers)
            {
                provider.PropertyChanged -= OnProviderSectionPropertyChanged;
            }
        }
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
            // Fallback default provider configuration using ProviderCatalog
            foreach (var descriptor in ProviderCatalog.All)
            {
                var item = new ProviderVisibilityItemViewModel(
                    descriptor.Id,
                    descriptor.DisplayName,
                    _settings.IsProviderVisible(descriptor.Id),
                    OnProviderVisibilityChanged);
                AddFallbackWindows(item, descriptor.Id);
                Providers.Add(item);
            }
        }
    }

    private void AddFallbackWindows(ProviderVisibilityItemViewModel providerItem, string providerId)
    {
        var descriptor = ProviderCatalog.GetDescriptor(providerId);
        if (descriptor != null)
        {
            foreach (var known in descriptor.KnownQuotaWindows)
            {
                providerItem.Windows.Add(new QuotaWindowVisibilityItemViewModel(
                    descriptor.Id,
                    known.Id,
                    known.DisplayName,
                    _settings.IsQuotaWindowVisible(descriptor.Id, known.Id),
                    OnWindowVisibilityChanged));
            }
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
