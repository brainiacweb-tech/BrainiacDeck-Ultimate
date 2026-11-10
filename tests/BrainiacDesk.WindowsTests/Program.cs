using System.IO;
using BrainiacDesk.App;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Pdf.IO;
internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        if (args.Contains("--teleprompter-tests")) { TeleprompterTests.Run(); return; }
        if (args.Contains("--visual-review")) { VisualReview.Run(); return; }
        var folder = Path.Combine(Path.GetTempPath(), "BrainiacDeskConversionTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        int count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
        void Fails(Action action, string name) { bool failed = false; try { action(); } catch { failed = true; } Check(failed, name); }
        try
        {
            var input = Path.Combine(folder, "source.png");
            var bitmap = BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 64).ToArray(), 16);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(input)) png.Save(stream);
            foreach (var extension in new[] { "png", "jpg", "bmp", "tiff", "gif" })
            {
                var output = Path.Combine(folder, "output." + extension); ConversionEngine.Image(input, output, 80, 2, CancellationToken.None);
                using var stream = File.OpenRead(output); var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Check(decoded.Frames[0].PixelWidth == 2, "Image conversion + resize: " + extension);
            }
            Fails(() => ConversionEngine.Image(input, Path.Combine(folder, "output.jpg"), 80, 0, CancellationToken.None), "Existing output protected");
            var corrupt = Path.Combine(folder, "corrupt.png"); File.WriteAllText(corrupt, "invalid");
            Fails(() => ConversionEngine.Image(corrupt, Path.Combine(folder, "bad.jpg"), 80, 0, CancellationToken.None), "Corrupt image rejected");
            Fails(() => ConversionEngine.Image(input, Path.Combine(folder, "bad.webp"), 80, 0, CancellationToken.None), "Unsupported output rejected");
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Fails(() => ConversionEngine.Image(input, Path.Combine(folder, "cancelled.png"), 80, 0, cancellation.Token), "Cancelled conversion rejected");
            Check(!File.Exists(Path.Combine(folder, "cancelled.png")), "Cancellation leaves no output");
            var pdf = Path.Combine(folder, "images.pdf"); ConversionEngine.Pdf([input, input], pdf, "Combined PDF", CancellationToken.None);
            using (var document = PdfReader.Open(pdf, PdfDocumentOpenMode.Import)) Check(document.PageCount == 2, "Images to PDF");
            var text = Path.Combine(folder, "script.txt"); File.WriteAllText(text, string.Join('\n', Enumerable.Repeat("Offline document conversion.", 100)));
            var textPdf = Path.Combine(folder, "text.pdf"); ConversionEngine.Pdf([text], textPdf, "Combined PDF", CancellationToken.None);
            using (var document = PdfReader.Open(textPdf, PdfDocumentOpenMode.Import)) Check(document.PageCount >= 2, "Text PDF pagination");
            var extractedPdf = Path.Combine(folder, "extracted.pdf"); PdfPages.Extract(pdf, extractedPdf, "2,1", CancellationToken.None);
            using (var document = PdfReader.Open(extractedPdf, PdfDocumentOpenMode.Import)) Check(document.PageCount == 2, "PDF extract/reorder");
            Check(PdfPages.Parse("1,3-4", 4).SequenceEqual(new[] { 0, 2, 3 }), "PDF page range parsing");
            Fails(() => PdfPages.Parse("0,8", 4), "Invalid PDF range rejected");
            var merged = Path.Combine(folder, "merged.pdf"); ConversionEngine.Pdf([pdf, textPdf], merged, "PDF rotate 90°", CancellationToken.None);
            using (var document = PdfReader.Open(merged, PdfDocumentOpenMode.Import)) Check(document.PageCount >= 4 && document.Pages[0].Rotate == 90, "PDF merge and rotation");
            Check(ClipboardShelfWindow.Transform(" A   B\nC ", "Normalize whitespace") == "A B C", "Smart paste normalization");
            Check(ClipboardShelfWindow.Transform("Hello", "Uppercase") == "HELLO", "Smart paste uppercase");
            Check(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "Temporary outputs cleaned");
            var crop = CaptureWindow.Annotate(bitmap, "Crop", new System.Windows.Rect(1, 1, 2, 2), "");
            Check(crop.PixelWidth == 2 && crop.PixelHeight == 2, "Screenshot region crop");
            var redacted = CaptureWindow.Annotate(bitmap, "Redact", new System.Windows.Rect(0, 0, 4, 4), "");
            var pixels = new byte[64]; redacted.CopyPixels(pixels, 16, 0);
            Check(pixels[0] == 0 && pixels[1] == 0 && pixels[2] == 0 && pixels[3] == 255, "Screenshot redaction exports opaque black pixels");
            foreach (var tool in new[] { "Arrow", "Rectangle", "Text", "Highlight" }) Check(CaptureWindow.Annotate(bitmap, tool, new System.Windows.Rect(0, 0, 3, 3), "A").PixelWidth == 4, "Annotation renders: " + tool);
            Check(ActionRouter.Score(ActionRouter.Catalog[0], "convert") > ActionRouter.Score(ActionRouter.Catalog[0], "zqx"), "Deterministic command matching");
            CaptureStudioTests.Run(folder, Check, Fails);
            Console.WriteLine($"{count} Windows checks passed.");
        }
        finally { Directory.Delete(folder, true); }
    }
}
