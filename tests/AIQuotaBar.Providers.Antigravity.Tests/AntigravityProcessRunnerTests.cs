namespace AIQuotaBar.Providers.Antigravity.Tests;

using AIQuotaBar.Providers.Antigravity.Transport;
using Xunit;

public class AntigravityProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ExecutesProcessAndReturnsStdout()
    {
        var runner = new StandardAntigravityProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        var output = await runner.RunAsync(
            cmdPath,
            new[] { "/c", "echo", "hello_antigravity" },
            TimeSpan.FromSeconds(3));

        Assert.Contains("hello_antigravity", output);
    }

    [Fact]
    public async Task RunAsync_ThrowsTimeoutException_WhenProcessExceedsTimeout()
    {
        var runner = new StandardAntigravityProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                new[] { "/c", "ping", "127.0.0.1", "-n", "10", ">", "nul" },
                TimeSpan.FromMilliseconds(400));
        });
    }

    [Fact]
    public async Task RunAsync_ThrowsOperationCanceledException_WhenUserCancels()
    {
        var runner = new StandardAntigravityProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                new[] { "/c", "ping", "127.0.0.1", "-n", "10", ">", "nul" },
                TimeSpan.FromSeconds(10),
                cts.Token);
        });
    }

    [Fact]
    public async Task RunAsync_ThrowsInvalidOperationException_OnNonZeroExitCode()
    {
        var runner = new StandardAntigravityProcessRunner();
        var cmdPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await runner.RunAsync(
                cmdPath,
                new[] { "/c", "exit", "1" },
                TimeSpan.FromSeconds(3));
        });
    }
}
