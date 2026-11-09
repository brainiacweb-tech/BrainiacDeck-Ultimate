using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BrainiacDesk.App;
public sealed class ConverterWindow : ToolWindow
{
    private readonly ObservableCollection<string> files = [];
    private readonly ListBox list;
    private readonly ComboBox format = new() { ItemsSource = new[] { "PNG", "JPG", "BMP", "TIFF", "GIF", "Combined PDF", "PDF rotate 90°", "PDF reverse" }, SelectedIndex = 0, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBox folder;
    private readonly Slider quality = new() { Minimum = 1, Maximum = 100, Value = 85, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly TextBox width;
    private CancellationTokenSource? cancellation;
    private readonly TextBox results = new() { IsReadOnly = true, Height = 100, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap };
    public ConverterWindow() : base("Smart File Converter")
    {
        Note("Drop files or choose inputs. GIF/TIFF conversion exports the first frame only. JPEG transparency becomes white. Markdown exports source text, without rich layout.");
        files.CollectionChanged += (_, _) => RefreshFormats();
        list = new ListBox { ItemsSource = files, Height = 150 }; Body.Children.Add(list);
        AllowDrop = true; Drop += (_, e) => { if (cancellation is null && e.Data.GetData(DataFormats.FileDrop) is string[] paths) Add(paths); };
        Action("Add files", () => { if (cancellation is null) Add(PickFiles("Supported inputs|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif;*.txt;*.md;*.pdf")); return Task.CompletedTask; });
        Action("Remove selected", () => { if (cancellation is null && list.SelectedItem is string selected) files.Remove(selected); return Task.CompletedTask; });
        var previewImage = new System.Windows.Controls.Image { Height = 120, Stretch = System.Windows.Media.Stretch.Uniform };
        Body.Children.Add(previewImage);
        list.SelectionChanged += (_, _) =>
        {
            previewImage.Source = null;
            if (list.SelectedItem is not string input || !ConversionEngine.ImageExtensions.Contains(Path.GetExtension(input).ToLowerInvariant())) return;
            try { using var stream = File.OpenRead(input); var thumbnail = new System.Windows.Media.Imaging.BitmapImage(); thumbnail.BeginInit(); thumbnail.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; thumbnail.DecodePixelWidth = 256; thumbnail.StreamSource = stream; thumbnail.EndInit(); thumbnail.Freeze(); previewImage.Source = thumbnail; }
            catch (Exception ex) { Status.Text = "Preview unavailable: " + ex.Message; }
        };
        Body.Children.Add(format); Note("JPEG quality"); Body.Children.Add(quality); Note("Maximum image width in pixels (0 keeps original)"); width = Input("0");
        Note("Output folder"); folder = Input(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Action("Choose output folder", () => { var path = PickFolder(); if (path is not null) folder.Text = path; return Task.CompletedTask; });
        Action("Save conversion preset", () => { LocalConfiguration.Save("conversion-preset.json", new ConversionPreset((string)format.SelectedItem, (int)quality.Value, width.Text, folder.Text)); Status.Text = "Preset saved locally."; return Task.CompletedTask; });
        Action("Load conversion preset", () => { var preset = LocalConfiguration.Load<ConversionPreset?>("conversion-preset.json", null) ?? throw new InvalidOperationException("Save a preset first."); if (!format.Items.Contains(preset.Format)) throw new InvalidOperationException("Preset format is not compatible with current inputs."); format.SelectedItem = preset.Format; quality.Value = preset.Quality; width.Text = preset.Width; folder.Text = preset.Folder; return Task.CompletedTask; });
        Action("Optimize for email", () => { format.SelectedItem = "JPG"; quality.Value = 75; width.Text = "1600"; return Task.CompletedTask; });
        Body.Children.Add(results);
        Action("Convert queue", ConvertAsync);
        Action("Cancel queue", () => { cancellation?.Cancel(); return Task.CompletedTask; });
        Note("PDF page tools: choose one PDF and specify page order such as 1,3-5,2. Extraction creates a new file; the original is preserved.");
        var pages = Input("1");
        Action("Extract / reorder PDF pages", async () => { var paths = PickFiles("PDF|*.pdf"); if (paths.Length == 0) return; var output = SaveFile("PDF|*.pdf", ".pdf"); if (output is null) return; var selection = pages.Text; await Task.Run(() => PdfPages.Extract(paths[0], output, selection, CancellationToken.None)); Status.Text = "PDF pages exported."; });
        Action("Split PDF to individual pages", async () => { var paths = PickFiles("PDF|*.pdf"); if (paths.Length == 0) return; var output = PickFolder(); if (output is null) return; await Task.Run(() => { using var document = PdfSharp.Pdf.IO.PdfReader.Open(paths[0], PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import); for (int i = 1; i <= document.PageCount; i++) PdfPages.Extract(paths[0], ConversionEngine.UniquePath(output, "Page-" + i.ToString("D4"), ".pdf"), i.ToString(), CancellationToken.None); }); Status.Text = "Split pages exported; existing files preserved."; });
        Action("View PDF metadata", async () => { var paths = PickFiles("PDF|*.pdf"); if (paths.Length > 0) Status.Text = await Task.Run(() => PdfPages.Metadata(paths[0])); });
        DesignLayout(previewImage, pages);
        Closing += (_, e) => { if (cancellation is not null) { cancellation.Cancel(); e.Cancel = true; Status.Text = "Cancelling. Close again after the current operation stops."; } };
    }
    private void DesignLayout(System.Windows.Controls.Image preview, TextBox pages)
    {
        Width = 1180; Height = 900; MinWidth = 1040;
        var buttons = Body.Children.OfType<Button>().ToDictionary(button => (string)button.Content);
        var introduction = Body.Children.OfType<TextBlock>().First();
        Body.Children.Clear();
        StackPanel Card(Panel parent, string title)
        {
            var panel = new StackPanel(); var border = new Border { Child = panel, Style = (Style)FindResource("Card"), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 16) }; parent.Children.Add(border);
            panel.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) }); return panel;
        }
        void Label(Panel parent, string title) { var label = new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(0, 8, 0, 6) }; label.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); parent.Children.Add(label); }
        void Actions(Panel parent, params string[] names) { var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; foreach (var name in names) row.Children.Add(buttons[name]); parent.Children.Add(row); }
        introduction.Margin = new Thickness(0, 0, 0, 20); introduction.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Body.Children.Add(introduction);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) }); Body.Children.Add(columns);
        var left = new StackPanel { Margin = new Thickness(0, 0, 20, 0) }; var right = new StackPanel(); Grid.SetColumn(right, 1); columns.Children.Add(left); columns.Children.Add(right);
        var queue = Card(left, "01  Add your files");
        var drop = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 14) }; drop.SetResourceReference(Border.BorderBrushProperty, "Accent");
        var dropText = new StackPanel(); dropText.Children.Add(new TextBlock { Text = "Drop files here", FontSize = 19, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center }); var detail = new TextBlock { Text = "Images, PDFs, TXT or Markdown", FontSize = 12, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center }; detail.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); dropText.Children.Add(detail); drop.Child = dropText; queue.Children.Add(drop);
        list.Height = 130; queue.Children.Add(list); Actions(queue, "Add files", "Remove selected");

        var options = Card(right, "02  Choose your output"); Label(options, "Output format"); options.Children.Add(format); Label(options, "JPEG quality"); options.Children.Add(quality); Label(options, "Maximum width · px (0 = original)"); options.Children.Add(width); Label(options, "Destination folder"); folder.TextWrapping = TextWrapping.NoWrap; folder.ToolTip = folder.Text; options.Children.Add(folder); Actions(options, "Choose output folder");
        var presetControls = new StackPanel(); Actions(presetControls, "Optimize for email", "Save conversion preset", "Load conversion preset"); options.Children.Add(new Expander { Header = "Presets", Content = presetControls });
        buttons["Convert queue"].Style = (Style)FindResource("Primary"); buttons["Convert queue"].Content = "Convert queue"; Actions(options, "Convert queue", "Cancel queue");
        var resultCard = Card(left, "Conversion results"); results.Height = 90; resultCard.Children.Add(results); left.Children.Add(new Expander { Header = "Image preview", Content = preview });
        var pdf = new StackPanel { Margin = new Thickness(0, 16, 0, 0) }; Label(pdf, "Page selection · for example 1,3–5,2"); pdf.Children.Add(pages); Actions(pdf, "Extract / reorder PDF pages", "Split PDF to individual pages", "View PDF metadata");
        Body.Children.Add(new Expander { Header = "More PDF tools", Content = pdf, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) });
    }
    private void RefreshFormats()
    {
        var previous = format.SelectedItem as string;
        var onlyImages = files.All(path => ConversionEngine.ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()));
        var onlyPdf = files.Count > 0 && files.All(path => Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        format.ItemsSource = onlyImages ? new[] { "PNG", "JPG", "BMP", "TIFF", "GIF", "Combined PDF" } : onlyPdf ? new[] { "Combined PDF", "PDF rotate 90°", "PDF reverse" } : new[] { "Combined PDF" };
        if (previous is not null && format.Items.Contains(previous)) format.SelectedItem = previous; else format.SelectedIndex = 0;
    }
    private void Add(string[] paths) { foreach (var path in paths.Where(File.Exists).Take(500 - files.Count)) if (!files.Contains(path)) files.Add(path); Status.Text = $"{files.Count} inputs selected."; }
    private async Task ConvertAsync()
    {
        if (files.Count == 0) throw new InvalidOperationException("Add at least one file.");
        if (!Directory.Exists(folder.Text)) throw new DirectoryNotFoundException("Choose an existing output folder.");
        if (!int.TryParse(width.Text, out var maxWidth) || maxWidth < 0 || maxWidth > 12000) throw new InvalidOperationException("Width must be 0–12000.");
        var operation = (string)format.SelectedItem; var inputs = files.ToArray(); var destination = folder.Text; var jpegQuality = (int)quality.Value;
        cancellation = new(); var token = cancellation.Token; var successful = 0; var failed = 0;
        try
        {
            if (operation.Contains("PDF", StringComparison.Ordinal))
            {
                var output = ConversionEngine.UniquePath(destination, "BrainiacDesk document", ".pdf");
                await Task.Run(() => ConversionEngine.Pdf(inputs, output, operation, token));
                Status.Text = "Created " + output; return;
            }
            foreach (var input in inputs)
            {
                token.ThrowIfCancellationRequested(); Status.Text = $"Processing {successful + failed + 1}/{inputs.Length}: {Path.GetFileName(input)}";
                try
                {
                    var output = ConversionEngine.UniquePath(destination, Path.GetFileNameWithoutExtension(input), "." + operation.ToLowerInvariant());
                    await StaWorker.Run(() => ConversionEngine.Image(input, output, jpegQuality, maxWidth, token));
                    successful++; results.AppendText($"{Path.GetFileName(input)} → {Path.GetFileName(output)} · {new FileInfo(input).Length:N0} → {new FileInfo(output).Length:N0} bytes" + Environment.NewLine);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { failed++; results.AppendText("FAILED " + Path.GetFileName(input) + ": " + ex.Message + Environment.NewLine); }
            }
            Status.Text = $"Finished: {successful} converted, {failed} failed. Inputs are retained for retry; existing outputs were preserved.";
        }
        catch (OperationCanceledException) { Status.Text = $"Cancelled. {successful} files completed; completed outputs are retained."; }
        finally { cancellation.Dispose(); cancellation = null; }
    }
}
public static class StaWorker
{
    public static Task Run(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
public sealed record ConversionPreset(string Format, int Quality, string Width, string Folder);