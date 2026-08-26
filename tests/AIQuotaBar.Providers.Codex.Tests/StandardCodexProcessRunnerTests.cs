namespace AIQuotaBar.Providers.Codex.Tests;

using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class StandardCodexProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesSessionAndClosesCleanly()
    {
        var runner = new StandardCodexProcessRunner();
        // Use cmd.exe as a standard process to verify ICodexProcessRunner on Windows
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        string? echoLine = null;
        await runner.RunAsync(
            cmdPath,
            "/c echo hello_runner",
            async session =>
            {
                echoLine = await session.ReadLineAsync();
            },
            TimeSpan.FromSeconds(3));

        Assert.Equal("hello_runner", echoLine);
    }

    [Fact]
    public async Task RunAsync_TerminatesOnTimeout()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        // Ping -n 10 waits ~10 seconds
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c ping 127.0.0.1 -n 10 > nul",
                async session =>
                {
                    await Task.Delay(5000);
                },
                TimeSpan.FromMilliseconds(500));
        });
    }

    [Fact]
    public async Task RunAsync_TerminatesOnUserCancellation()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c ping 127.0.0.1 -n 10 > nul",
                async session =>
                {
                    await Task.Delay(5000, cts.Token);
                },
                TimeSpan.FromSeconds(10),
                cts.Token);
        });
    }
}
