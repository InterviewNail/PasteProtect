using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasteProtect;

/// <summary>Values match the MOD_* flags taken by RegisterHotKey.</summary>
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A global keyboard shortcut such as Ctrl+Shift+Alt+V.</summary>
[JsonConverter(typeof(HotkeyJsonConverter))]
internal readonly record struct Hotkey(HotkeyModifiers Modifiers, Keys Key)
{
    public static readonly Hotkey Default = new(HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Alt, Keys.V);

    /// <summary>Needs Ctrl, Alt or Win: Shift plus a key is just typing.</summary>
    public bool IsValid =>
        (int)Key is > 0 and <= 0xFE   // a real virtual-key code, not a Keys flag combination
        && !IsModifierKey(Key)
        && (Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Win)) != 0;

    public override string ToString() => FormatModifiers(Modifiers) + KeyName(Key);

    /// <summary>"Ctrl+Shift+" style prefix, also used while a shortcut is being typed.</summary>
    public static string FormatModifiers(HotkeyModifiers modifiers)
    {
        var text = "";
        if (modifiers.HasFlag(HotkeyModifiers.Win)) text += "Win+";
        if (modifiers.HasFlag(HotkeyModifiers.Control)) text += "Ctrl+";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) text += "Shift+";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) text += "Alt+";
        return text;
    }

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var modifiers = HotkeyModifiers.None;
        var key = Keys.None;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= HotkeyModifiers.Control; break;
                case "shift": modifiers |= HotkeyModifiers.Shift; break;
                case "alt": modifiers |= HotkeyModifiers.Alt; break;
                case "win" or "windows": modifiers |= HotkeyModifiers.Win; break;
                default:
                    if (key != Keys.None)
                        return false;
                    if (part.Length == 1 && char.IsAsciiDigit(part[0]))
                        key = Keys.D0 + (part[0] - '0');
                    else if (int.TryParse(part, out _) || !Enum.TryParse(part, ignoreCase: true, out key))
                        return false;
                    break;
            }
        }

        hotkey = new Hotkey(modifiers, key);
        return hotkey.IsValid;
    }

    public static bool IsModifierKey(Keys key) => key is
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu or
        Keys.LWin or Keys.RWin;

    private static string KeyName(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (key - Keys.D0))).ToString() : key.ToString();
}

/// <summary>Stores a hotkey in settings.json as readable text, e.g. "Ctrl+Shift+Alt+V".</summary>
internal sealed class HotkeyJsonConverter : JsonConverter<Hotkey>
{
    public override Hotkey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            reader.Skip();
            return Hotkey.Default;
        }
        return Hotkey.TryParse(reader.GetString(), out var hotkey) ? hotkey : Hotkey.Default;
    }

    public override void Write(Utf8JsonWriter writer, Hotkey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
