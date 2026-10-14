using System.Diagnostics;
using System.Runtime.InteropServices;
using static PasteProtect.NativeMethods;

namespace PasteProtect;

/// <summary>Synthesizes the keystrokes for a special paste.</summary>
internal static class Keyboard
{
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_LSHIFT = 0xA0;
    private const ushort VK_RSHIFT = 0xA1;
    private const ushort VK_LCONTROL = 0xA2;
    private const ushort VK_RCONTROL = 0xA3;
    private const ushort VK_LMENU = 0xA4;
    private const ushort VK_RMENU = 0xA5;

    // An unassigned virtual key. Tapping it while Alt or Win is held stops their release from opening
    // the app's menu bar or the Start menu, since the hotkey swallowed the only other key pressed.
    private const ushort VK_MASK = 0xE8;

    private static readonly ushort[] Modifiers =
        [VK_LSHIFT, VK_RSHIFT, VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU, VK_LWIN, VK_RWIN];

    /// <summary>
    /// Waits for the user to let go of the hotkey's modifiers, so the target app receives a plain
    /// Ctrl+V rather than Ctrl+Shift+Alt+V. Anything still held after the timeout is released for them.
    /// </summary>
    public static async Task ReleaseModifiersAsync(int timeoutMs = 1000)
    {
        if (IsDown(VK_LMENU) || IsDown(VK_RMENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN))
            Send(Key(VK_MASK, up: false), Key(VK_MASK, up: true));

        var elapsed = Stopwatch.StartNew();
        while (Modifiers.Any(IsDown) && elapsed.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);

        var stillHeld = Modifiers.Where(IsDown).Select(vk => Key(vk, up: true)).ToArray();
        if (stillHeld.Length > 0)
            Send(stillHeld);
    }

    public static void SendPaste() =>
        Send(Key(VK_CONTROL, up: false), Key(VK_V, up: false), Key(VK_V, up: true), Key(VK_CONTROL, up: true));

    private static bool IsDown(ushort vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static INPUT Key(ushort vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (vk is VK_RCONTROL or VK_RMENU or VK_LWIN or VK_RWIN)
            flags |= KEYEVENTF_EXTENDEDKEY;

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = vk, wScan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC), dwFlags = flags },
            },
        };
    }

    private static void Send(params INPUT[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
}
