namespace AIQuotaBar.Providers.Codex.Tests;

using AIQuotaBar.Providers.Codex.Transport;
using Xunit;

public class StandardCodexProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesSessionAndClosesCleanly()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        string? echoLine = null;
        await runner.RunAsync(
            cmdPath,
            "/c echo hello_runner",
            async (session, ct) =>
            {
                echoLine = await session.ReadLineAsync(ct);
            },
            TimeSpan.FromSeconds(3));

        Assert.Equal("hello_runner", echoLine);
    }

    [Fact]
    public async Task RunAsync_InterruptsStuckReadAtTimeout()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // cmd.exe /c pause waits for stdin and never writes stdout on its own
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c pause > nul",
                async (session, token) =>
                {
                    // This read will block until token is cancelled by the runner timeout
                    await session.ReadLineAsync(token);
                },
                TimeSpan.FromMilliseconds(500));
        });

        sw.Stop();
        // Verify timeout interrupted the stuck read quickly (e.g. under 2.5s)
        Assert.True(sw.ElapsedMilliseconds < 2500, $"Expected timeout under 2500ms, took {sw.ElapsedMilliseconds}ms");
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
                async (session, token) =>
                {
                    await Task.Delay(5000, token);
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
                async (session, token) =>
                {
                    await Task.Delay(5000, token);
                },
                TimeSpan.FromSeconds(10),
                cts.Token);
        });
    }

    [Fact]
    public async Task RunAsync_DrainsStderrWithoutBlocking()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        string? stdoutLine = null;
        await runner.RunAsync(
            cmdPath,
            "/c echo msg_to_stderr 1>&2 & echo msg_to_stdout",
            async (session, token) =>
            {
                stdoutLine = await session.ReadLineAsync(token);
            },
            TimeSpan.FromSeconds(3));

        Assert.Equal("msg_to_stdout", stdoutLine);
    }
}
