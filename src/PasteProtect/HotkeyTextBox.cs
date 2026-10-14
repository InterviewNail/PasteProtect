using System.ComponentModel;

namespace PasteProtect;

/// <summary>A read-only box that records the key combination pressed while it has focus.</summary>
internal sealed class HotkeyTextBox : TextBox
{
    private Hotkey hotkey;

    public HotkeyTextBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        BackColor = SystemColors.Window;
    }

    public event EventHandler? HotkeyChanged;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Hotkey Hotkey
    {
        get => hotkey;
        set
        {
            hotkey = value;
            Text = value.ToString();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        Keys modifierKeys = keyData & Keys.Modifiers;

        // Plain Tab / Shift+Tab still move focus.
        if (key == Keys.Tab && (modifierKeys & ~Keys.Shift) == 0)
            return base.ProcessCmdKey(ref msg, keyData);

        var modifiers = HotkeyModifiers.None;
        if (modifierKeys.HasFlag(Keys.Control)) modifiers |= HotkeyModifiers.Control;
        if (modifierKeys.HasFlag(Keys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (modifierKeys.HasFlag(Keys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (IsWinKeyDown()) modifiers |= HotkeyModifiers.Win;

        if (Hotkey.IsModifierKey(key))
        {
            Text = Hotkey.FormatModifiers(modifiers) + "…";
            return true;
        }

        var candidate = new Hotkey(modifiers, key);
        if (candidate.IsValid)
        {
            Hotkey = candidate;
            HotkeyChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Text = "Include Ctrl, Alt or Win";
        }
        return true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        // Once every key is released, show the recorded shortcut again instead of a partial one.
        if (e.Modifiers == Keys.None && !IsWinKeyDown())
            Text = hotkey.ToString();
        base.OnKeyUp(e);
    }

    protected override void OnLeave(EventArgs e)
    {
        Text = hotkey.ToString();
        base.OnLeave(e);
    }

    private static bool IsWinKeyDown() =>
        (NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0 ||
        (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
}
