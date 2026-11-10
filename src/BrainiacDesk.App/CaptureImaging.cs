using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
namespace BrainiacDesk.App;
public sealed record CapturePreset(string Name, int Width, int Height)
{
    public override string ToString() => Name;
    public static readonly CapturePreset[] All = [new("Original pixels", 0, 0), new("HD · 1280 × 720", 1280, 720), new("Full HD · 1920 × 1080", 1920, 1080), new("Square · 1080 × 1080", 1080, 1080), new("Portrait · 1080 × 1920", 1080, 1920)];
}
public static class CaptureImaging
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo { public int Size, Flags; public IntPtr Cursor; public NativePoint Position; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, int step, IntPtr brush, int flags);
    public static Drawing.Rectangle Desktop => Forms.SystemInformation.VirtualScreen;
    public static Drawing.Rectangle Primary => Forms.Screen.PrimaryScreen?.Bounds ?? Desktop;
    public static Drawing.Rectangle Foreground()
    {
        if (!GetWindowRect(GetForegroundWindow(), out var rect)) throw new InvalidOperationException("Cannot determine the foreground window. Focus the target during the countdown.");
        return Drawing.Rectangle.Intersect(Desktop, new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top));
    }
    public static Drawing.Bitmap Grab(Drawing.Rectangle bounds, bool cursor)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || (long)bounds.Width * bounds.Height > 32_000_000) throw new InvalidOperationException("Choose a capture area of at most 32 megapixels.");
        var image = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            using var graphics = Drawing.Graphics.FromImage(image);
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
            if (cursor)
            {
                var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
                if (GetCursorInfo(ref info) && (info.Flags & 1) != 0)
                {
                    var dc = graphics.GetHdc(); try { DrawIconEx(dc, info.Position.X - bounds.Left, info.Position.Y - bounds.Top, info.Cursor, 0, 0, 0, IntPtr.Zero, 3); } finally { graphics.ReleaseHdc(dc); }
                }
            }
            return image;
        }
        catch { image.Dispose(); throw; }
    }
    public static BitmapSource ToSource(Drawing.Bitmap image)
    {
        using var memory = new MemoryStream(); image.Save(memory, Drawing.Imaging.ImageFormat.Png); memory.Position = 0;
        var result = BitmapDecoder.Create(memory, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; result.Freeze(); return result;
    }
    public static BitmapSource Resize(BitmapSource source, CapturePreset preset, bool fill)
    {
        if (preset.Width == 0) return source;
        if (preset.Width < 1 || preset.Height < 1 || (long)preset.Width * preset.Height > 32_000_000) throw new ArgumentException("Invalid output size.");
        double scale = fill ? Math.Max((double)preset.Width / source.PixelWidth, (double)preset.Height / source.PixelHeight) : Math.Min((double)preset.Width / source.PixelWidth, (double)preset.Height / source.PixelHeight);
        var width = source.PixelWidth * scale; var height = source.PixelHeight * scale;
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, preset.Width, preset.Height)); dc.PushClip(new RectangleGeometry(new Rect(0, 0, preset.Width, preset.Height))); dc.DrawImage(source, new Rect((preset.Width - width) / 2, (preset.Height - height) / 2, width, height)); dc.Pop(); }
        var output = new RenderTargetBitmap(preset.Width, preset.Height, 96, 96, PixelFormats.Pbgra32); output.Render(visual); output.Freeze(); return output;
    }
    public static void DrawVideoFrame(Drawing.Bitmap source, Drawing.Bitmap output, Drawing.Rectangle? crop, bool fill)
    {
        var area = crop ?? new Drawing.Rectangle(0, 0, source.Width, source.Height);
        if (area.Width < 1 || area.Height < 1 || !new Drawing.Rectangle(0, 0, source.Width, source.Height).Contains(area)) throw new ArgumentException("Video crop is outside the source frame.");
        double scale = fill ? Math.Max((double)output.Width / area.Width, (double)output.Height / area.Height) : Math.Min((double)output.Width / area.Width, (double)output.Height / area.Height);
        using var graphics = Drawing.Graphics.FromImage(output); graphics.Clear(Drawing.Color.Black); graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var width = (float)(area.Width * scale); var height = (float)(area.Height * scale);
        graphics.DrawImage(source, new Drawing.RectangleF((output.Width - width) / 2, (output.Height - height) / 2, width, height), area, Drawing.GraphicsUnit.Pixel);
    }
}