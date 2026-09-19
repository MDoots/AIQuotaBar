using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Providers;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using AIQuotaBar.App.Views;
using AIQuotaBar.Core.Interfaces;
using AIQuotaBar.Core.Models;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1) return 2;
        var output = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(output);
        // Render production XAML with fixtures. No App startup, HWND interaction,
        // user settings, provider process, tray registration or model session.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var observations = new List<object>();
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var width in new[] { 170.0, 350.0, 580.0 })
        foreach (var compact in new[] { false, true })
            Render(output, observations, width, scale, compact, false, 1, false);
        foreach (var count in new[] { 0, 1, 5 })
        {
            Render(output, observations, 350, 1.0, false, true, count, false);
            Render(output, observations, 350, 2.0, false, true, count, true);
        }
        foreach (var scenario in new[] { "missing", "signed-out", "unsupported", "stale", "no-quota" })
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        foreach (var width in new[] { 170.0, 350.0 })
        foreach (var compact in new[] { false, true })
        foreach (var docked in new[] { false, true })
            Render(output, observations, width, scale, compact, docked, scenario == "missing" ? 5 : 1, false, scenario);
        File.WriteAllText(Path.Combine(output, "layout-observations.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        app.Shutdown();
        return 0;
    }

    private static void Render(string output, List<object> observations, double width, double scale,
        bool compact, bool docked, int count, bool hidden, string scenario = "available")
    {
        var requestedWidth = width;
        var sections = Enumerable.Range(0, count).Select(index =>
        {
            var provider = new FixtureProvider(index);
            var section = new ProviderSectionViewModel(provider, TimeSpan.FromHours(1));
            if (scenario == "missing")
                section.ApplyDiscoveryStatus(ProviderDiscoveryStatus.NotDetected);
            else if (scenario == "available")
                section.ApplySnapshot(provider.Snapshot);
            else
            {
                if (scenario == "stale") section.ApplySnapshot(provider.Snapshot);
                section.ApplySnapshot(new ProviderSnapshot(provider.Id, provider.DisplayName,
                    scenario switch
                    {
                        "signed-out" => ProviderStatus.Unauthenticated,
                        "stale" => ProviderStatus.Timeout,
                        _ => ProviderStatus.Unavailable
                    }, scenario switch
                    {
                        "signed-out" => "Sign in using the official provider tool.",
                        "stale" => "Provider did not respond; showing the last successful quota.",
                        "no-quota" => "No finite quota is exposed by this provider.",
                        _ => "Automatic quota is unavailable; view usage in the official provider tool."
                    }));
            }
            return section;
        }).ToArray();
        using var vm = new WidgetViewModel(providerSections: sections)
        {
            WidgetWidth = width - 20,
            IsCompactMode = compact,
            DockMode = docked ? WidgetDockMode.Top : WidgetDockMode.Floating,
            AutoHideDockedBar = false,
            IsDockCollapsed = false
        };
        var settings = new AppSettings();
        if (hidden) foreach (var section in sections) settings.SetQuotaWindowVisible(section.ProviderId, "quota", false);
        vm.UpdateVisibility(settings);
        var window = new WidgetWindow { DataContext = vm, Width = width, ShowActivated = false };
        var root = (FrameworkElement)window.Content;
        // Flush data/template bindings without showing a window.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
        root.Measure(new Size(width, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, width, root.DesiredSize.Height));
        root.UpdateLayout();
        if (docked)
        {
            width = Math.Max(170, DockingHelper.CalculateDockedOuterWidth(window.MeasureDesiredDockedOuterWidth(), 1920));
            window.Width = width;
            root.Measure(new Size(width, double.PositiveInfinity));
            root.Arrange(new Rect(0, 0, width, root.DesiredSize.Height));
            root.UpdateLayout();
        }
        var height = Math.Max(1, root.ActualHeight);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var name = $"{scenario}-{(docked ? "dock" : "float")}-{width:0}-from{requestedWidth:0}-{scale:0.00}-{(compact ? "compact" : "expanded")}-{count}-{hidden}.png";
        using (var stream = File.Create(Path.Combine(output, name)))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }
        var buttons = Descendants(root).OfType<Button>().Where(b => IsRendered(b, root) && b.ActualWidth > 0)
            .Select(b => new { Name = System.Windows.Automation.AutomationProperties.GetName(b), Width = b.ActualWidth, Height = b.ActualHeight }).ToArray();
        observations.Add(new { File = name, Scenario = scenario, WidthDip = width, HeightDip = height, RasterScale = scale, Buttons = buttons,
            NoProvidersMessage = vm.ShowZeroProvidersDetected, StaleQuota = sections.Any(s => s.IsQuotaStale),
            Limitation = "Offscreen XAML rendering; raster scale does not simulate an actual mixed-DPI monitor or native resize." });
        window.Close();
    }

    private static bool IsRendered(DependencyObject child, DependencyObject root)
    {
        for (DependencyObject? current = child; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement element && element.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(current, root)) return true;
        }
        return false;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private sealed class FixtureProvider(int index) : IUsageProvider
    {
        public string Id => index == 0 ? "codex" : $"fixture-{index}";
        public string DisplayName => index == 0 ? "OpenAI Codex" : $"Long provider label {index}";
        public ProviderSnapshot Snapshot => new(Id, DisplayName, ProviderStatus.Available, windows:
            [new QuotaWindow("quota", index == 0 ? "Codex · Weekly" : "Long quota pool label · Weekly", 28, TimeSpan.FromDays(7), DateTimeOffset.UtcNow.AddDays(2))]);
        public Task<ProviderSnapshot> GetUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);
    }
}
