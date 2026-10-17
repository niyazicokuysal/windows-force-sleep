using System.Globalization;
using System.Text.RegularExpressions;

namespace ForceSleep;

internal sealed record SleepAttempt(DateTime At, IReadOnlyList<(SleepMethod Method, StepOutcome Outcome)> Steps);

/// <summary>Reads Force Sleep's own past attempts back out of the log.</summary>
internal static partial class AttemptHistory
{
    public static SleepAttempt? Last()
    {
        try
        {
            return Last(File.ReadAllLines(Log.FilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static SleepAttempt? Last(IReadOnlyList<string> lines)
    {
        var start = -1;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].EndsWith(SleepController.AttemptMarker, StringComparison.Ordinal))
            {
                start = i;
                break;
            }
        }

        if (start < 0 || !TryTimestamp(lines[start], out var at))
            return null;

        var steps = new List<(SleepMethod, StepOutcome)>();
        foreach (var line in lines.Skip(start + 1))
        {
            var step = StepLine().Match(line);
            if (step.Success
                && Enum.TryParse<SleepMethod>(step.Groups["method"].Value, out var method)
                && Enum.TryParse<StepOutcome>(step.Groups["outcome"].Value, out var outcome))
                steps.Add((method, outcome));
        }

        return new SleepAttempt(at, steps);
    }

    private static bool TryTimestamp(string line, out DateTime at)
    {
        at = default;
        return line.Length >= Log.TimestampFormat.Length
            && DateTime.TryParseExact(line[..Log.TimestampFormat.Length], Log.TimestampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out at);
    }

    [GeneratedRegex(@"^\S+ \S+  (?<method>\w+): (?<outcome>\w+)")]
    private static partial Regex StepLine();
}
