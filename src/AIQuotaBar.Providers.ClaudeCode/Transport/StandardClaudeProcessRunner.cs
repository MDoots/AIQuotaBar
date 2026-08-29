namespace AIQuotaBar.Providers.ClaudeCode.Transport;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

public sealed class StandardClaudeProcessRunner : IClaudeProcessRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ClaudeAuthStatusResult?> CheckAuthStatusAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePath);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = "auth status --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start Claude process: '{executablePath}'");

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(stdout))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ClaudeAuthStatusResult>(stdout.Trim(), JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            throw new TimeoutException($"Claude auth check timed out after {timeout.TotalSeconds:0.##}s");
        }
        finally
        {
            if (process != null)
            {
                if (!process.HasExited)
                {
                    KillProcessTreeSafe(process);
                }
                process.Dispose();
            }
        }
    }

    public async Task<string> CaptureUsageAsync(
        string executablePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePath);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var neutralWorkingDir = Path.Combine(Path.GetTempPath(), "AIQuotaBar", "provider-runtime");
        try
        {
            Directory.CreateDirectory(neutralWorkingDir);
        }
        catch
        {
            neutralWorkingDir = Path.GetTempPath();
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = "",
            WorkingDirectory = neutralWorkingDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        Process? process = null;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start Claude process: '{executablePath}'");

            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            var sb = new StringBuilder();

            // Send /usage immediately
            await process.StandardInput.WriteLineAsync("/usage".AsMemory(), cts.Token).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cts.Token).ConfigureAwait(false);

            // Read output deterministically until usage markers and return-to-prompt or EOF are observed
            var buffer = new char[1024];
            var readDeadlineCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedReadCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, readDeadlineCts.Token);

            try
            {
                while (!linkedReadCts.IsCancellationRequested)
                {
                    var readTask = process.StandardOutput.ReadAsync(buffer, 0, buffer.Length);
                    var completedTask = await Task.WhenAny(readTask, Task.Delay(200, linkedReadCts.Token)).ConfigureAwait(false);
                    if (completedTask == readTask)
                    {
                        var charsRead = await readTask.ConfigureAwait(false);
                        if (charsRead <= 0) break;
                        sb.Append(buffer, 0, charsRead);
                        var current = sb.ToString();

                        // Detect usage panel completion and return to prompt
                        if ((current.Contains("limit", StringComparison.OrdinalIgnoreCase) ||
                             current.Contains("allowance", StringComparison.OrdinalIgnoreCase) ||
                             current.Contains("used", StringComparison.OrdinalIgnoreCase) ||
                             current.Contains("resets", StringComparison.OrdinalIgnoreCase) ||
                             current.Contains("not logged in", StringComparison.OrdinalIgnoreCase)) &&
                            (current.EndsWith("> ") || current.EndsWith(">") || current.Contains("\n> ") || current.Contains("\r\n> ") || current.Contains("claude>")))
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Bounded read completed
            }

            try
            {
                await process.StandardInput.WriteLineAsync("/exit".AsMemory(), cts.Token).ConfigureAwait(false);
                await process.StandardInput.FlushAsync(cts.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }
            catch
            {
                // Stdin already closed
            }

            var exitedCleanly = await WaitForExitAsync(process, TimeSpan.FromMilliseconds(800), cts.Token).ConfigureAwait(false);
            if (!exitedCleanly && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }

            return sb.ToString();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            throw;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            throw new TimeoutException($"Claude usage capture timed out after {timeout.TotalSeconds:0.##}s");
        }
        finally
        {
            if (process != null)
            {
                if (!process.HasExited)
                {
                    KillProcessTreeSafe(process);
                }
                process.Dispose();
            }
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            delayCts.CancelAfter(timeout);

            await process.WaitForExitAsync(delayCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return process.HasExited;
        }
    }

    private static void KillProcessTreeSafe(Process process)
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
            // Ignore
        }
    }
}
