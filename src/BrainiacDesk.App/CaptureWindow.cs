using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
namespace BrainiacDesk.App;
public sealed class CaptureWindow : ToolWindow
{
    private BitmapSource? bitmap;
    private readonly Stack<BitmapSource> undo = new();
    private readonly Stack<BitmapSource> redo = new();
    private readonly Canvas canvas = new();
    private readonly Image image = new();
    private Point? start;
    private readonly ComboBox tool = new() { ItemsSource = new[] { "Rectangle", "Arrow", "Highlight", "Redact", "Text", "Crop" }, SelectedIndex = 0 };
    private readonly TextBox annotation;
    private readonly ActionRouter router;
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
    public CaptureWindow(ActionRouter router) : base("Screen Capture Studio")
    {
        this.router = router;
        Note("Capture the virtual desktop or foreground window after a delay. Select Crop and drag over the captured image for a region. Redact burns opaque black pixels into exports. No cloud OCR is used.");
        var delay = new Slider { Minimum = 1, Maximum = 10, Value = 3, TickFrequency = 1, IsSnapToTickEnabled = true }; Note("Capture delay in seconds; focus the desired window during the delay."); Body.Children.Add(delay);
        Action("Capture full desktop", () => CaptureAsync(false, (int)delay.Value));
        Action("Capture foreground window", () => CaptureAsync(true, (int)delay.Value));
        Action("Open image", () => { var paths = PickFiles("Images|*.png;*.jpg;*.jpeg;*.bmp"); if (paths.Length > 0) { using var source = File.OpenRead(paths[0]); var loaded = BitmapDecoder.Create(source, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; if ((long)loaded.PixelWidth * loaded.PixelHeight > 16_000_000) throw new InvalidDataException("Capture editor limit is 16 megapixels."); SetImage(loaded); } return Task.CompletedTask; });
        Body.Children.Add(tool); annotation = Input("Annotation");
        var view = new Viewbox { Child = canvas, Stretch = Stretch.Uniform, Height = 340 }; canvas.Children.Add(image); Body.Children.Add(view);
        canvas.MouseLeftButtonDown += (_, e) => { if (bitmap is not null) { start = e.GetPosition(canvas); canvas.CaptureMouse(); } };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (bitmap is null || start is not Point first) return;
            var last = e.GetPosition(canvas); canvas.ReleaseMouseCapture(); start = null;
            try
            {
                var rect = new Rect(new Point(Math.Clamp(Math.Min(first.X, last.X), 0, bitmap.PixelWidth - 1), Math.Clamp(Math.Min(first.Y, last.Y), 0, bitmap.PixelHeight - 1)), new Point(Math.Clamp(Math.Max(first.X, last.X), 0, bitmap.PixelWidth), Math.Clamp(Math.Max(first.Y, last.Y), 0, bitmap.PixelHeight)));
                if (rect.Width < 2 || rect.Height < 2) return;
                var mode = (string)tool.SelectedItem;
                PushUndo();
                SetImage(Annotate(bitmap, mode, rect, annotation.Text)); redo.Clear(); Status.Text = "Applied " + mode;
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        };
        Action("Undo", () => { if (undo.Count > 0 && bitmap is not null) { redo.Push(bitmap); SetImage(undo.Pop()); } return Task.CompletedTask; });
        Action("Redo", () => { if (redo.Count > 0 && bitmap is not null) { PushUndo(); SetImage(redo.Pop()); } return Task.CompletedTask; });
        Action("Copy image", () => { if (bitmap is not null) System.Windows.Clipboard.SetImage(bitmap); return Task.CompletedTask; });
        Action("Pin image to Clipboard Shelf", () => { if (bitmap is not null) router.Shelf.AddImage(bitmap); return Task.CompletedTask; });
        Action("Save PNG / JPG", () => { if (bitmap is null) throw new InvalidOperationException("Capture or open an image first."); var path = SaveFile("PNG|*.png|JPEG|*.jpg", ".png"); if (path is not null) { BitmapEncoder encoder = Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ? new JpegBitmapEncoder { QualityLevel = 90 } : new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream); Status.Text = "Saved flattened image."; } return Task.CompletedTask; });
        Action("Export screenshot to PDF", () => { if (bitmap is null) throw new InvalidOperationException("Capture first."); var path = SaveFile("PDF|*.pdf", ".pdf"); if (path is not null) { var temp = Path.Combine(Path.GetTempPath(), "brainiac-" + Guid.NewGuid().ToString("N") + ".png"); try { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(temp)) encoder.Save(stream); ConversionEngine.Pdf([temp], path, "Combined PDF", CancellationToken.None); } finally { if (File.Exists(temp)) File.Delete(temp); } } return Task.CompletedTask; });
    }
    public static BitmapSource Annotate(BitmapSource source, string mode, Rect area, string text)
    {
        if (mode == "Crop") { var crop = new CroppedBitmap(source, new Int32Rect((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height)); crop.Freeze(); return crop; }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(source, new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            var pen = new Pen(Brushes.Orange, 4);
            switch (mode)
            {
                case "Redact": dc.DrawRectangle(Brushes.Black, null, area); break;
                case "Highlight": dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100, 255, 210, 0)), null, area); break;
                case "Rectangle": dc.DrawRectangle(null, pen, area); break;
                case "Arrow":
                    dc.DrawLine(pen, area.TopLeft, area.BottomRight);
                    var vector = area.BottomRight - area.TopLeft; vector.Normalize(); var perpendicular = new Vector(-vector.Y, vector.X);
                    dc.DrawLine(pen, area.BottomRight, area.BottomRight - vector * 18 + perpendicular * 9); dc.DrawLine(pen, area.BottomRight, area.BottomRight - vector * 18 - perpendicular * 9); break;
                case "Text":
                    if (text.Length > 500) throw new InvalidOperationException("Annotation text limit is 500 characters.");
                    dc.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 24, Brushes.Orange, 1), area.TopLeft); break;
                default: throw new ArgumentException("Unknown annotation tool.");
            }
        }
        var result = new RenderTargetBitmap(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
    private void PushUndo()
    {
        if (bitmap is null) return;
        var bytes = (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
        if (undo.Count >= 10 || bytes * (undo.Count + redo.Count + 1) > 128_000_000) undo.Clear();
        undo.Push(bitmap);
    }
    private void SetImage(BitmapSource source) { bitmap = source; image.Source = bitmap; image.Width = canvas.Width = bitmap.PixelWidth; image.Height = canvas.Height = bitmap.PixelHeight; }
    private async Task CaptureAsync(bool foreground, int delay)
    {
        Hide();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
            var bounds = Forms.SystemInformation.VirtualScreen;
            if (foreground && GetWindowRect(GetForegroundWindow(), out var rectangle)) bounds = new Drawing.Rectangle(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);
            if (bounds.Width <= 0 || bounds.Height <= 0 || (long)bounds.Width * bounds.Height > 16_000_000) throw new InvalidOperationException("Capture bounds exceed the 16 megapixel editor limit.");
            using var captured = new Drawing.Bitmap(bounds.Width, bounds.Height);
            using (var graphics = Drawing.Graphics.FromImage(captured)) graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
            using var memory = new MemoryStream(); captured.Save(memory, Drawing.Imaging.ImageFormat.Png); memory.Position = 0;
            var source = BitmapDecoder.Create(memory, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0]; source.Freeze(); undo.Clear(); redo.Clear(); SetImage(source); Status.Text = "Captured. Drag over the image to apply the selected tool.";
        }
        finally { Show(); Activate(); }
    }
}