using System.Diagnostics;
using System.Text;

var mode = args.FirstOrDefault() ?? "respond";
switch (mode)
{
    case "respond":
        using (var input = Console.OpenStandardInput())
        using (var reader = new StreamReader(input, Encoding.UTF8))
        {
            var line = await reader.ReadLineAsync();
            Console.WriteLine(line ?? "fixture-response");
        }
        break;
    case "stdout-unterminated":
        Console.Out.Write(new string('x', 1024 * 1024 + 1));
        Console.Out.Flush();
        break;
    case "stderr-flood":
        Console.Error.Write(new string('e', 1024 * 1024 + 1));
        Console.Error.Flush();
        await Task.Delay(Timeout.InfiniteTimeSpan);
        break;
    case "nonzero":
        Console.Error.WriteLine("secret-user-path=C:\\Users\\fixture\\token");
        Environment.ExitCode = 7;
        break;
    case "hang":
        await Task.Delay(Timeout.InfiniteTimeSpan);
        break;
    case "spawn-child":
        var pidFile = args.ElementAtOrDefault(1) ?? throw new ArgumentException("pid file required");
        using (var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"child-sentinel \"{pidFile}\"") { UseShellExecute = false, CreateNoWindow = true }))
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        break;
    case "child-sentinel":
        var childPidFile = args.ElementAtOrDefault(1) ?? throw new ArgumentException("pid file required");
        await File.WriteAllTextAsync(childPidFile, Environment.ProcessId.ToString());
        await Task.Delay(Timeout.InfiniteTimeSpan);
        break;
}
