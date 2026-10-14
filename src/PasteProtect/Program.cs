namespace PasteProtect;

internal static class Program
{
    public const string AppName = "PasteProtect";
    public const string ShowSettingsEventName = @"Local\PasteProtect.ShowSettings";
    private const string SingleInstanceMutexName = @"Local\PasteProtect.SingleInstance";

    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            // Already running in the tray: ask that copy to open its settings window instead.
            if (EventWaitHandle.TryOpenExisting(ShowSettingsEventName, out var showSettings))
            {
                NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
                showSettings.Set();
                showSettings.Dispose();
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
