namespace ForceSleep;

/// <summary>A one-click fix offered in the "Neden uyumadı?" window.</summary>
internal abstract record Fix;

/// <summary>Stop these devices from waking the machine (admin).</summary>
internal sealed record DisableDeviceWakeFix(IReadOnlyList<string> Devices) : Fix
{
    /// <summary>Turning off keyboard/mouse wake changes how the user wakes the machine, so ask first.</summary>
    public bool NeedsConfirmation =>
        Devices.Any(d => PowerCfg.ClassifyDevice(d) is DeviceKind.Keyboard or DeviceKind.Mouse);
}

/// <summary>Make Windows ignore one caller's stay-awake request (admin).</summary>
internal sealed record IgnoreRequestFix(PowerRequest Request) : Fix;

/// <summary>Turn a power plan setting off (no admin).</summary>
internal sealed record TurnOffSettingFix(PowerSetting Setting) : Fix;

internal sealed record UndoFix(AppliedFix Applied) : Fix;

internal enum FixKind { DeviceWakeDisabled, RequestIgnored, SettingTurnedOff }

/// <summary>A change Force Sleep made, remembered so it can be shown and undone.</summary>
/// <param name="Target">Device name, request caller name, or setting name.</param>
/// <param name="Label">What to call it when talking to the user.</param>
internal sealed record AppliedFix(
    FixKind Kind,
    string Target,
    string Label,
    string? CallerType = null,
    string[]? Requests = null,
    uint? PreviousAc = null,
    uint? PreviousDc = null);

internal sealed record FixResult(bool Succeeded, string Message);

internal static class Fixes
{
    public static async Task<FixResult> ApplyAsync(Fix fix, Settings settings)
    {
        var result = fix switch
        {
            DisableDeviceWakeFix f => await DisableDeviceWakeAsync(f, settings),
            IgnoreRequestFix f => await IgnoreRequestAsync(f.Request, settings),
            TurnOffSettingFix f => TurnOffSetting(f.Setting, settings),
            UndoFix f => await UndoAsync(f.Applied, settings),
            _ => throw new ArgumentOutOfRangeException(nameof(fix)),
        };

        if (result.Succeeded)
            settings.Save();
        return result;
    }

    private static async Task<FixResult> DisableDeviceWakeAsync(DisableDeviceWakeFix fix, Settings settings)
    {
        var outcome = await PowerCfg.RunElevatedAsync(fix.Devices.Select(d => new[] { "/devicedisablewake", d }).ToList());
        if (outcome != ElevatedResult.Ok)
            return Failure(outcome);

        foreach (var device in fix.Devices)
        {
            settings.AppliedFixes.RemoveAll(a => a.Kind == FixKind.DeviceWakeDisabled && a.Target == device);
            settings.AppliedFixes.Add(new AppliedFix(FixKind.DeviceWakeDisabled, device, PowerCfg.FriendlyDevice(device)));
        }
        return new FixResult(true, L.FixDevicesDone);
    }

    private static async Task<FixResult> IgnoreRequestAsync(PowerRequest request, Settings settings)
    {
        // An override replaces the caller's previous one, so keep request types ignored earlier.
        var existing = settings.AppliedFixes.FirstOrDefault(a => IsOverrideFor(a, request.CallerType, request.OverrideName));
        var requestTypes = (existing?.Requests ?? []).Append(request.RequestType).Distinct().ToArray();

        var outcome = await PowerCfg.RunElevatedAsync(
            [["/requestsoverride", request.CallerType, request.OverrideName, .. requestTypes]]);
        if (outcome != ElevatedResult.Ok)
            return Failure(outcome);

        if (existing is not null)
            settings.AppliedFixes.Remove(existing);
        settings.AppliedFixes.Add(new AppliedFix(
            FixKind.RequestIgnored, request.OverrideName, request.Source, request.CallerType, requestTypes));
        return new FixResult(true, L.FixRequestDone(request.Source));
    }

    private static FixResult TurnOffSetting(PowerSetting setting, Settings settings)
    {
        if (PowerSettings.Read(setting) is not { } previous)
            return new FixResult(false, L.SettingReadFailed);
        if (!PowerSettings.Write(setting, PowerSettingValue.Off))
            return new FixResult(false, L.SettingWriteFailed);

        // Keep the original value if it was already turned off once, so undo restores that.
        if (!settings.AppliedFixes.Any(a => a.Kind == FixKind.SettingTurnedOff && a.Target == setting.ToString()))
            settings.AppliedFixes.Add(new AppliedFix(
                FixKind.SettingTurnedOff, setting.ToString(), L.SettingName(setting), PreviousAc: previous.Ac, PreviousDc: previous.Dc));
        return new FixResult(true, L.FixSettingDone(L.SettingName(setting)));
    }

    private static async Task<FixResult> UndoAsync(AppliedFix applied, Settings settings)
    {
        switch (applied.Kind)
        {
            case FixKind.DeviceWakeDisabled:
                var deviceOutcome = await PowerCfg.RunElevatedAsync([["/deviceenablewake", applied.Target]]);
                if (deviceOutcome != ElevatedResult.Ok)
                    return Failure(deviceOutcome);
                break;

            case FixKind.RequestIgnored:
                // Without request types, powercfg removes the caller's override.
                var requestOutcome = await PowerCfg.RunElevatedAsync(
                    [["/requestsoverride", applied.CallerType ?? "", applied.Target]]);
                if (requestOutcome != ElevatedResult.Ok)
                    return Failure(requestOutcome);
                break;

            case FixKind.SettingTurnedOff:
                if (!Enum.TryParse<PowerSetting>(applied.Target, out var setting)
                    || !PowerSettings.Write(setting, new PowerSettingValue(applied.PreviousAc ?? 1, applied.PreviousDc ?? 1)))
                    return new FixResult(false, L.UndoFailed);
                break;
        }

        settings.AppliedFixes.Remove(applied);
        return new FixResult(true, L.UndoDone);
    }

    public static bool IsOverrideFor(AppliedFix applied, string callerType, string name) =>
        applied.Kind == FixKind.RequestIgnored
        && string.Equals(applied.CallerType, callerType, StringComparison.OrdinalIgnoreCase)
        && string.Equals(applied.Target, name, StringComparison.OrdinalIgnoreCase);

    private static FixResult Failure(ElevatedResult outcome) =>
        new(false, outcome == ElevatedResult.Cancelled ? L.AdminDeclined : L.FixFailed);
}
