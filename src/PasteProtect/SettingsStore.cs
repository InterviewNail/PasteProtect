using System.Text.Json;
using System.Text.Json.Serialization;

namespace PasteProtect;

/// <summary>Reads and writes %APPDATA%\PasteProtect\settings.json.</summary>
internal static class SettingsStore
{
    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Program.AppName, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Loads the saved settings. Returns defaults when there is no file yet (<paramref name="firstRun"/>)
    /// or when the file can't be read (<paramref name="error"/>).
    /// </summary>
    public static AppSettings Load(out bool firstRun, out string? error)
    {
        firstRun = !File.Exists(FilePath);
        error = null;
        if (firstRun)
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep a copy of the unreadable file so no rules are silently lost, then carry on with defaults.
            string backup = FilePath + ".bad";
            try
            {
                File.Copy(FilePath, backup, overwrite: true);
            }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException)
            {
            }
            error = $"{ex.Message} Using default settings; the old file was kept as {backup}.";
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
