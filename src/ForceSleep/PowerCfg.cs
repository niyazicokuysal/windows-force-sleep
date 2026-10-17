using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForceSleep;

internal enum RequestKind { Display, System, AwayMode, Execution, Other }

/// <summary>An app, service or driver asking Windows to stay awake (one entry of powercfg /requests).</summary>
/// <param name="Source">Friendly name to show the user.</param>
/// <param name="CallerType">PROCESS, SERVICE or DRIVER, as powercfg /requestsoverride expects it.</param>
/// <param name="OverrideName">The caller's name as powercfg /requestsoverride expects it.</param>
internal sealed record PowerRequest(RequestKind Kind, string Source, string? Reason, string CallerType, string OverrideName)
{
    /// <summary>The request type name used by powercfg, e.g. "AWAYMODE".</summary>
    public string RequestType => Kind switch
    {
        RequestKind.Display => "DISPLAY",
        RequestKind.System => "SYSTEM",
        RequestKind.AwayMode => "AWAYMODE",
        RequestKind.Execution => "EXECUTION",
        _ => "",
    };
}

internal enum ElevatedResult { Ok, Cancelled, Failed }

internal sealed record WakeTimer(string Owner, string? Reason);

/// <summary>Raw output of the powercfg commands that only work elevated.</summary>
internal sealed record ElevatedScan(string Requests, string WakeTimers);

/// <summary>Runs powercfg and turns its output into plain-language data.</summary>
internal static partial class PowerCfg
{
    private const int ErrorCancelled = 1223;

    private static readonly Dictionary<string, string> KnownPrograms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chrome"] = "Google Chrome",
        ["msedge"] = "Microsoft Edge",
        ["firefox"] = "Firefox",
        ["opera"] = "Opera",
        ["brave"] = "Brave",
        ["spotify"] = "Spotify",
        ["discord"] = "Discord",
        ["steam"] = "Steam",
        ["steamwebhelper"] = "Steam",
        ["vlc"] = "VLC",
        ["ms-teams"] = "Microsoft Teams",
        ["teams"] = "Microsoft Teams",
        ["zoom"] = "Zoom",
        ["obs64"] = "OBS Studio",
        ["onedrive"] = "OneDrive",
    };

    /// <summary>Devices allowed to wake the machine; works without admin rights.</summary>
    public static IReadOnlyList<string> WakeArmedDevices()
    {
        var output = Run("/devicequery wake_armed");
        return output
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Runs the admin-only queries in one elevated cmd (one UAC prompt) and returns their output,
    /// or null if the user declined the prompt.
    /// </summary>
    public static async Task<ElevatedScan?> ScanElevatedAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ForceSleep-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var requestsFile = Path.Combine(dir, "requests.txt");
        var timersFile = Path.Combine(dir, "waketimers.txt");

        var start = new ProcessStartInfo("cmd.exe",
            $"/c chcp 65001 >nul & powercfg /requests > \"{requestsFile}\" 2>&1 & powercfg /waketimers > \"{timersFile}\" 2>&1")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using (var process = Process.Start(start))
            {
                if (process is not null)
                    await process.WaitForExitAsync();
            }
            return new ElevatedScan(ReadIfExists(requestsFile), ReadIfExists(timersFile));
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
        }
    }

    public static IReadOnlyList<PowerRequest> ParseRequests(string output)
    {
        var requests = new List<PowerRequest>();
        RequestKind? kind = null;
        string? source = null;
        string? reason = null;

        void Flush()
        {
            if (kind is { } k && source is not null)
            {
                var (callerType, overrideName) = OverrideTarget(source);
                requests.Add(new PowerRequest(k, FriendlySource(source), reason, callerType, overrideName));
            }
            source = null;
            reason = null;
        }

        foreach (var line in Lines(output))
        {
            var header = CategoryHeader().Match(line);
            if (header.Success)
            {
                Flush();
                kind = header.Groups[1].Value switch
                {
                    "DISPLAY" => RequestKind.Display,
                    "SYSTEM" => RequestKind.System,
                    "AWAYMODE" => RequestKind.AwayMode,
                    "EXECUTION" => RequestKind.Execution,
                    _ => RequestKind.Other,
                };
            }
            else if (kind is null)
            {
                continue;
            }
            else if (line.StartsWith('['))
            {
                Flush();
                source = line;
            }
            else if (source is not null && reason is null && line.Length > 0)
            {
                reason = line;
            }
        }

        Flush();
        return requests;
    }

    public static IReadOnlyList<WakeTimer> ParseWakeTimers(string output)
    {
        var timers = new List<WakeTimer>();
        var lines = Lines(output).ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            var bracket = lines[i].IndexOf('[');
            if (bracket < 0)
                continue;

            var owner = TimerOwner().Match(lines[i], bracket);
            var ownerText = owner.Success ? owner.Value : lines[i][bracket..];
            var reasonLine = lines.Skip(i + 1).FirstOrDefault(l => l.Length > 0 && !l.Contains('['));
            timers.Add(new WakeTimer(FriendlySource(ownerText), FriendlyTimerReason(reasonLine)));
        }

        return timers;
    }

    /// <summary>
    /// "[PROCESS] \Device\...\chrome.exe" → "Google Chrome",
    /// "[SERVICE] ...svchost.exe (wuauserv)" → "Windows hizmeti: wuauserv",
    /// "[DRIVER] Realtek Audio (HDAUDIO\...)" → "Sürücü: Realtek Audio".
    /// </summary>
    public static string FriendlySource(string entry)
    {
        var close = entry.IndexOf(']');
        if (!entry.StartsWith('[') || close < 0)
            return entry.Trim();

        var type = entry[1..close];
        var rest = entry[(close + 1)..].Trim();

        switch (type)
        {
            case "SERVICE":
                var service = TrailingParenthesis().Match(rest);
                return service.Success ? L.Service(service.Groups[1].Value) : ProgramName(rest);
            case "DRIVER":
                var paren = rest.IndexOf(" (", StringComparison.Ordinal);
                return L.Driver(paren > 0 ? rest[..paren] : rest);
            case "PROCESS":
                return ProgramName(rest);
            default:
                return rest;
        }
    }

    /// <summary>
    /// The caller type and name powercfg /requestsoverride matches on: the executable name for
    /// processes, the service name for services and the full listed name for drivers.
    /// </summary>
    public static (string CallerType, string Name) OverrideTarget(string entry)
    {
        var close = entry.IndexOf(']');
        if (!entry.StartsWith('[') || close < 0)
            return ("", entry.Trim());

        var type = entry[1..close];
        var rest = entry[(close + 1)..].Trim();
        var name = type switch
        {
            "PROCESS" => ExeName().Matches(rest).LastOrDefault() is { } exe ? $"{exe.Groups[1].Value}.exe" : rest,
            "SERVICE" => TrailingParenthesis().Match(rest) is { Success: true } m ? m.Groups[1].Value : rest,
            _ => rest,
        };
        return (type, name);
    }

    /// <summary>
    /// Runs powercfg commands as administrator behind a single UAC prompt. Force Sleep itself is
    /// started elevated (see <see cref="RunAsElevatedChild"/>), so no shell ever parses the
    /// arguments and device or program names cannot inject commands.
    /// </summary>
    public static async Task<ElevatedResult> RunElevatedAsync(IReadOnlyList<string[]> commands)
    {
        var payload = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(commands));
        var start = new ProcessStartInfo(Environment.ProcessPath!, $"{ElevatedArgument} {payload}")
        {
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return ElevatedResult.Failed;
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? ElevatedResult.Ok : ElevatedResult.Failed;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return ElevatedResult.Cancelled;
        }
    }

    public const string ElevatedArgument = "--elevated-powercfg";

    private static readonly HashSet<string> AllowedElevatedVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "/devicedisablewake",
        "/deviceenablewake",
        "/requestsoverride",
    };

    /// <summary>
    /// The elevated side of <see cref="RunElevatedAsync"/>. Only a few powercfg verbs are allowed
    /// so the elevated instance cannot be used to run anything else. Returns the failure count.
    /// </summary>
    public static int RunAsElevatedChild(string payload)
    {
        string[][]? commands;
        try
        {
            commands = JsonSerializer.Deserialize<string[][]>(Convert.FromBase64String(payload));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            Log.Write($"Rejected elevated request: {e.Message}");
            return 1;
        }

        var failures = 0;
        foreach (var args in commands ?? [])
        {
            if (args.Length == 0 || !AllowedElevatedVerbs.Contains(args[0]) || args.Any(a => a.Any(char.IsControl)))
            {
                Log.Write($"Rejected elevated command: {string.Join(' ', args)}");
                failures++;
                continue;
            }

            var start = new ProcessStartInfo("powercfg.exe") { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args)
                start.ArgumentList.Add(arg);

            using var process = Process.Start(start)!;
            process.WaitForExit(30_000);
            var ok = process.HasExited && process.ExitCode == 0;
            Log.Write($"powercfg {string.Join(' ', args)}: {(ok ? "ok" : $"failed ({(process.HasExited ? process.ExitCode : -1)})")}");
            if (!ok)
                failures++;
        }

        return failures;
    }

    /// <summary>Recognizes device names from English or Turkish Windows.</summary>
    public static DeviceKind ClassifyDevice(string name)
    {
        bool Has(params string[] words) => words.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

        if (Has("keyboard", "klavye")) return DeviceKind.Keyboard;
        if (Has("mouse", "fare")) return DeviceKind.Mouse;
        if (IsNetworkDevice(name)) return DeviceKind.Network;
        if (Has("power button", "güç düğmesi")) return DeviceKind.PowerButton;
        if (Has("lid", "kapak")) return DeviceKind.Lid;
        if (Has("usb root hub", "usb kök")) return DeviceKind.Usb;
        return DeviceKind.Other;
    }

    /// <summary>A word a non-technical user knows, or the raw name if the device is unrecognized.</summary>
    public static string FriendlyDevice(string name) =>
        ClassifyDevice(name) is var kind and not DeviceKind.Other ? L.Device(kind) : name;

    public static bool IsNetworkDevice(string name) =>
        new[] { "ethernet", "gbe", "network", "wi-fi", "wifi", "wireless", "wlan", "ağ bağdaştırıcısı" }
            .Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>"\Device\...\svchost.exe" + "SystemEventsBroker" → "Windows hizmeti: SystemEventsBroker".</summary>
    public static string FriendlyProgram(string path, string? serviceName = null)
    {
        var name = ProgramName(path);
        return serviceName is not null && name.Equals("svchost", StringComparison.OrdinalIgnoreCase)
            ? L.Service(serviceName)
            : name;
    }

    private static string ProgramName(string path)
    {
        var exe = ExeName().Matches(path).LastOrDefault();
        if (exe is null)
            return path;

        var name = exe.Groups[1].Value;
        return KnownPrograms.TryGetValue(name, out var known) ? known : name;
    }

    public static string? FriendlyTimerReason(string? line)
    {
        if (line is null)
            return null;

        // "Reason: Windows will execute 'NT TASK\...\Backup Scan' scheduled task ..." → the task name.
        var task = ScheduledTask().Match(line);
        if (!task.Success)
            return line;

        var isUpdate = line.Contains("UpdateOrchestrator", StringComparison.OrdinalIgnoreCase)
                       || line.Contains("WindowsUpdate", StringComparison.OrdinalIgnoreCase);
        return L.ScheduledTask(task.Groups[1].Value, isUpdate);
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Replace("\r", "").Split('\n').Select(l => l.Trim());

    private static string Run(string arguments)
    {
        var start = new ProcessStartInfo("powercfg.exe", arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
        };

        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10_000);
            return output;
        }
        catch (Win32Exception e)
        {
            Log.Write($"powercfg {arguments} failed: {e.Message}");
            return "";
        }
    }

    private static string ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : "";

    [GeneratedRegex(@"^([A-Z]+):$")]
    private static partial Regex CategoryHeader();

    [GeneratedRegex(@"\[[A-Z]+\].*?\.exe(\s*\([^)]*\))?", RegexOptions.IgnoreCase)]
    private static partial Regex TimerOwner();

    [GeneratedRegex(@"\(([^)]+)\)\s*$")]
    private static partial Regex TrailingParenthesis();

    [GeneratedRegex(@"([^\\/]+)\.exe", RegexOptions.IgnoreCase)]
    private static partial Regex ExeName();

    [GeneratedRegex(@"'NT TASK\\(?:[^'\\]*\\)*([^'\\]+)'")]
    private static partial Regex ScheduledTask();
}
