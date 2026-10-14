namespace PasteProtect;

internal enum CaseMode
{
    /// <summary>Match any capitalization and copy it onto the replacement (acme→contoso, Acme→Contoso, ACME→CONTOSO).</summary>
    Preserve,

    /// <summary>Match any capitalization and insert the replacement exactly as typed.</summary>
    Ignore,

    /// <summary>Only match the exact capitalization of the find text.</summary>
    Match,
}

internal sealed class ReplacementRule
{
    private string find = "";
    private string replace = "";

    public bool Enabled { get; set; } = true;

    public string Find
    {
        get => find;
        set => find = value ?? "";
    }

    public string Replace
    {
        get => replace;
        set => replace = value ?? "";
    }

    public bool WholeWord { get; set; }

    public CaseMode CaseMode { get; set; } = CaseMode.Preserve;

    public ReplacementRule Clone() => (ReplacementRule)MemberwiseClone();
}

internal sealed class AppSettings
{
    public const int MinRestoreDelayMs = 50;
    public const int MaxRestoreDelayMs = 5000;

    public Hotkey Hotkey { get; set; } = Hotkey.Default;

    /// <summary>False while special paste is paused from the tray menu.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Put the original clipboard contents back after a special paste.</summary>
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>How long the target app gets to read the replaced text before the original is restored.</summary>
    public int RestoreDelayMs { get; set; } = 500;

    public List<ReplacementRule> Rules { get; set; } = [];

    /// <summary>Repairs values from a hand-edited or older settings file.</summary>
    public void Normalize()
    {
        Rules = (Rules ?? []).Where(r => r is not null && r.Find.Length > 0).ToList();
        RestoreDelayMs = Math.Clamp(RestoreDelayMs, MinRestoreDelayMs, MaxRestoreDelayMs);
        if (!Hotkey.IsValid)
            Hotkey = Hotkey.Default;
    }
}
