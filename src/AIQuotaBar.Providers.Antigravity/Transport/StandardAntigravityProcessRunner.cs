namespace AIQuotaBar.Providers.Antigravity.Transport;

using System.Diagnostics;
using System.Text;

public sealed class StandardAntigravityProcessRunner : IAntigravityProcessRunner
{
    public async Task<string> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path must not be null or empty", nameof(executablePath));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (stdoutBuilder)
                {
                    stdoutBuilder.AppendLine(e.Data);
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                lock (stderrBuilder)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            }
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start Antigravity process");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var stderr = stderrBuilder.ToString().Trim();
                var stdout = stdoutBuilder.ToString().Trim();
                var message = !string.IsNullOrWhiteSpace(stdout)
                    ? stdout
                    : (!string.IsNullOrWhiteSpace(stderr) ? stderr : $"Process exited with code {process.ExitCode}");

                throw new InvalidOperationException($"Antigravity CLI process failed: {message}");
            }

            return stdoutBuilder.ToString();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            KillProcessSafely(process);
            throw new TimeoutException($"Antigravity CLI process timed out after {timeout.TotalSeconds} seconds");
        }
        catch
        {
            KillProcessSafely(process);
            throw;
        }
    }

    private static void KillProcessSafely(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may already have terminated
        }
    }
}
