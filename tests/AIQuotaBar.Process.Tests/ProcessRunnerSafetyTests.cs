namespace AIQuotaBar.Process.Tests;

using AIQuotaBar.Providers.Antigravity.Transport;
using AIQuotaBar.Providers.ClaudeCode.Transport;
using AIQuotaBar.Providers.Codex.Transport;
using AIQuotaBar.Providers.GrokBuild.Transport;

public sealed class ProcessRunnerSafetyTests
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "AIQuotaBar.ProcessFixture.exe");

    [Fact]
    public async Task AntigravityRejectsOversizedUnterminatedStdout()
    {
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => new StandardAntigravityProcessRunner().RunAsync(Fixture, ["stdout-unterminated"], TimeSpan.FromSeconds(3)));
        Assert.Contains("output", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AntigravityDrainsStderrFloodAndTimesOut()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => new StandardAntigravityProcessRunner().RunAsync(Fixture, ["stderr-flood"], TimeSpan.FromSeconds(2)));
        Assert.True(ex is TimeoutException or InvalidDataException, $"Unexpected process safety exception: {ex.GetType().Name}");
    }

    [Fact]
    public async Task AntigravityRedactsNonzeroProcessOutput()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new StandardAntigravityProcessRunner().RunAsync(Fixture, ["nonzero"], TimeSpan.FromSeconds(3)));
        Assert.DoesNotContain("fixture", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CodexAndGrokRejectNativeShims()
    {
        if (!OperatingSystem.IsWindows()) return;
        await Assert.ThrowsAsync<NotSupportedException>(() => new StandardCodexProcessRunner().RunAsync("fixture.cmd", "", (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<NotSupportedException>(() => new StandardGrokProcessRunner().RunAsync("fixture.cmd", [], (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task CodexAndGrokExchangeThroughFixtureSession()
    {
        await new StandardCodexProcessRunner().RunAsync(Fixture, "respond", async (session, token) =>
        {
            await session.WriteLineAsync("codex-ok", token);
            Assert.Equal("codex-ok", await session.ReadLineAsync(token));
        }, TimeSpan.FromSeconds(3));
        await new StandardGrokProcessRunner().RunAsync(Fixture, ["respond"], async (session, token) =>
        {
            await session.WriteLineAsync("grok-ok", token);
            Assert.Equal("grok-ok", await session.ReadLineAsync(token));
        }, TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task CodexTimeoutKillsOwnedChild()
    {
        using var unrelated = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Fixture, "hang")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var pidFile = Path.Combine(Path.GetTempPath(), $"aiquotabar-child-{Guid.NewGuid():N}.txt");
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => new StandardCodexProcessRunner().RunAsync(Fixture, $"spawn-child \"{pidFile}\"", (_, _) => Task.Delay(Timeout.InfiniteTimeSpan), TimeSpan.FromSeconds(1)));
            var pid = int.Parse(await WaitForFileAsync(pidFile));
            await Task.Delay(100);
            Assert.False(IsRunning(pid));
            Assert.False(unrelated.HasExited);
        }
        finally
        {
            TryDelete(pidFile);
            if (!unrelated.HasExited) unrelated.Kill(entireProcessTree: true);
            await unrelated.WaitForExitAsync();
        }
    }

    [Fact]
    public async Task CodexRejectsOversizedFrameWithoutWaitingForNewline()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => new StandardCodexProcessRunner().RunAsync(
            Fixture, "stdout-unterminated", async (session, token) => { await session.ReadLineAsync(token); }, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task GrokCancellationIsBoundedAndRemainsCancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new StandardGrokProcessRunner().RunAsync(
            Fixture, ["hang"], async (session, token) => { await session.ReadLineAsync(token); }, TimeSpan.FromSeconds(10), cancel.Token));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task ClaudeUsageCaptureIsUnsupportedWithoutLaunchingFixture()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => new StandardClaudeProcessRunner().CaptureUsageAsync(Fixture, TimeSpan.FromSeconds(1)));
        Assert.Contains("unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> WaitForFileAsync(string path)
    {
        for (var i = 0; i < 50; i++)
        {
            if (File.Exists(path)) return await File.ReadAllTextAsync(path);
            await Task.Delay(20);
        }
        throw new Xunit.Sdk.XunitException("Fixture child did not publish its PID.");
    }

    private static bool IsRunning(int pid)
    {
        try { using var p = System.Diagnostics.Process.GetProcessById(pid); return !p.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static void TryDelete(string path) { try { File.Delete(path); } catch { } }
}
