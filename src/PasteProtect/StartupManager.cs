using Microsoft.Win32;

namespace PasteProtect;

/// <summary>Controls the per-user "Run" registry entry that starts the app when you sign in.</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <summary>True when Windows will start this copy of the app at sign-in.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return string.Equals(key?.GetValue(Program.AppName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(Program.AppName, Command);
        else
            key.DeleteValue(Program.AppName, throwOnMissingValue: false);
    }
}
