namespace AIQuotaBar.Providers.ClaudeCode.Transport;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public sealed partial class StandardClaudeProcessRunner : IClaudeProcessRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled)]
    private static partial Regex AnsiRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%\s*(?:used|consumed)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UsedPercentRegex();

    [GeneratedRegex(@"(?:used|consumed)\s*[:=]\s*(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UsedColonPercentRegex();

    [GeneratedRegex(@"(\d+(?:\.\d+)?)\s*%\s*(?:remaining|left)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RemainingPercentRegex();

    [GeneratedRegex(@"(?:remaining|left)\s*[:=]\s*(\d+(?:\.\d+)?)\s*%", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RemainingColonPercentRegex();

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
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        startInfo.ArgumentList.Add("auth");
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--json");

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
            throw new TimeoutException($"Claude auth check timed out after {timeout.TotalSeconds:0.##}s");
        }
        catch
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            throw;
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

            // Single outstanding read state machine
            var buffer = new char[1024];
            var isCompleted = false;

            while (!cts.IsCancellationRequested)
            {
                var charsRead = await process.StandardOutput.ReadAsync(buffer.AsMemory(), cts.Token).ConfigureAwait(false);
                if (charsRead <= 0)
                {
                    break;
                }

                sb.Append(buffer, 0, charsRead);
                var current = sb.ToString();

                if (IsUsagePanelComplete(current))
                {
                    isCompleted = true;
                    break;
                }
            }

            if (!isCompleted && !cancellationToken.IsCancellationRequested)
            {
                // Partial output must never be accepted as valid quota
                throw new TimeoutException($"Claude /usage capture did not complete within {timeout.TotalSeconds:0.##}s");
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

            var exitedCleanly = await WaitForExitAsync(process, TimeSpan.FromMilliseconds(500), cts.Token).ConfigureAwait(false);
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
        catch
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            throw;
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

    public static int FindUnderstoodContentEndIndex(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }

        var maxEndIndex = -1;

        var m1 = UsedPercentRegex().Match(text);
        if (m1.Success) maxEndIndex = Math.Max(maxEndIndex, m1.Index + m1.Length);

        var m2 = UsedColonPercentRegex().Match(text);
        if (m2.Success) maxEndIndex = Math.Max(maxEndIndex, m2.Index + m2.Length);

        var m3 = RemainingPercentRegex().Match(text);
        if (m3.Success) maxEndIndex = Math.Max(maxEndIndex, m3.Index + m3.Length);

        var m4 = RemainingColonPercentRegex().Match(text);
        if (m4.Success) maxEndIndex = Math.Max(maxEndIndex, m4.Index + m4.Length);

        string[] statusPhrases =
        {
            "not logged in",
            "please run /login",
            "run `claude login`",
            "authentication required",
            "api key",
            "pay-as-you-go",
            "usage-based",
            "no subscription"
        };

        foreach (var phrase in statusPhrases)
        {
            var idx = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                maxEndIndex = Math.Max(maxEndIndex, idx + phrase.Length);
            }
        }

        return maxEndIndex;
    }

    public static bool IsUsagePanelComplete(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = AnsiRegex().Replace(text, " ");
        var usageEndIndex = FindUnderstoodContentEndIndex(cleaned);
        if (usageEndIndex < 0)
        {
            return false;
        }

        // Return prompt marker MUST occur AFTER the recognized usage content
        var postUsage = cleaned[usageEndIndex..];

        return postUsage.EndsWith("> ") ||
               postUsage.EndsWith(">") ||
               postUsage.Contains("\n> ") ||
               postUsage.Contains("\r\n> ") ||
               postUsage.Contains("claude>");
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
