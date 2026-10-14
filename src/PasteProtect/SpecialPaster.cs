using System.Media;

namespace PasteProtect;

/// <summary>
/// The special paste: swaps the clipboard text for its replaced version, sends Ctrl+V to the focused
/// app, then (optionally) puts the original clipboard back.
/// </summary>
internal sealed class SpecialPaster(nint clipboardOwner)
{
    private readonly Win32Clipboard clipboard = new(clipboardOwner);
    private bool busy;

    public async Task PasteAsync(TextReplacer replacer, bool restoreClipboard, int restoreDelayMs)
    {
        if (busy)
            return;
        busy = true;
        try
        {
            await Keyboard.ReleaseModifiersAsync();

            if (!clipboard.TryGetText(out string? original))
            {
                // The clipboard is locked by another app. Never fall back to pasting unreplaced text.
                SystemSounds.Beep.Play();
                return;
            }
            if (original is null)
            {
                // Nothing to replace (e.g. an image or files), so paste as normal.
                Keyboard.SendPaste();
                return;
            }

            // Always paste plain text, even if no rule matched, so hidden formatting such as HTML
            // link targets can't carry the original words along.
            string replaced = replacer.Replace(original);
            var snapshot = restoreClipboard ? clipboard.Capture() : null;
            if (!clipboard.SetText(replaced, hideFromHistory: snapshot is not null))
            {
                if (snapshot is not null)
                    clipboard.Restore(snapshot);
                SystemSounds.Beep.Play();
                return;
            }
            uint ourContents = Win32Clipboard.SequenceNumber;
            Keyboard.SendPaste();

            if (snapshot is null)
                return;
            await Task.Delay(restoreDelayMs);

            // If something else changed the clipboard in the meantime (e.g. the user copied), keep that.
            if (Win32Clipboard.SequenceNumber == ourContents)
                clipboard.Restore(snapshot);
        }
        finally
        {
            busy = false;
        }
    }
}
