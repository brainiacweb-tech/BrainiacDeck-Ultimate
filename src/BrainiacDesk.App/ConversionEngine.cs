using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
namespace BrainiacDesk.App;
public static class ConversionEngine
{
    public static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif"];
    public static string UniquePath(string folder, string name, string extension)
    {
        for (int i = 0; i < 10000; i++)
        {
            var candidate = Path.Combine(folder, name + (i == 0 ? "" : $" ({i})") + extension);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
        throw new IOException("Too many output name collisions.");
    }
    public static void Image(string input, string output, int quality, int maxWidth, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!ImageExtensions.Contains(Path.GetExtension(input).ToLowerInvariant())) throw new NotSupportedException("Unsupported image input.");
        using var source = File.OpenRead(input);
        var decoder = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        if ((long)frame.PixelWidth * frame.PixelHeight > 60_000_000) throw new InvalidDataException("Image exceeds the 60 megapixel safety limit.");
        BitmapSource bitmap = frame;
        if (maxWidth > 0 && frame.PixelWidth > maxWidth) bitmap = new TransformedBitmap(frame, new ScaleTransform((double)maxWidth / frame.PixelWidth, (double)maxWidth / frame.PixelWidth));
        BitmapEncoder encoder = Path.GetExtension(output).ToLowerInvariant() switch
        {
            ".png" => new PngBitmapEncoder(), ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 1, 100) },
            ".bmp" => new BmpBitmapEncoder(), ".tiff" or ".tif" => new TiffBitmapEncoder { Compression = TiffCompressOption.Zip }, ".gif" => new GifBitmapEncoder(), _ => throw new NotSupportedException("Unsupported output.")
        };
        if (encoder is JpegBitmapEncoder)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)); dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight)); }
            var flattened = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32); flattened.Render(visual); bitmap = flattened;
        }
        token.ThrowIfCancellationRequested();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        WriteNew(output, stream => encoder.Save(stream), token);
    }
    public static void WriteNew(string output, Action<Stream> write, CancellationToken token)
    {
        var temporary = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { using (var stream = new FileStream(temporary, FileMode.CreateNew)) { write(stream); } token.ThrowIfCancellationRequested(); File.Move(temporary, output, false); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Pdf(string[] inputs, string output, string operation, CancellationToken token)
    {
        using var document = new PdfDocument();
        foreach (var input in inputs)
        {
            token.ThrowIfCancellationRequested();
            if (Path.GetExtension(input).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
                var indices = operation == "PDF reverse" ? Enumerable.Range(0, source.PageCount).Reverse() : Enumerable.Range(0, source.PageCount);
                foreach (var index in indices) { token.ThrowIfCancellationRequested(); var page = document.AddPage(source.Pages[index]); if (operation == "PDF rotate 90°") page.Rotate = (page.Rotate + 90) % 360; }
            }
            else if (Path.GetExtension(input).Equals(".txt", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(input).Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                if (new FileInfo(input).Length > 2_000_000) throw new InvalidDataException("Text input exceeds 2 MB.");
                XGraphics? graphics = null;
                try
                {
                    double y = 0; var font = new XFont("Segoe UI", 11);
                    foreach (var line in File.ReadLines(input))
                    {
                        // Fixed-width wrapping prevents unbounded lines and provides deterministic pagination.
                        var pieces = line.Length == 0 ? new[] { " " } : Enumerable.Range(0, (line.Length + 79) / 80).Select(i => line.Substring(i * 80, Math.Min(80, line.Length - i * 80)));
                        foreach (var piece in pieces)
                        {
                            token.ThrowIfCancellationRequested();
                            if (graphics is null || y > 780) { graphics?.Dispose(); graphics = XGraphics.FromPdfPage(document.AddPage()); y = 50; }
                            graphics.DrawString(piece, font, XBrushes.Black, new XPoint(40, y)); y += 17;
                        }
                    }
                    if (graphics is null) document.AddPage();
                }
                finally { graphics?.Dispose(); }
            }
            else
            {
                if (!ImageExtensions.Contains(Path.GetExtension(input).ToLowerInvariant())) throw new NotSupportedException("Only supported images, text, Markdown source, and PDFs can be combined.");
                using var image = XImage.FromFile(input); var page = document.AddPage();
                using var graphics = XGraphics.FromPdfPage(page);
                var scale = Math.Min((page.Width.Point - 60) / image.PixelWidth, (page.Height.Point - 60) / image.PixelHeight);
                graphics.DrawImage(image, 30, 30, image.PixelWidth * scale, image.PixelHeight * scale);
            }
        }
        if (document.PageCount == 0) throw new InvalidDataException("No pages to export.");
        WriteNew(output, stream => document.Save(stream, false), token);
    }
}