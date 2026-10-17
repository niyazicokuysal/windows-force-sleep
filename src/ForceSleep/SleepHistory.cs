using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Xml.Linq;

namespace ForceSleep;

/// <param name="WakeSource">Device name, or for timer wakes the reason (e.g. the scheduled task).</param>
/// <param name="WakeTimerOwner">Path of the program that set the timer.</param>
/// <param name="WakeTimerContext">For services hosted in svchost, the service name.</param>
internal sealed record SleepRecord(
    DateTime SleptAt, DateTime WokeAt, string? WakeSource, string? WakeTimerOwner, string? WakeTimerContext = null)
{
    public TimeSpan Duration => WokeAt - SleptAt;
}

/// <summary>Past sleeps from the System event log; readable without admin rights.</summary>
internal static class SleepHistory
{
    // "The system has returned from a low power state." Fields are read by name, not from the
    // message text, so this works on any Windows display language.
    private const string WakeQuery =
        "*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]";

    public static IReadOnlyList<SleepRecord> Recent(int max)
    {
        var records = new List<SleepRecord>();
        try
        {
            using var reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, WakeQuery)
            {
                ReverseDirection = true,
            });

            while (records.Count < max && reader.ReadEvent() is { } entry)
            {
                using (entry)
                {
                    if (Parse(entry.ToXml()) is { } record)
                        records.Add(record);
                }
            }
        }
        catch (EventLogException e)
        {
            Log.Write($"Could not read sleep history: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            Log.Write($"Could not read sleep history: {e.Message}");
        }

        return records;
    }

    public static SleepRecord? Parse(string eventXml)
    {
        var data = new Dictionary<string, string>();
        foreach (var element in XDocument.Parse(eventXml).Descendants().Where(e => e.Name.LocalName == "Data"))
        {
            if (element.Attribute("Name")?.Value is { } name)
                data[name] = element.Value;
        }

        if (!TryTime(data, "SleepTime", out var slept) || !TryTime(data, "WakeTime", out var woke))
            return null;

        return new SleepRecord(slept, woke,
            NonEmpty(data, "WakeSourceText"), NonEmpty(data, "WakeTimerOwner"), NonEmpty(data, "WakeTimerContext"));
    }

    private static bool TryTime(Dictionary<string, string> data, string key, out DateTime local)
    {
        local = default;
        if (!data.TryGetValue(key, out var text)
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time))
            return false;

        local = time.ToLocalTime();
        return true;
    }

    private static string? NonEmpty(Dictionary<string, string> data, string key) =>
        data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
