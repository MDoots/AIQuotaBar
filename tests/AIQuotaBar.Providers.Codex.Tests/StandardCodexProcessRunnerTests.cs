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

        // The runner must interrupt the stuck read and throw TimeoutException rather than hanging
        var runnerTask = Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c pause > nul",
                async (session, token) =>
                {
                    // This read will block indefinitely unless interrupted by the runner timeout
                    await session.ReadLineAsync(token);
                },
                TimeSpan.FromMilliseconds(500));
        });

        // Test harness emergency guard to ensure CI never hangs if there were a regression
        var guardTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(runnerTask, guardTask);

        Assert.Same(runnerTask, completedTask);
        await runnerTask;
    }

    [Fact]
    public async Task RunAsync_TerminatesOnTimeout()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        // Ping -n 10 waits ~10 seconds
        var runnerTask = Assert.ThrowsAsync<TimeoutException>(async () =>
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

        var guardTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(runnerTask, guardTask);

        Assert.Same(runnerTask, completedTask);
        await runnerTask;
    }

    [Fact]
    public async Task RunAsync_TerminatesOnUserCancellation()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        var runnerTask = Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
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

        var guardTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(runnerTask, guardTask);

        Assert.Same(runnerTask, completedTask);
        await runnerTask;
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

    [Fact]
    public async Task RunAsync_DistinguishesCallerCancellationFromTimeout()
    {
        var runner = new StandardCodexProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        // 1. Caller cancellation throws OperationCanceledException
        using var callerCts = new CancellationTokenSource();
        callerCts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c pause > nul",
                async (session, token) =>
                {
                    await session.ReadLineAsync(token);
                },
                TimeSpan.FromSeconds(5),
                callerCts.Token);
        });

        // 2. Runner timeout throws TimeoutException
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                "/c pause > nul",
                async (session, token) =>
                {
                    await session.ReadLineAsync(token);
                },
                TimeSpan.FromMilliseconds(200));
        });
    }
}
