using System.Windows.Input;

namespace ForceSleep;

/// <summary>A global keyboard shortcut such as "Ctrl+Alt+End", stored as text in the settings.</summary>
internal sealed record Hotkey(ModifierKeys Modifiers, Key Key)
{
    public const string Default = "Ctrl+Alt+End";

    // RegisterHotKey modifier flags.
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

    public uint NativeModifiers =>
        (Modifiers.HasFlag(ModifierKeys.Alt) ? ModAlt : 0)
        | (Modifiers.HasFlag(ModifierKeys.Control) ? ModControl : 0)
        | (Modifiers.HasFlag(ModifierKeys.Shift) ? ModShift : 0)
        | (Modifiers.HasFlag(ModifierKeys.Windows) ? ModWin : 0)
        | ModNoRepeat;

    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>
    /// A shortcut needs at least one of Ctrl/Alt/Win, so ordinary typing (or Shift+letter)
    /// can never trigger a sleep.
    /// </summary>
    public static Hotkey? Create(ModifierKeys modifiers, Key key)
    {
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift
            or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.ImeProcessed or Key.DeadCharProcessed)
            return null;
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
            return null;
        return new Hotkey(modifiers, key);
    }

    public static Hotkey? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var modifiers = ModifierKeys.None;
        Key? key = null;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "win": modifiers |= ModifierKeys.Windows; break;
                default:
                    if (key is not null || !Enum.TryParse<Key>(part, ignoreCase: true, out var parsed))
                        return null;
                    key = parsed;
                    break;
            }
        }

        return key is { } k ? Create(modifiers, k) : null;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }
}
