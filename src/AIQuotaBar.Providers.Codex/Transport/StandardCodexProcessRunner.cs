namespace AIQuotaBar.Providers.Codex.Transport;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

public sealed class StandardCodexProcessRunner : ICodexProcessRunner
{
    private sealed class ProcessSession(StreamWriter writer, StreamReader reader) : ICodexProcessSession
    {
        private readonly BoundedLineReader _reader = new(reader);
        public async Task WriteLineAsync(string line, CancellationToken cancellationToken = default)
        {
            if (line.Length > ProcessIo.MaxOutputChars) throw new InvalidDataException("Provider request exceeded the supported limit.");
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => _reader.ReadLineAsync(cancellationToken);
    }

    public async Task RunAsync(string executablePath, string arguments,
        Func<ICodexProcessSession, CancellationToken, Task> sessionAction, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ProcessIo.ValidateExecutable(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(sessionAction);
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
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        startInfo.Arguments = arguments;
        Process? process = null;
        Task? sessionTask = null;
        Task? stderrTask = null;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start provider process.");
            stderrTask = ProcessIo.ReadBoundedAsync(process.StandardError, false, cts.Token);
            sessionTask = sessionAction(new ProcessSession(process.StandardInput, process.StandardOutput), cts.Token);
            await ProcessIo.CompleteAsync(sessionTask, stderrTask, cts.Token).ConfigureAwait(false);
            cts.Token.ThrowIfCancellationRequested();
            process.StandardInput.Close();
            // Graceful shutdown is inside the request budget; force cleanup adds at most one second.
            using var graceful = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            graceful.CancelAfter(TimeSpan.FromMilliseconds(500));
            try { await process.WaitForExitAsync(graceful.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (!cts.IsCancellationRequested) { }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            throw new TimeoutException("Codex process did not respond within the time limit.");
        }
        catch (Win32Exception) { throw new InvalidOperationException("Unable to start provider process."); }
        catch (IOException) { throw new IOException("Provider communication failed."); }
        finally
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await ProcessIo.CleanupAsync(process).ConfigureAwait(false);
            ProcessIo.Observe(sessionTask);
            ProcessIo.Observe(stderrTask);
        }
    }
}
