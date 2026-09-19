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
        string executablePath, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ProcessIo.ValidateExecutable(executablePath);
        cancellationToken.ThrowIfCancellationRequested();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = ProcessIo.NeutralDirectory(),
            RedirectStandardInput = true,
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
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        Task? completion = null;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Claude Code.");
            process.StandardInput.Close();
            stdoutTask = ProcessIo.ReadBoundedAsync(process.StandardOutput, true, cts.Token);
            stderrTask = ProcessIo.ReadBoundedAsync(process.StandardError, false, cts.Token);
            async Task FinishAsync()
            {
                await stdoutTask.ConfigureAwait(false);
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                await stderrTask.ConfigureAwait(false);
            }
            completion = FinishAsync();
            await ProcessIo.CompleteAsync(completion, stderrTask, cts.Token).ConfigureAwait(false);
            try
            {
                // Signed-out status can use a nonzero exit code with valid JSON.
                return JsonSerializer.Deserialize<ClaudeAuthStatusResult>(await stdoutTask.ConfigureAwait(false), JsonOptions);
            }
            catch (JsonException) { return null; }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            throw new TimeoutException("Claude auth check did not respond within the time limit.");
        }
        catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Unable to start Claude Code."); }
        catch (IOException) { throw new IOException("Claude Code communication failed."); }
        finally
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await ProcessIo.CleanupAsync(process).ConfigureAwait(false);
            ProcessIo.Observe(stdoutTask);
            ProcessIo.Observe(stderrTask);
            ProcessIo.Observe(completion);
        }
    }

    public Task<string> CaptureUsageAsync(string executablePath, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // /usage is documented for an interactive terminal, not as a safe
        // unattended pipe protocol. Never start a model session to obtain quota.
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<string>(cancellationToken);
        return Task.FromException<string>(new NotSupportedException(
            "Automatic quota is unavailable; view /usage in Claude Code."));
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
