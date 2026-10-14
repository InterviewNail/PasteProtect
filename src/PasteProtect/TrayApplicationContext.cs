namespace PasteProtect;

/// <summary>Owns the tray icon, the global hotkey and the settings window for the life of the app.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly MessageWindow window = new();
    private readonly SpecialPaster paster;
    private readonly NotifyIcon tray;
    private readonly Icon activeIcon;
    private readonly Icon pausedIcon;
    private readonly ToolStripMenuItem statusItem;
    private readonly ToolStripMenuItem enabledItem;
    private readonly ToolStripMenuItem startupItem;
    private readonly EventWaitHandle showSettingsSignal;
    private readonly RegisteredWaitHandle showSettingsWait;
    private AppSettings settings;
    private TextReplacer replacer;
    private SettingsForm? settingsForm;
    private bool hotkeySuspended;
    private bool showTrayTipAfterSettings;

    public TrayApplicationContext()
    {
        settings = SettingsStore.Load(out bool firstRun, out string? loadError);
        replacer = new TextReplacer(settings.Rules);
        paster = new SpecialPaster(window.Handle);
        window.HotkeyPressed += OnHotkeyPressed;

        activeIcon = AppIcons.Load(SystemInformation.SmallIconSize);
        pausedIcon = AppIcons.CreatePaused(activeIcon);

        var menu = new ContextMenuStrip();
        statusItem = new ToolStripMenuItem { Enabled = false };
        var settingsItem = new ToolStripMenuItem("&Rules && Settings…", null, (_, _) => ShowSettings())
        {
            Font = new Font(menu.Font, FontStyle.Bold),
        };
        enabledItem = new ToolStripMenuItem("&Enabled", null, (_, _) => SetEnabled(!settings.Enabled));
        startupItem = new ToolStripMenuItem("Start with &Windows", null, (_, _) => ToggleStartup());
        menu.Items.AddRange(new ToolStripItem[]
        {
            statusItem,
            new ToolStripSeparator(),
            settingsItem,
            enabledItem,
            startupItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("E&xit", null, (_, _) => Exit()),
        });
        menu.Opening += (_, _) => startupItem.Checked = StartupManager.IsEnabled;

        tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowSettings();
        };

        // A second copy of the app signals this event instead of starting; respond by showing settings.
        var ui = new WindowsFormsSynchronizationContext();
        showSettingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowSettingsEventName);
        showSettingsWait = ThreadPool.RegisterWaitForSingleObject(
            showSettingsSignal, (_, _) => ui.Post(_ => ShowSettings(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        if (!RefreshHotkey())
            ShowBalloon($"{settings.Hotkey} is already used by another app", "Open settings to choose a different shortcut.", ToolTipIcon.Warning);
        if (loadError is not null)
            ShowBalloon("Settings couldn't be read", loadError, ToolTipIcon.Warning);
        if (firstRun)
        {
            showTrayTipAfterSettings = true;
            ShowSettings();
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        try
        {
            await paster.PasteAsync(replacer, settings.RestoreClipboard, settings.RestoreDelayMs);
        }
        catch (Exception ex)
        {
            ShowBalloon("Special paste failed", ex.Message, ToolTipIcon.Error);
        }
    }

    /// <summary>Registers or releases the global shortcut to match the current state. False if another app owns it.</summary>
    private bool RefreshHotkey()
    {
        bool available = true;
        if (settings.Enabled && !hotkeySuspended)
            available = window.RegisterHotkey(settings.Hotkey);
        else
            window.UnregisterHotkey();

        tray.Icon = settings.Enabled && available ? activeIcon : pausedIcon;
        tray.Text = !settings.Enabled ? $"{Program.AppName} (paused)"
            : available ? $"{Program.AppName} – {settings.Hotkey}"
            : $"{Program.AppName} – {settings.Hotkey} is in use";
        statusItem.Text = !settings.Enabled ? "Special paste is paused"
            : available ? $"Special paste: {settings.Hotkey}"
            : $"{settings.Hotkey} is used by another app";
        enabledItem.Checked = settings.Enabled;
        return available;
    }

    /// <summary>
    /// Called while the shortcut box in settings has focus, so pressing the current shortcut there
    /// records it instead of triggering a paste.
    /// </summary>
    private void SuspendHotkey(bool suspend)
    {
        if (hotkeySuspended == suspend)
            return;
        hotkeySuspended = suspend;
        RefreshHotkey();
    }

    /// <summary>Called by the settings window. Returns an error message, or null on success.</summary>
    private string? ApplySettings(AppSettings updated)
    {
        updated.Enabled = settings.Enabled;
        var previous = settings;
        settings = updated;
        if (!RefreshHotkey())
        {
            settings = previous;
            RefreshHotkey();
            return $"{updated.Hotkey} is already used by another app. Please choose a different shortcut.";
        }
        replacer = new TextReplacer(settings.Rules);
        return TrySave();
    }

    private string? TrySave()
    {
        try
        {
            SettingsStore.Save(settings);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"Couldn't save settings to {SettingsStore.FilePath}: {ex.Message}";
        }
    }

    private void SetEnabled(bool enabled)
    {
        settings.Enabled = enabled;
        if (!RefreshHotkey())
            ShowBalloon($"{settings.Hotkey} is already used by another app", "Open settings to choose a different shortcut.", ToolTipIcon.Warning);
        if (TrySave() is { } error)
            ShowBalloon("Couldn't save settings", error, ToolTipIcon.Warning);
    }

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ShowBalloon("Couldn't change the startup setting", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void ShowSettings()
    {
        if (settingsForm is not null)
        {
            if (settingsForm.WindowState == FormWindowState.Minimized)
                settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
            return;
        }

        settingsForm = new SettingsForm(settings, ApplySettings, SuspendHotkey);
        settingsForm.FormClosed += (_, _) =>
        {
            settingsForm = null;
            if (showTrayTipAfterSettings)
            {
                showTrayTipAfterSettings = false;
                ShowBalloon($"{Program.AppName} is running in the tray",
                    $"Press {settings.Hotkey} in any app to paste with your replacements.", ToolTipIcon.Info);
            }
        };
        settingsForm.Show();
        settingsForm.Activate();
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon) =>
        tray.ShowBalloonTip(5000, title, text, icon);

    private void Exit()
    {
        // Give an open settings window the chance to save its changes, or to cancel the exit.
        settingsForm?.Close();
        if (settingsForm is null)
            ExitThread();
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            showSettingsWait.Unregister(null);
            showSettingsSignal.Dispose();
            tray.ContextMenuStrip?.Dispose();
            tray.Dispose();
            window.Dispose();
            activeIcon.Dispose();
            pausedIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
