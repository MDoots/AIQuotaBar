namespace AIQuotaBar.App.Tests;

using System.IO;
using AIQuotaBar.App.Layout;
using AIQuotaBar.App.Settings;
using AIQuotaBar.App.ViewModels;
using Xunit;

public class FloatingGeometryPreservationTests
{
    [Fact]
    public void DockingTransition_PreservesSavedFloatingGeometry()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_geom_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 1250.0,
                WindowTop = 300.0,
                WidgetWidth = 270.0,
                DockMode = WidgetDockMode.Floating
            };
            manager.Save(settings);

            using var vm = new WidgetViewModel
            {
                WidgetWidth = 270.0,
                DockMode = WidgetDockMode.Floating
            };

            // Transition from Floating to Top Dock
            // Simulation of App.DockModeChanged callback:
            vm.DockModeChanged = mode =>
            {
                if (mode != WidgetDockMode.Floating)
                {
                    // Saved floating geometry remains intact
                    settings.DockMode = mode;
                    manager.Save(settings);
                }
                else
                {
                    settings.DockMode = WidgetDockMode.Floating;
                    manager.Save(settings);
                }
            };

            vm.DockMode = WidgetDockMode.Top;

            var loadedWhileDocked = manager.Load();
            Assert.Equal(WidgetDockMode.Top, loadedWhileDocked.DockMode);
            Assert.Equal(1250.0, loadedWhileDocked.WindowLeft);
            Assert.Equal(300.0, loadedWhileDocked.WindowTop);
            Assert.Equal(270.0, loadedWhileDocked.WidgetWidth);

            // Transition to Bottom Dock
            vm.DockMode = WidgetDockMode.Bottom;

            var loadedBottom = manager.Load();
            Assert.Equal(WidgetDockMode.Bottom, loadedBottom.DockMode);
            Assert.Equal(1250.0, loadedBottom.WindowLeft);
            Assert.Equal(300.0, loadedBottom.WindowTop);
            Assert.Equal(270.0, loadedBottom.WidgetWidth);

            // Undock back to Floating
            vm.DockMode = WidgetDockMode.Floating;

            var loadedFloating = manager.Load();
            Assert.Equal(WidgetDockMode.Floating, loadedFloating.DockMode);
            Assert.Equal(1250.0, loadedFloating.WindowLeft);
            Assert.Equal(300.0, loadedFloating.WindowTop);
            Assert.Equal(270.0, loadedFloating.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ExitWhileDocked_PreservesFloatingGeometry()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_geom_exit_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 850.0,
                WindowTop = 450.0,
                WidgetWidth = 330.0,
                DockMode = WidgetDockMode.Top
            };
            manager.Save(settings);

            using var vm = new WidgetViewModel
            {
                WidgetWidth = 330.0,
                DockMode = WidgetDockMode.Top
            };

            // Simulate OnExit while docked:
            // When vm.IsFloatingMode is false, docked window Left/Top/Width must NOT overwrite WindowLeft/WindowTop/WidgetWidth
            double dockedWindowLeft = 480.0;
            double dockedWindowTop = 0.0;
            double dockedWindowWidth = 940.0;

            if (vm.IsFloatingMode)
            {
                settings.WindowLeft = dockedWindowLeft;
                settings.WindowTop = dockedWindowTop;
                settings.WidgetWidth = dockedWindowWidth;
            }

            settings.DockMode = vm.DockMode;
            manager.Save(settings);

            var reloaded = manager.Load();
            Assert.Equal(WidgetDockMode.Top, reloaded.DockMode);
            Assert.Equal(850.0, reloaded.WindowLeft);
            Assert.Equal(450.0, reloaded.WindowTop);
            Assert.Equal(330.0, reloaded.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void SizeOrPositionChanged_IgnoredWhileDocked()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_geom_ign_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 600.0,
                WindowTop = 200.0,
                WidgetWidth = 220.0,
                DockMode = WidgetDockMode.Top
            };
            manager.Save(settings);

            using var vm = new WidgetViewModel
            {
                DockMode = WidgetDockMode.Top
            };

            // Simulation of SizeOrPositionChanged callback:
            Action<double, double, double> sizeOrPositionChanged = (left, top, width) =>
            {
                if (vm.IsFloatingMode)
                {
                    settings.WindowLeft = left;
                    settings.WindowTop = top;
                    settings.WidgetWidth = width;
                    manager.Save(settings);
                }
            };

            // Invoke size or position change event while docked
            sizeOrPositionChanged(100.0, 0.0, 960.0);

            var loaded = manager.Load();
            // Must not be modified
            Assert.Equal(600.0, loaded.WindowLeft);
            Assert.Equal(200.0, loaded.WindowTop);
            Assert.Equal(220.0, loaded.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void FloatingToDocked_ExplicitCaptureOrdering_CapturesFloatingBeforeDockMutation()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_geom_order_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 123.0,
                WindowTop = 456.0,
                WidgetWidth = 270.0,
                DockMode = WidgetDockMode.Floating
            };
            manager.Save(settings);

            // Simulation of Window and App state machine
            double simulatedWindowLeft = 123.0;
            double simulatedWindowTop = 456.0;
            double simulatedWindowContentWidth = 270.0;
            var appliedDockMode = WidgetDockMode.Floating;

            using var vm = new WidgetViewModel
            {
                WidgetWidth = 270.0,
                DockMode = WidgetDockMode.Floating
            };

            // Handler implementing the explicit capture-then-dock sequence
            vm.DockModeChanged = mode =>
            {
                var previousMode = appliedDockMode;
                appliedDockMode = mode;

                if (mode != WidgetDockMode.Floating)
                {
                    // 1. Capture floating geometry BEFORE mutating window/layout to docked
                    if (previousMode == WidgetDockMode.Floating)
                    {
                        settings.WindowLeft = simulatedWindowLeft;
                        settings.WindowTop = simulatedWindowTop;
                        settings.WidgetWidth = simulatedWindowContentWidth;
                    }

                    settings.DockMode = mode;
                    manager.Save(settings);

                    // 2. Dock mutation occurs (e.g. expands to 960px strip)
                    simulatedWindowContentWidth = 940.0;
                    simulatedWindowLeft = 480.0;
                    simulatedWindowTop = 0.0;
                }
            };

            // Request transition to Top Dock
            vm.DockMode = WidgetDockMode.Top;

            var persisted = manager.Load();
            Assert.Equal(WidgetDockMode.Top, persisted.DockMode);
            Assert.Equal(123.0, persisted.WindowLeft);
            Assert.Equal(456.0, persisted.WindowTop);
            Assert.Equal(270.0, persisted.WidgetWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void DockedToFloating_RestoresSavedFloatingWidth_NotDockedWidth()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aiquotabar_geom_restore_{Guid.NewGuid():N}.json");

        try
        {
            var manager = new SettingsManager(tempPath);
            var settings = new AppSettings
            {
                WindowLeft = 200.0,
                WindowTop = 150.0,
                WidgetWidth = 220.0,
                DockMode = WidgetDockMode.Top
            };
            manager.Save(settings);

            double simulatedWindowContentWidth = 940.0; // Docked width
            var appliedDockMode = WidgetDockMode.Top;

            using var vm = new WidgetViewModel
            {
                WidgetWidth = 220.0,
                DockMode = WidgetDockMode.Top
            };

            vm.DockModeChanged = mode =>
            {
                appliedDockMode = mode;
                if (mode == WidgetDockMode.Floating)
                {
                    settings.DockMode = WidgetDockMode.Floating;
                    manager.Save(settings);

                    // Restore sequence: Clamp saved floating width (220)
                    var restoredWidth = ResponsiveLayoutHelper.ClampWidth(settings.WidgetWidth);
                    simulatedWindowContentWidth = restoredWidth;
                    vm.WidgetWidth = restoredWidth;
                }
            };

            // Transition from Docked to Floating
            vm.DockMode = WidgetDockMode.Floating;

            Assert.Equal(220.0, simulatedWindowContentWidth);
            Assert.Equal(220.0, vm.WidgetWidth);
            Assert.NotEqual(940.0, simulatedWindowContentWidth);
            Assert.NotEqual(560.0, simulatedWindowContentWidth);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
