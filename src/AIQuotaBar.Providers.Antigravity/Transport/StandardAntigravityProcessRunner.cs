namespace AIQuotaBar.Providers.Antigravity.Transport;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

public sealed class StandardAntigravityProcessRunner : IAntigravityProcessRunner
{
    public async Task<string> RunAsync(string executablePath, IReadOnlyList<string> arguments,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ProcessIo.ValidateExecutable(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        cancellationToken.ThrowIfCancellationRequested();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = ProcessIo.NeutralDirectory(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        Process? process = null;
        Task<string>? stdoutTask = null;
        Task<string>? stderrTask = null;
        Task? completion = null;
        try
        {
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start Antigravity process.");
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
            if (process.ExitCode != 0) throw new InvalidOperationException("Antigravity CLI returned an error.");
            return await stdoutTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && cts.IsCancellationRequested)
        {
            throw new TimeoutException("Antigravity process did not respond within the time limit.");
        }
        catch (Win32Exception) { throw new InvalidOperationException("Unable to start Antigravity process."); }
        catch (IOException) { throw new IOException("Antigravity communication failed."); }
        finally
        {
            await cts.CancelAsync().ConfigureAwait(false);
            await ProcessIo.CleanupAsync(process).ConfigureAwait(false);
            ProcessIo.Observe(stdoutTask);
            ProcessIo.Observe(stderrTask);
            ProcessIo.Observe(completion);
        }
    }
}
