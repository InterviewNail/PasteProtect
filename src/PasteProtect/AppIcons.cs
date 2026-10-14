using System.Drawing.Imaging;

namespace PasteProtect;

internal static class AppIcons
{
    public static Icon Load(Size size)
    {
        using var stream = typeof(AppIcons).Assembly.GetManifestResourceStream("PasteProtect.app.ico")
            ?? throw new InvalidOperationException("The app.ico resource is missing.");
        return new Icon(stream, size);
    }

    /// <summary>A faded grey copy of the icon, shown while special paste is paused.</summary>
    public static Icon CreatePaused(Icon source)
    {
        using var original = source.ToBitmap();
        using var faded = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(faded))
        using (var attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(new ColorMatrix(
            [
                [0.30f, 0.30f, 0.30f, 0, 0],
                [0.59f, 0.59f, 0.59f, 0, 0],
                [0.11f, 0.11f, 0.11f, 0, 0],
                [0, 0, 0, 0.55f, 0],
                [0, 0, 0, 0, 1],
            ]));
            graphics.DrawImage(original, new Rectangle(Point.Empty, original.Size),
                0, 0, original.Width, original.Height, GraphicsUnit.Pixel, attributes);
        }

        nint handle = faded.GetHicon();
        try
        {
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }
}
