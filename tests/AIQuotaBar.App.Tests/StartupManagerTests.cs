namespace AIQuotaBar.App.Tests;

using AIQuotaBar.App.Platform;
using AIQuotaBar.App.Settings;
using Windows.ApplicationModel;
using Xunit;

public class StartupManagerTests
{
    // ==========================================
    // UNPACKAGED / REGISTRY TESTS
    // ==========================================

    [Fact]
    public void SetStartup_SetsQuotedPath_WhenEnabled()
    {
        string? storedKey = null;
        string? storedValue = null;

        var success = StartupManager.SetStartup(
            enable: true,
            executablePath: @"C:\Program Files\AIQuotaBar\AIQuotaBar.App.exe",
            setRegistryValue: (key, val) =>
            {
                storedKey = key;
                storedValue = val;
            });

        Assert.True(success);
        Assert.Equal("AIQuotaBar", storedKey);
        Assert.Equal("\"C:\\Program Files\\AIQuotaBar\\AIQuotaBar.App.exe\"", storedValue);
    }

    [Fact]
    public void SetStartup_DeletesEntry_WhenDisabled()
    {
        string? deletedKey = null;

        var success = StartupManager.SetStartup(
            enable: false,
            deleteRegistryValue: key => deletedKey = key);

        Assert.True(success);
        Assert.Equal("AIQuotaBar", deletedKey);
    }

    [Fact]
    public void SetStartup_FailsGracefully_WhenPathIsNotExe()
    {
        var success = StartupManager.SetStartup(
            enable: true,
            executablePath: @"C:\some\script.bat");

        Assert.False(success);
    }

    [Fact]
    public void IsStartupEnabled_ReturnsTrue_WhenValueIsPresent()
    {
        var isEnabled = StartupManager.IsStartupEnabled(
            getRegistryValue: key => key == "AIQuotaBar" ? "\"C:\\app.exe\"" : null);

        Assert.True(isEnabled);
    }

    [Fact]
    public void IsStartupEnabled_ReturnsFalse_WhenValueIsEmptyOrNull()
    {
        var isEnabled = StartupManager.IsStartupEnabled(getRegistryValue: _ => null);
        Assert.False(isEnabled);
    }

    // ==========================================
    // PACKAGED STARTUP HANDLER TESTS
    // ==========================================

    [Theory]
    [InlineData(StartupTaskState.Enabled, true)]
    [InlineData(StartupTaskState.EnabledByPolicy, true)]
    [InlineData(StartupTaskState.Disabled, false)]
    [InlineData(StartupTaskState.DisabledByUser, false)]
    [InlineData(StartupTaskState.DisabledByPolicy, false)]
    public async Task PackagedStartupHandler_IsStartupEnabledAsync_MapsStatesCorrectly(StartupTaskState state, bool expected)
    {
        var mockProxy = new MockStartupTaskProxy { State = state };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var isEnabled = await handler.IsStartupEnabledAsync();
        Assert.Equal(expected, isEnabled);
    }

    [Fact]
    public async Task PackagedStartupHandler_IsStartupEnabledAsync_ReturnsFalse_WhenTaskNotFound()
    {
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(null));
        var isEnabled = await handler.IsStartupEnabledAsync();
        Assert.False(isEnabled);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Enable_WhenDisabled_RequestsEnableAndSucceeds()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.Disabled,
            RequestEnableResult = StartupTaskState.Enabled
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(true);

        Assert.True(success);
        Assert.True(mockProxy.RequestEnableCalled);
        Assert.Equal(StartupTaskState.Enabled, mockProxy.State);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Enable_WhenDisabledByUser_DoesNotRequestAndReturnsFalse()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.DisabledByUser
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(true);

        Assert.False(success);
        Assert.False(mockProxy.RequestEnableCalled);
        Assert.Equal(StartupTaskState.DisabledByUser, mockProxy.State);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Enable_WhenDisabledByPolicy_DoesNotRequestAndReturnsFalse()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.DisabledByPolicy
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(true);

        Assert.False(success);
        Assert.False(mockProxy.RequestEnableCalled);
        Assert.Equal(StartupTaskState.DisabledByPolicy, mockProxy.State);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Enable_WhenAlreadyEnabled_ReturnsTrueWithoutRequest()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.Enabled
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(true);

        Assert.True(success);
        Assert.False(mockProxy.RequestEnableCalled);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Disable_WhenEnabled_DisablesSuccessfully()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.Enabled
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(false);

        Assert.True(success);
        Assert.True(mockProxy.DisableCalled);
        Assert.Equal(StartupTaskState.Disabled, mockProxy.State);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Disable_WhenEnabledByPolicy_FailsToDisable()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.EnabledByPolicy
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(false);

        Assert.False(success);
        Assert.False(mockProxy.DisableCalled);
        Assert.Equal(StartupTaskState.EnabledByPolicy, mockProxy.State);
    }

    [Fact]
    public async Task PackagedStartupHandler_SetStartupAsync_Disable_WhenAlreadyDisabled_ReturnsTrue()
    {
        var mockProxy = new MockStartupTaskProxy
        {
            State = StartupTaskState.DisabledByUser
        };
        var handler = new PackagedStartupHandler(_ => Task.FromResult<IStartupTaskProxy?>(mockProxy));

        var success = await handler.SetStartupAsync(false);

        Assert.True(success);
        Assert.False(mockProxy.DisableCalled);
    }

    // ==========================================
    // ROUTING / FACTORY TESTS
    // ==========================================

    [Fact]
    public void StartupManager_GetDefaultHandler_ReturnsPackagedHandler_WhenPackaged()
    {
        var mockIdentity = new MockPackageIdentity { IsPackaged = true };
        var handler = StartupManager.GetDefaultHandler(mockIdentity);

        Assert.IsType<PackagedStartupHandler>(handler);
    }

    [Fact]
    public void StartupManager_GetDefaultHandler_ReturnsRegistryHandler_WhenUnpackaged()
    {
        var mockIdentity = new MockPackageIdentity { IsPackaged = false };
        var handler = StartupManager.GetDefaultHandler(mockIdentity);

        Assert.IsType<RegistryStartupHandler>(handler);
    }

    [Fact]
    public async Task StartupManager_DelegatesToCustomHandler_Async()
    {
        var mockHandler = new MockStartupHandler { Enabled = true };

        var isEnabled = await StartupManager.IsStartupEnabledAsync(mockHandler);
        Assert.True(isEnabled);

        var setSuccess = await StartupManager.SetStartupAsync(false, mockHandler);
        Assert.True(setSuccess);
        Assert.False(mockHandler.Enabled);
    }

    // ==========================================
    // TEST DOUBLES / MOCKS
    // ==========================================

    private sealed class MockStartupTaskProxy : IStartupTaskProxy
    {
        public StartupTaskState State { get; set; }
        public StartupTaskState RequestEnableResult { get; set; } = StartupTaskState.Enabled;
        public bool RequestEnableCalled { get; private set; }
        public bool DisableCalled { get; private set; }

        public Task<StartupTaskState> RequestEnableAsync()
        {
            RequestEnableCalled = true;
            State = RequestEnableResult;
            return Task.FromResult(RequestEnableResult);
        }

        public void Disable()
        {
            DisableCalled = true;
            if (State != StartupTaskState.EnabledByPolicy)
            {
                State = StartupTaskState.Disabled;
            }
        }
    }

    private sealed class MockPackageIdentity : IPackageIdentity
    {
        public bool IsPackaged { get; set; }
    }

    private sealed class MockStartupHandler : IStartupHandler
    {
        public bool Enabled { get; set; }

        public Task<bool> IsStartupEnabledAsync() => Task.FromResult(Enabled);

        public Task<bool> SetStartupAsync(bool enable)
        {
            Enabled = enable;
            return Task.FromResult(true);
        }
    }
}
