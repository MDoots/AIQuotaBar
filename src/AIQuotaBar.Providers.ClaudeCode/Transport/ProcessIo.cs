using System.Diagnostics;
using System.Text;

namespace AIQuotaBar.Providers.ClaudeCode.Transport;

// Kept inside this provider assembly: no provider-to-provider dependency.
internal static class ProcessIo
{
    internal const int MaxOutputChars = 1024 * 1024;

    internal static string NeutralDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "AIQuotaBar", "provider-runtime");
        try { Directory.CreateDirectory(path); return path; }
        catch { return Path.GetTempPath(); }
    }

    internal static void ValidateExecutable(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (OperatingSystem.IsWindows() && !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("A native provider executable is required.");
    }

    internal static async Task<string> ReadBoundedAsync(StreamReader reader, bool capture, CancellationToken token)
    {
        var buffer = new char[4096];
        var output = capture ? new StringBuilder() : null;
        var count = 0;
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) != 0)
        {
            count += read;
            if (count > MaxOutputChars) throw new InvalidDataException("Provider output exceeded the supported limit.");
            output?.Append(buffer, 0, read);
        }
        return output?.ToString() ?? string.Empty;
    }

    // Observe both pumps immediately: a full stderr pipe or oversized unterminated
    // line must not wait behind a stalled stdout reader or process exit.
    internal static async Task CompleteAsync(Task action, Task stderr, CancellationToken token)
    {
        var first = await Task.WhenAny(action, stderr).WaitAsync(token).ConfigureAwait(false);
        await first.ConfigureAwait(false);
        await action.WaitAsync(token).ConfigureAwait(false);
        if (stderr.IsFaulted) await stderr.ConfigureAwait(false);
    }

    internal static async Task CleanupAsync(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
        }
        catch { /* Only this owned process is eligible for cleanup. */ }
        finally { process.Dispose(); }
    }

    internal static void Observe(Task? task)
    {
        if (task != null) _ = task.ContinueWith(t => _ = t.Exception,
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

internal sealed class BoundedLineReader(StreamReader reader)
{
    private readonly char[] _buffer = new char[4096];
    private int _offset;
    private int _length;
    private int _total;

    internal async Task<string?> ReadLineAsync(CancellationToken token)
    {
        var line = new StringBuilder();
        while (true)
        {
            if (_offset == _length)
            {
                _length = await reader.ReadAsync(_buffer.AsMemory(), token).ConfigureAwait(false);
                _offset = 0;
                if (_length == 0) return line.Length == 0 ? null : line.ToString().TrimEnd('\r');
                _total += _length;
                if (_total > 8 * ProcessIo.MaxOutputChars)
                    throw new InvalidDataException("Provider output exceeded the supported limit.");
            }
            var value = _buffer[_offset++];
            if (value == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= ProcessIo.MaxOutputChars)
                throw new InvalidDataException("Provider response exceeded the supported limit.");
            line.Append(value);
        }
    }
}

