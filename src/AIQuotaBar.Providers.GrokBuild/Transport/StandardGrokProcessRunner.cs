namespace AIQuotaBar.Providers.GrokBuild.Transport;

using System.Diagnostics;
using System.Text;

public sealed class StandardGrokProcessRunner : IGrokProcessRunner
{
    private sealed class ProcessSession : IGrokProcessSession
    {
        private readonly StreamWriter _writer;
        private readonly StreamReader _reader;

        public ProcessSession(StreamWriter writer, StreamReader reader)
        {
            _writer = writer;
            _reader = reader;
        }

        public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
        {
            return await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        Func<IGrokProcessSession, CancellationToken, Task> sessionAction,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(sessionAction);

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

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process? process = null;
        Task? sessionTask = null;

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start Grok process: '{executablePath}'");

            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            var session = new ProcessSession(process.StandardInput, process.StandardOutput);

            sessionTask = sessionAction(session, cts.Token);

            // Authoritative runner-level bounded await to prevent hanging on stuck pipe reads
            await sessionTask.WaitAsync(cts.Token).ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Grok process timed out after {timeout.TotalSeconds:0.##}s");
            }

            try
            {
                process.StandardInput.Close();
            }
            catch
            {
                // Process may already have closed stdin
            }

            var exitedCleanly = await WaitForExitAsync(process, TimeSpan.FromMilliseconds(500), cts.Token).ConfigureAwait(false);
            if (!exitedCleanly && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            if (sessionTask != null)
            {
                _ = sessionTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            throw;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            if (sessionTask != null)
            {
                _ = sessionTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            throw new TimeoutException($"Grok process timed out after {timeout.TotalSeconds:0.##}s");
        }
        catch (TimeoutException)
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            if (sessionTask != null)
            {
                _ = sessionTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            throw;
        }
        catch
        {
            if (process != null && !process.HasExited)
            {
                KillProcessTreeSafe(process);
            }
            if (sessionTask != null)
            {
                _ = sessionTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
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
            // Ignore failure if process has already exited
        }
    }
}
