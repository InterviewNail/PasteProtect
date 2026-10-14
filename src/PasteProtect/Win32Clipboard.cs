using System.Runtime.InteropServices;
using System.Text;
using static PasteProtect.NativeMethods;

namespace PasteProtect;

/// <summary>A copy of every format that was on the clipboard, so it can be put back later.</summary>
internal sealed record ClipboardSnapshot(IReadOnlyList<(uint Format, byte[] Data)> Items);

/// <summary>
/// Direct Win32 clipboard access. Unlike the WinForms Clipboard class this can snapshot and restore
/// every memory-based format (text, HTML, RTF, images, file lists…), and it can flag our temporary
/// text so Windows clipboard history and clipboard managers ignore it.
/// </summary>
internal sealed class Win32Clipboard(nint owner)
{
    private const uint CF_BITMAP = 2;
    private const uint CF_METAFILEPICT = 3;
    private const uint CF_PALETTE = 9;
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_ENHMETAFILE = 14;
    private const uint CF_OWNERDISPLAY = 0x80;
    private const uint CF_DSPBITMAP = 0x82;
    private const uint CF_DSPMETAFILEPICT = 0x83;
    private const uint CF_DSPENHMETAFILE = 0x8E;
    private const uint CF_PRIVATEFIRST = 0x200;
    private const uint CF_GDIOBJLAST = 0x3FF;

    private static readonly uint ExcludeFromMonitoring = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint CanIncludeInHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint CanUploadToCloud = RegisterClipboardFormat("CanUploadToCloudClipboard");

    /// <summary>Changes whenever anything writes to the clipboard.</summary>
    public static uint SequenceNumber => GetClipboardSequenceNumber();

    /// <summary>
    /// Reads the clipboard text. <paramref name="text"/> is null if there is no text on the clipboard.
    /// Returns false if the clipboard couldn't be opened, e.g. because another app is holding it.
    /// </summary>
    public bool TryGetText(out string? text)
    {
        text = null;
        if (!IsClipboardFormatAvailable(CF_UNICODETEXT))
            return true;
        if (!TryOpen())
            return false;
        try
        {
            var data = ReadFormat(CF_UNICODETEXT);
            if (data is null)
                return false;
            text = Encoding.Unicode.GetString(data);
            int end = text.IndexOf('\0');
            if (end >= 0)
                text = text[..end];
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Replaces the clipboard contents with plain text.</summary>
    public bool SetText(string text, bool hideFromHistory)
    {
        if (!TryOpen())
            return false;
        try
        {
            if (!EmptyClipboard())
                return false;
            bool ok = WriteFormat(CF_UNICODETEXT, Encoding.Unicode.GetBytes(text + '\0'));
            if (hideFromHistory)
                WriteHistoryExclusion();
            return ok;
        }
        finally
        {
            CloseClipboard();
        }
    }

    public ClipboardSnapshot? Capture()
    {
        if (!TryOpen())
            return null;
        try
        {
            var items = new List<(uint, byte[])>();
            for (uint format = EnumClipboardFormats(0); format != 0; format = EnumClipboardFormats(format))
            {
                if (!IsCopyable(format))
                    continue;
                try
                {
                    if (ReadFormat(format) is { } data)
                        items.Add((format, data));
                }
                catch (OverflowException)
                {
                    // Over 2 GB. Skip this format rather than fail the whole paste.
                }
            }
            return new ClipboardSnapshot(items);
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Puts a snapshot back. It's hidden from clipboard history, which already has the original.</summary>
    public bool Restore(ClipboardSnapshot snapshot)
    {
        if (!TryOpen())
            return false;
        try
        {
            if (!EmptyClipboard())
                return false;
            foreach (var (format, data) in snapshot.Items)
            {
                if (format != ExcludeFromMonitoring && format != CanIncludeInHistory && format != CanUploadToCloud)
                    WriteFormat(format, data);
            }
            WriteHistoryExclusion();
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private bool TryOpen()
    {
        // Another app may have the clipboard open for a moment, so retry briefly.
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(owner))
                return true;
            Thread.Sleep(15);
        }
        return false;
    }

    // GDI handles and private formats aren't plain memory blocks, so they can't be copied byte for byte.
    // Bitmaps still survive because Windows also offers them as CF_DIB, which is a memory block.
    private static bool IsCopyable(uint format) =>
        format is not (CF_BITMAP or CF_METAFILEPICT or CF_PALETTE or CF_ENHMETAFILE or
                       CF_OWNERDISPLAY or CF_DSPBITMAP or CF_DSPMETAFILEPICT or CF_DSPENHMETAFILE)
        && format is not (>= CF_PRIVATEFIRST and <= CF_GDIOBJLAST);

    private static byte[]? ReadFormat(uint format)
    {
        nint handle = GetClipboardData(format);
        if (handle == 0)
            return null;
        nint ptr = GlobalLock(handle);
        if (ptr == 0)
            return null;
        try
        {
            var data = new byte[checked((int)GlobalSize(handle))];
            Marshal.Copy(ptr, data, 0, data.Length);
            return data;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static bool WriteFormat(uint format, byte[] data)
    {
        nint handle = GlobalAlloc(GMEM_MOVEABLE, (nuint)Math.Max(data.Length, 1));
        if (handle == 0)
            return false;
        nint ptr = GlobalLock(handle);
        if (ptr == 0)
        {
            GlobalFree(handle);
            return false;
        }
        Marshal.Copy(data, 0, ptr, data.Length);
        GlobalUnlock(handle);

        // On success the clipboard owns the memory; otherwise it's still ours to free.
        if (SetClipboardData(format, handle) != 0)
            return true;
        GlobalFree(handle);
        return false;
    }

    private static void WriteHistoryExclusion()
    {
        byte[] no = BitConverter.GetBytes(0);   // DWORD 0
        WriteFormat(ExcludeFromMonitoring, no); // its presence alone tells clipboard managers to skip it
        WriteFormat(CanIncludeInHistory, no);
        WriteFormat(CanUploadToCloud, no);
    }
}
