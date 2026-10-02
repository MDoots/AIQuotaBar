using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AIQuotaBar.Providers.Codex.Normalization;
using AIQuotaBar.Providers.Codex.Protocol;
using AIQuotaBar.Providers.Codex.Transport;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

// Never print raw RPC, config, identities, command lines, ETW events or exception messages.
// Raw ETL stays in an ignored, local artifact directory and is parsed locally.
return await Entry.Run(args);

static class Entry
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));
    static string Output(string path)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo != null && !File.Exists(Path.Combine(repo.FullName, "AIQuotaBar.slnf"))) repo = repo.Parent;
        if (repo == null) throw new ArgumentException();
        var root = Path.Combine(repo.FullName, "artifacts") + Path.DirectorySeparatorChar;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
        Directory.CreateDirectory(path);
        return path;
    }
    static string Staging => Path.Combine(Environment.GetEnvironmentVariable("CODEX_HOME") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex"), ".tmp", "marketplaces", ".staging");
    static string[] Folders() => Directory.Exists(Staging)
        ? Directory.GetDirectories(Staging, "marketplace-upgrade-*").Select(Path.GetFileName).OfType<string>().ToArray() : [];
    internal static async Task<int> Run(string[] args)
    {
        try
        {
            switch (args.FirstOrDefault())
            {
                case "preflight":
                    Console.WriteLine(TraceEventSession.IsElevated() == true ? "Elevated; collector self-test still required." : "BLOCKED: Windows ETW requires an elevated PowerShell.");
                    return TraceEventSession.IsElevated() == true ? 0 : 3;
                case "capture" when args.Length == 3:
                    return await CaptureStreaming(Output(args[1]), int.Parse(args[2]));
                case "analyze" when args.Length == 2:
                    return Analyze(Output(args[1]));
                case "probe" when args.Length == 4:
                    return await Probe(Output(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]));
                case "provider" when args.Length == 4:
                    return await ProviderProbe(Output(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]));
                case "writer" when args.Length == 2:
                    using (var leaf = StartSelf("leaf", args[1]))
                    {
                        await leaf.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                        return leaf.ExitCode;
                    }
                case "leaf" when args.Length == 2:
                    // Only the harness-owned validation directory, never the shared Codex staging area.
                    Directory.CreateDirectory(args[1]);
                    await File.WriteAllTextAsync(Path.Combine(args[1], "marker.txt"), "ETW attribution self-test");
                    await File.WriteAllTextAsync(Path.Combine(args[1], "delete-marker.txt"), "ETW deletion self-test");
                    File.Delete(Path.Combine(args[1], "delete-marker.txt"));
                    return 0;
                default: return 2;
            }
        }
        catch
        {
            Console.Error.WriteLine("INCONCLUSIVE: diagnostic operation failed; no acceptance result.");
            return 1;
        }
    }

    static Process StartSelf(params string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info) ?? throw new InvalidOperationException();
    }

    static async Task<int> CaptureStreaming(string output, int seconds)
    {
        if (seconds is < 10 or > 900 || TraceEventSession.IsElevated() != true) return 3;
        var rawPath = Path.Combine(output, "events.jsonl");
        if (File.Exists(rawPath)) throw new InvalidOperationException();
        var baseline = Process.GetProcesses().Select(p =>
        {
            using (p) { try { return new Baseline(p.Id, p.StartTime.ToUniversalTime()); } catch { return null; } }
        }).OfType<Baseline>().ToArray();
        var baselineTimes = baseline.ToDictionary(p => p.Pid, p => p.StartedUtc);
        var began = DateTime.UtcNow;
        var name = "AIQuotaBar-Codex-" + Guid.NewGuid().ToString("N");
        using var writer = new StreamWriter(new FileStream(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        using var session = new TraceEventSession(name) { BufferSizeMB = 64, StopOnDispose = true };
        session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread |
            KernelTraceEventParser.Keywords.FileIO | KernelTraceEventParser.Keywords.FileIOInit |
            KernelTraceEventParser.Keywords.DiskFileIO);
        // Real-time processing persists only process lifetimes, relevant filesystem
        // events and pathless-event metadata. No system-wide paths/command lines.
        var source = session.Source;
        var kernel = source.Kernel;
        var staging = Path.GetFullPath(Staging).TrimEnd('\\') + "\\";
        var validation = Path.Combine(output, "validation").TrimEnd('\\') + "\\";
        long emitted = 0;
        var limitReached = false;
        var activeObjectNames = new Dictionary<ulong, FileNameLife>();
        var emittedObjectNames = new HashSet<ulong>();
        void Emit(TraceRow row)
        {
            if (emitted >= 1_000_000) { limitReached = true; return; }
            writer.WriteLine(JsonSerializer.Serialize(row));
            emitted++;
        }
        kernel.ProcessStartGroup += e => Emit(new("Start", new ProcessLife(e.ProcessID, e.ParentID,
            e.Opcode != TraceEventOpcode.Start && baselineTimes.TryGetValue(e.ProcessID, out var start) ? start : e.TimeStamp.ToUniversalTime(),
            Path.GetFileName(e.ImageFileName), e.Opcode != TraceEventOpcode.Start)));
        kernel.ProcessStop += e => Emit(new("End", new ProcessLife(e.ProcessID, 0, e.TimeStamp.ToUniversalTime(), "", false) { ExitStatus = e.ExitStatus }));
        void FileEvent(TraceEvent e, string file, string operation, ulong fileObject = 0, ulong fileKey = 0)
        {
            if (file.StartsWith(staging, StringComparison.OrdinalIgnoreCase))
                Emit(new("File", File: new(e.TimeStamp.ToUniversalTime(), e.ProcessID, e.ThreadID, operation, "Staging", file[staging.Length..])));
            else if (file.StartsWith(validation, StringComparison.OrdinalIgnoreCase))
                Emit(new("File", File: new(e.TimeStamp.ToUniversalTime(), e.ProcessID, e.ThreadID, operation, "SelfTest", file[validation.Length..])));
            else if (string.IsNullOrEmpty(file))
            {
                if (fileObject != 0 && activeObjectNames.TryGetValue(fileObject, out var objectName) && emittedObjectNames.Add(fileObject))
                    Emit(new("Name", Name: objectName));
                Emit(new("Unknown", Unknown: new(e.TimeStamp.ToUniversalTime(), e.ProcessID, operation, fileObject, fileKey, e.ThreadID)));
            }
        }
        void NameEvent(TraceEvent e, ulong identity, string identityKind, string transition, string file)
        {
            // Persist only the classification of unrelated names. No unrelated path
            // enters the log, but its object lifetime can still resolve a later write.
            var area = string.IsNullOrEmpty(file) ? "Unknown" : "OutsideStaging";
            var relative = "";
            if (file.StartsWith(staging, StringComparison.OrdinalIgnoreCase)) { area = "Staging"; relative = file[staging.Length..]; }
            else if (file.StartsWith(validation, StringComparison.OrdinalIgnoreCase)) { area = "SelfTest"; relative = file[validation.Length..]; }
            var nameRow = new FileNameLife(e.TimeStamp.ToUniversalTime(), identity, identityKind, transition, area, relative);
            if (identityKind == "Object")
            {
                if (transition == "End")
                {
                    activeObjectNames.Remove(identity);
                    if (emittedObjectNames.Remove(identity)) Emit(new("Name", Name: nameRow));
                }
                else
                {
                    if (activeObjectNames.Count >= 250_000 && !activeObjectNames.ContainsKey(identity)) { limitReached = true; return; }
                    activeObjectNames[identity] = nameRow;
                    if (emittedObjectNames.Contains(identity)) Emit(new("Name", Name: nameRow));
                }
            }
            else Emit(new("Name", Name: nameRow));
        }
        kernel.FileIOCreate += e =>
        {
            NameEvent(e, e.FileObject, "Object", "Create", e.FileName);
            FileEvent(e, e.FileName, "CreateOrOpen:" + e.CreateDisposition, e.FileObject);
        };
        kernel.FileIOClose += e => NameEvent(e, e.FileObject, "Object", "End", "");
        kernel.FileIOName += e => NameEvent(e, e.FileKey, "Key", "Name", e.FileName);
        kernel.FileIOFileCreate += e => NameEvent(e, e.FileKey, "Key", "Create", e.FileName);
        kernel.FileIOFileDelete += e => NameEvent(e, e.FileKey, "Key", "End", e.FileName);
        kernel.FileIOFileRundown += e => NameEvent(e, e.FileKey, "Key", "Rundown", e.FileName);
        kernel.FileIOWrite += e => FileEvent(e, e.FileName, "Write", e.FileObject, e.FileKey);
        kernel.FileIODelete += e => FileEvent(e, e.FileName, "Delete", e.FileObject, e.FileKey);
        kernel.FileIORename += e => FileEvent(e, e.FileName, "Rename", e.FileObject, e.FileKey);
        var pumping = Task.Run(() => source.Process());
        Save(Path.Combine(output, "capture-start.json"), new { Session = name, StartedUtc = began, Seconds = seconds,
            Baseline = baseline, InitialFolders = Folders(), Format = "FilteredRealtimeETW",
            FilenameStrategy = "Bounded object snapshots and key lifetime/rundown metadata" });
        var reason = "DurationCompleted";
        try
        {
            await Task.Delay(1000);
            using (var test = StartSelf("writer", Path.Combine(output, "validation")))
            {
                var start = test.StartTime.ToUniversalTime();
                try { await test.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
                catch { if (!test.HasExited) test.Kill(true); throw; }
                Save(Path.Combine(output, "self-test.json"), new { Pid = test.Id, StartedUtc = start, ExitCode = test.ExitCode });
            }
            File.WriteAllText(Path.Combine(output, "ready"), "Self-test emitted; analysis must validate it.");
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < seconds && !File.Exists(Path.Combine(output, "stop")))
            {
                await Task.Delay(500);
                if (pumping.IsCompleted) { reason = "ConsumerStoppedEarly"; break; }
                if (Volatile.Read(ref limitReached) || new FileInfo(rawPath).Length >= 240L * 1024 * 1024) { reason = "StorageBoundReached"; break; }
            }
            session.Flush();
            var lost = session.EventsLost;
            session.Stop();
            await pumping.WaitAsync(TimeSpan.FromSeconds(10));
            writer.Flush();
            Save(Path.Combine(output, "capture-end.json"), new { EndedUtc = DateTime.UtcNow, EventsLost = Math.Max(lost, source.EventsLost),
                Reason = reason, FinalFolders = Folders() });
        }
        finally
        {
            session.Stop(true);
            if (!pumping.IsCompleted) source.StopProcessing();
            await pumping.WaitAsync(TimeSpan.FromSeconds(10));
            writer.Flush();
        }
        writer.Dispose(); // Close the write handle before the offline reader opens it.
        return Analyze(output);
    }

    static async Task<int> Capture(string output, int seconds)
    {
        if (seconds is < 10 or > 900 || TraceEventSession.IsElevated() != true) return 3;
        if (File.Exists(Path.Combine(output, "trace.etl"))) throw new InvalidOperationException();
        var baseline = Process.GetProcesses().Select(p =>
        {
            using (p) { try { return new Baseline(p.Id, p.StartTime.ToUniversalTime()); } catch { return null; } }
        }).OfType<Baseline>().ToArray();
        var folders = Folders();
        var sessionName = "AIQuotaBar-Codex-" + Guid.NewGuid().ToString("N");
        using var session = new TraceEventSession(sessionName, Path.Combine(output, "trace.etl"))
        { BufferSizeMB = 64, MaximumFileMB = 512, StopOnDispose = true };
        session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread |
            KernelTraceEventParser.Keywords.FileIO | KernelTraceEventParser.Keywords.FileIOInit);
        var began = DateTime.UtcNow;
        Save(Path.Combine(output, "capture-start.json"), new { Session = sessionName, StartedUtc = began, Seconds = seconds, Baseline = baseline, InitialFolders = folders });
        await Task.Delay(1000);
        using (var writer = StartSelf("writer", Path.Combine(output, "validation")))
        {
            var start = writer.StartTime.ToUniversalTime();
            try { await writer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { if (!writer.HasExited) writer.Kill(true); throw; }
            Save(Path.Combine(output, "self-test.json"), new { Pid = writer.Id, StartedUtc = start, ExitCode = writer.ExitCode });
        }
        File.WriteAllText(Path.Combine(output, "ready"), "Self-test emitted; ETL analysis must validate it.");
        var watch = Stopwatch.StartNew();
        var reason = "DurationCompleted";
        while (watch.Elapsed < TimeSpan.FromSeconds(seconds) && !File.Exists(Path.Combine(output, "stop")))
        {
            await Task.Delay(500);
            if (new FileInfo(Path.Combine(output, "trace.etl")).Length >= 480L * 1024 * 1024)
            { reason = "StorageBoundReached"; break; }
        }
        session.Flush();
        var lost = session.EventsLost;
        session.Stop(); // Only this uniquely named owned session. No global WPR cancellation.
        Save(Path.Combine(output, "capture-end.json"), new { EndedUtc = DateTime.UtcNow, EventsLost = lost, Reason = reason, FinalFolders = Folders() });
        return Analyze(output);
    }

    static async Task<int> Probe(string output, string executable, int cycles)
    {
        if (cycles is < 1 or > 10 || !File.Exists(executable) || !File.Exists(Path.Combine(output, "ready"))) return 2;
        using var own = Process.GetCurrentProcess();
        Save(Path.Combine(output, "probe-root.json"), new { Pid = own.Id, StartedUtc = own.StartTime.ToUniversalTime(),
            ExecutableSHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))),
            Arguments = "--disable plugins app-server", WorkingDirectory = "OS temporary directory/AIQuotaBar/provider-runtime",
            CodexHomeOverridePresent = Environment.GetEnvironmentVariable("CODEX_HOME") != null });
        var runner = new StandardCodexProcessRunner();
        var clock = Stopwatch.StartNew();
        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            var due = TimeSpan.FromSeconds((cycle - 1) * 60) - clock.Elapsed;
            if (due > TimeSpan.Zero) await Task.Delay(due);
            var began = DateTime.UtcNow;
            CodexRateLimitsResult? limits = null;
            CodexAccountResult? account = null;
            var pluginsDisabled = false;
            var configuredMarketplaces = 0;
            var authenticated = false;
            var failed = false;
            var step = "initialize";
            string? failureKind = null;
            try
            {
                await runner.RunAsync(executable, "--disable plugins app-server", async (session, token) =>
                {
                    var client = new CodexJsonRpcClient(session);
                    await client.InitializeAsync("AIQuotaBarTrace", "1.0.5-experiment", token);
                    step = "config/read";
                    // Inspect only this single safe field in the official response; never save raw config.
                    var config = await client.SendRequestAsync<JsonElement>("config/read", new { includeLayers = false }, token);
                    pluginsDisabled = config.TryGetProperty("config", out var c) && c.TryGetProperty("features", out var f) &&
                        f.TryGetProperty("plugins", out var p) && p.ValueKind == JsonValueKind.False;
                    if (c.ValueKind == JsonValueKind.Object && c.TryGetProperty("marketplaces", out var marketplaces) && marketplaces.ValueKind == JsonValueKind.Object)
                        configuredMarketplaces = marketplaces.EnumerateObject().Count();
                    if (!pluginsDisabled) throw new InvalidOperationException();
                    step = "account/read";
                    account = await client.SendRequestAsync<CodexAccountResult>("account/read", null, token);
                    authenticated = account?.Account?.Type == "chatgpt";
                    step = "account/rateLimits/read";
                    limits = await client.SendRequestAsync<CodexRateLimitsResult>("account/rateLimits/read", null, token);
                    step = "shutdown";
                }, TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                failed = true;
                failureKind = ex switch
                {
                    TimeoutException => "Timeout", OperationCanceledException => "Cancelled",
                    EndOfStreamException => "ClosedStream", CodexRpcException => "RpcError",
                    _ => "CommunicationError"
                };
            }
            var snapshot = CodexUsageNormalizer.Normalize(limits, account);
            failed |= !authenticated || configuredMarketplaces == 0 || snapshot.Status != AIQuotaBar.Core.Models.ProviderStatus.Available;
            Save(Path.Combine(output, $"probe-{cycle}.json"), new { Cycle = cycle, StartedUtc = began, CompletedUtc = DateTime.UtcNow,
                PluginsDisabled = pluginsDisabled, ConfiguredMarketplaces = configuredMarketplaces, Authenticated = authenticated,
                Failed = failed, FailureKind = failureKind, LastStep = step, Status = snapshot.Status.ToString(),
                Windows = snapshot.Windows.Select(w => new { w.RemainingPercent, w.ResetsAt }),
                FreshRpcRead = !failed && pluginsDisabled && limits != null });
            // No retries or unsafe fallback if the configuration cannot be established.
            if (failed) return 1;
        }
        return 0;
    }

    static async Task<int> ProviderProbe(string output, string executable, int cycles)
    {
        if (cycles is < 1 or > 10) return 2;
        var observer = new ProviderSessionObserver();
        var provider = new AIQuotaBar.Providers.Codex.CodexUsageProvider(observer, () => executable);
        Save(Path.Combine(output, "probe-root.json"), new { Pid = Environment.ProcessId,
            StartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime(), Mode = "Actual provider with default six-second budget" });
        var schedule = Stopwatch.StartNew();
        var successful = 0;
        for (var cycle = 1; cycle <= cycles; cycle++)
        {
            var remaining = TimeSpan.FromSeconds((cycle - 1) * 60) - schedule.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);
            observer.Reset();
            var began = DateTime.UtcNow;
            var watch = Stopwatch.StartNew();
            var snapshot = await provider.GetUsageAsync();
            var result = new { Cycle = cycle, StartedUtc = began, CompletedUtc = DateTime.UtcNow,
                Status = snapshot.Status.ToString(), snapshot.StatusMessage, DurationMs = watch.ElapsedMilliseconds,
                observer.LastStep, observer.FailureKind, observer.CompletedMethods, observer.ConfiguredMarketplaces,
                FreshRpcRead = observer.CompletedMethods.Contains("account/rateLimits/read") &&
                    snapshot.Status == AIQuotaBar.Core.Models.ProviderStatus.Available,
                Windows = snapshot.Windows.Select(w => new { w.RemainingPercent, w.ResetsAt }) };
            Save(Path.Combine(output, $"provider-{cycle}.json"), result);
            Console.WriteLine(JsonSerializer.Serialize(result));
            if (result.FreshRpcRead && observer.ConfiguredMarketplaces > 0) successful++;
        }
        return successful == cycles ? 0 : 1;
    }

    static int Analyze(string output)
    {
        var start = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "capture-start.json")));
        var end = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "capture-end.json")));
        var test = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "self-test.json")));
        var baseline = start.RootElement.GetProperty("Baseline").EnumerateArray()
            .ToDictionary(b => b.GetProperty("Pid").GetInt32(), b => b.GetProperty("StartedUtc").GetDateTime());
        var processes = new List<ProcessLife>();
        var names = new List<FileNameLife>();
        var resolvedByObjectNames = 0;
        var resolvedByEndRundown = 0;
        var resolvedByInitialKeyClosure = 0;
        var files = new List<FileActivity>();
        var unresolvedFileNames = 0;
        var unknownFiles = new List<UnknownFileActivity>();
        var eventLimitReached = false;
        var sourceLost = 0;
        if (File.Exists(Path.Combine(output, "events.jsonl")))
        {
            foreach (var line in File.ReadLines(Path.Combine(output, "events.jsonl")))
            {
                var row = JsonSerializer.Deserialize<TraceRow>(line) ?? throw new InvalidDataException();
                switch (row.Kind)
                {
                    case "Start": processes.Add(row.Process!); break;
                    case "End":
                        var life = processes.LastOrDefault(p => p.Pid == row.Process!.Pid && p.EndedUtc == null);
                        if (life != null) { life.EndedUtc = row.Process!.StartedUtc; life.ExitStatus = row.Process.ExitStatus; }
                        break;
                    case "File": files.Add(row.File!); break;
                    case "Unknown": unknownFiles.Add(row.Unknown!); break;
                    case "Name": names.Add(row.Name!); break;
                }
            }
            unresolvedFileNames = unknownFiles.Count;
            var mappings = names.GroupBy(n => (n.IdentityKind, n.Identity)).ToDictionary(g => g.Key, g => g.OrderBy(n => n.AtUtc).ToArray());
            FileNameLife? ResolveName(string kind, ulong identity, DateTime at)
            {
                if (identity == 0 || !mappings.TryGetValue((kind, identity), out var lives)) return null;
                var life = lives.LastOrDefault(n => n.AtUtc <= at);
                // TraceEvent's HistoryDictionary backfills a first end-rundown name.
                // Apply that only to a key with no creation/deletion/name transitions
                // anywhere in this loss-free capture and one consistent rundown name.
                // Ordinary future names and every observed reuse remain rejected.
                if (life == null && kind == "Key" && lives.All(n => n.Transition == "Rundown" && n.Area != "Unknown" &&
                    n.Area == lives[0].Area && n.RelativePath == lives[0].RelativePath)) return lives[0];
                // The kernel parser also treats FileDelete name metadata as end
                // rundown. A first named closure bounds the initial key lifetime;
                // a later creation cannot retroactively rename that earlier life.
                if (life == null && kind == "Key" && lives[0] is { Transition: "End", Area: not "Unknown" }) return lives[0];
                return life is { Transition: not "End", Area: not "Unknown" } ? life : null;
            }
            var stillUnknown = new List<UnknownFileActivity>();
            foreach (var unknown in unknownFiles)
            {
                var byObject = ResolveName("Object", unknown.FileObject, unknown.AtUtc);
                var byKey = ResolveName("Key", unknown.FileKey, unknown.AtUtc);
                if (byObject != null && byKey != null && (byObject.Area != byKey.Area || byObject.RelativePath != byKey.RelativePath)) { stillUnknown.Add(unknown); continue; }
                var name = byObject ?? byKey;
                if (name == null) { stillUnknown.Add(unknown); continue; }
                if (name.Area is "Staging" or "SelfTest") files.Add(new(unknown.AtUtc, unknown.Pid, unknown.Tid, unknown.Operation, name.Area, name.RelativePath));
                resolvedByObjectNames++;
                if (name.Transition == "Rundown" && name.AtUtc > unknown.AtUtc) resolvedByEndRundown++;
                if (name.Transition == "End" && name.AtUtc > unknown.AtUtc) resolvedByInitialKeyClosure++;
            }
            unknownFiles = stillUnknown;
            unresolvedFileNames = unknownFiles.Count;
        }
        else
        {
        // ETLX preprocessing retains lifetime-indexed filename/thread maps, including
        // rundown arriving after the IO event. A streaming ETL pass cannot resolve that.
        using var log = TraceLog.OpenOrConvert(Path.Combine(output, "trace.etl"), new TraceLogOptions { ConversionLog = TextWriter.Null });
        using var source = log.Events.GetSource();
        var kernel = source.Kernel;
        kernel.ProcessStartGroup += e =>
        {
            var time = e.TimeStamp.ToUniversalTime();
            var rundown = e.Opcode != TraceEventOpcode.Start;
            processes.Add(new(e.ProcessID, e.ParentID, rundown && baseline.TryGetValue(e.ProcessID, out var b) ? b : time,
                Path.GetFileName(e.ImageFileName), rundown));
        };
        kernel.ProcessStop += e =>
        {
            var life = processes.LastOrDefault(p => p.Pid == e.ProcessID && p.EndedUtc == null);
            if (life != null) life.EndedUtc = e.TimeStamp.ToUniversalTime();
        };
        void FileEvent(TraceEvent e, string file, string operation)
        {
            if (files.Count >= 100_000) { eventLimitReached = true; return; }
            var staging = Path.GetFullPath(Staging).TrimEnd('\\') + "\\";
            var validation = Path.Combine(output, "validation").TrimEnd('\\') + "\\";
            if (file.StartsWith(staging, StringComparison.OrdinalIgnoreCase))
                files.Add(new(e.TimeStamp.ToUniversalTime(), e.ProcessID, e.ThreadID, operation, "Staging", file[staging.Length..]));
            else if (file.StartsWith(validation, StringComparison.OrdinalIgnoreCase))
                files.Add(new(e.TimeStamp.ToUniversalTime(), e.ProcessID, e.ThreadID, operation, "SelfTest", file[validation.Length..]));
            else if (string.IsNullOrEmpty(file)) unresolvedFileNames++;
        }
        kernel.FileIOCreate += e => FileEvent(e, e.FileName, "CreateOrOpen:" + e.CreateDisposition);
        kernel.FileIOWrite += e => FileEvent(e, e.FileName, "Write");
        kernel.FileIODelete += e => FileEvent(e, e.FileName, "Delete");
        kernel.FileIORename += e => FileEvent(e, e.FileName, "Rename");
        source.Process();
        sourceLost = source.EventsLost;
        }
        ProcessLife? Resolve(int pid, DateTime at) => processes.LastOrDefault(p => p.Pid == pid && p.StartedUtc <= at && (p.EndedUtc == null || p.EndedUtc >= at));
        bool Descendant(ProcessLife? process, int root, DateTime rootStart)
        {
            var seen = new HashSet<ProcessLife>();
            while (process != null && seen.Add(process))
            {
                if (process.Pid == root && Math.Abs((process.StartedUtc - rootStart).TotalSeconds) < 1) return true;
                process = Resolve(process.ParentPid, process.StartedUtc);
            }
            return false;
        }
        var testPid = test.RootElement.GetProperty("Pid").GetInt32();
        var testStart = test.RootElement.GetProperty("StartedUtc").GetDateTime();
        var validatedLeaf = processes.Where(p => p.ParentPid == testPid && Descendant(p, testPid, testStart)).Any(p =>
            files.Any(f => f.Area == "SelfTest" && f.Pid == p.Pid && f.Operation.StartsWith("CreateOrOpen")) &&
            files.Any(f => f.Area == "SelfTest" && f.Pid == p.Pid && f.Operation == "Write") &&
            files.Any(f => f.Area == "SelfTest" && f.Pid == p.Pid && f.Operation == "Delete"));
        var rootFile = Path.Combine(output, "probe-root.json");
        using var root = File.Exists(rootFile) ? JsonDocument.Parse(File.ReadAllText(rootFile)) : null;
        var rootPid = root?.RootElement.GetProperty("Pid").GetInt32() ?? -1;
        var rootStart = root?.RootElement.GetProperty("StartedUtc").GetDateTime() ?? DateTime.MinValue;
        var lost = Math.Max(sourceLost, end.RootElement.GetProperty("EventsLost").GetInt32());
        var activity = files.Where(f => f.Area == "Staging").Select(f =>
        {
            var life = Resolve(f.Pid, f.AtUtc);
            var owned = Descendant(life, rootPid, rootStart);
            return new { f.AtUtc, f.Pid, f.Tid, f.Operation,
                Folder = f.RelativePath.Split('\\')[0],
                Writer = life?.Name, StartedUtc = life?.StartedUtc, AttributableToProbe = owned, Unattributed = life == null };
        }).ToArray();
        var initial = start.RootElement.GetProperty("InitialFolders").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        var final = end.RootElement.GetProperty("FinalFolders").EnumerateArray().Select(e => e.GetString()).ToHashSet();
        var bounded = end.RootElement.GetProperty("Reason").GetString() == "StorageBoundReached";
        var rootCaptured = root == null || processes.Any(p => p.Pid == rootPid && Descendant(p, rootPid, rootStart));
        var unknownOwned = unknownFiles.Count(f => Descendant(Resolve(f.Pid, f.AtUtc), rootPid, rootStart));
        var unknownWriter = unknownFiles.Count(f => f.Pid <= 0 || Resolve(f.Pid, f.AtUtc) == null);
        var unmatchedFolders = final.Except(initial).Where(folder => !files.Any(f => f.Area == "Staging" &&
            f.RelativePath.TrimEnd('\\', '/') == folder && f.Operation is "CreateOrOpen:Create" or "CreateOrOpen:OpenIf" or "CreateOrOpen:OverwriteIf" or "CreateOrOpen:Supersede" &&
            Resolve(f.Pid, f.AtUtc) != null)).ToArray();
        var collectorValid = validatedLeaf && lost == 0 && !bounded && !eventLimitReached &&
            end.RootElement.GetProperty("Reason").GetString() == "DurationCompleted";
        // Global pathless activity stays explicitly inconclusive. It cannot be treated
        // as a helper write, nor silently counted as proven unrelated to staging.
        var globalValid = collectorValid && unresolvedFileNames == 0 && activity.All(e => !e.Unattributed) && unmatchedFolders.Length == 0;
        var helperValid = collectorValid && root != null && rootCaptured && unknownOwned == 0 && unknownWriter == 0 &&
            unresolvedFileNames == unknownFiles.Count &&
            activity.All(e => !e.Unattributed) && unmatchedFolders.Length == 0;
        var valid = root == null ? collectorValid : helperValid;
        Save(Path.Combine(output, "summary.json"), new { AttributionConclusive = globalValid, CollectorValidated = collectorValid,
            HelperAttributionConclusive = helperValid, SelfTestPassed = validatedLeaf, EventsLost = lost,
            UnresolvedFileNames = unresolvedFileNames, StorageBoundReached = bounded, EventLimitReached = eventLimitReached, ProbeRootCaptured = rootCaptured,
            ResolvedByObjectNames = resolvedByObjectNames,
            ResolvedByEndRundown = resolvedByEndRundown,
            ResolvedByInitialKeyClosure = resolvedByInitialKeyClosure,
            UnresolvedOwnedFileNames = unknownOwned, UnknownWriterFileEvents = unknownWriter, UnattributedNewFolders = unmatchedFolders,
            ProbePresent = root != null, GlobalStagingActivity = activity,
            ProbeStagingEventCount = activity.Count(e => e.AttributableToProbe),
            NewPersistentFolders = final.Except(initial).ToArray(),
            TransientFolders = activity.Select(e => e.Folder).Distinct().Where(f => !initial.Contains(f) && !final.Contains(f)).ToArray(),
            Processes = processes.Select(p => new { p.Pid, p.ParentPid, p.Name, p.StartedUtc, p.EndedUtc, p.ExitStatus, p.Rundown,
                AttributableToProbe = Descendant(p, rootPid, rootStart) }) });
        Console.WriteLine(valid ? "Collector self-test passed; review local summary and probe results." : "INCONCLUSIVE: collector validation, event loss, name resolution or attribution failed.");
        return valid ? 0 : 1;
    }
    record Baseline(int Pid, DateTime StartedUtc);
    sealed record ProcessLife(int Pid, int ParentPid, DateTime StartedUtc, string Name, bool Rundown)
    { public DateTime? EndedUtc { get; set; } public int? ExitStatus { get; set; } }
    record FileActivity(DateTime AtUtc, int Pid, int Tid, string Operation, string Area, string RelativePath);
    record UnknownFileActivity(DateTime AtUtc, int Pid, string Operation, ulong FileObject = 0, ulong FileKey = 0, int Tid = 0);
    record FileNameLife(DateTime AtUtc, ulong Identity, string IdentityKind, string Transition, string Area, string RelativePath);
    record TraceRow(string Kind, ProcessLife? Process = null, FileActivity? File = null, UnknownFileActivity? Unknown = null, FileNameLife? Name = null);
}
