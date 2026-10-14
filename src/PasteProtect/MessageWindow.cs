namespace PasteProtect;

/// <summary>
/// Hidden message-only window. It receives the global hotkey and owns the clipboard while we change it.
/// </summary>
internal sealed class MessageWindow : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x5050;
    private static readonly nint HWND_MESSAGE = -3;
    private bool registered;

    public MessageWindow() => CreateHandle(new CreateParams { Parent = HWND_MESSAGE });

    public event EventHandler? HotkeyPressed;

    /// <summary>Returns false if another app already uses this shortcut.</summary>
    public bool RegisterHotkey(Hotkey hotkey)
    {
        UnregisterHotkey();
        registered = NativeMethods.RegisterHotKey(
            Handle, HotkeyId, (uint)hotkey.Modifiers | NativeMethods.MOD_NOREPEAT, (uint)hotkey.Key);
        return registered;
    }

    public void UnregisterHotkey()
    {
        if (!registered)
            return;
        NativeMethods.UnregisterHotKey(Handle, HotkeyId);
        registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterHotkey();
        DestroyHandle();
    }
}
