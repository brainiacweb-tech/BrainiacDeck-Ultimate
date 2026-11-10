using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public sealed class CaptureWindow : ToolWindow
{
    private BitmapSource? bitmap, original;
    private readonly Stack<BitmapSource> undo = new(), redo = new();
    private readonly Canvas canvas = new() { Background = Brushes.Transparent };
    private readonly Image image = new();
    private readonly System.Windows.Shapes.Rectangle selection = new() { Stroke = Brushes.Orange, StrokeThickness = 2, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
    private readonly ScrollViewer preview = new() { Height = 340, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ComboBox source = new() { ItemsSource = new[] { "Primary display", "All displays", "Select region", "Foreground window" }, SelectedIndex = 0 };
    private readonly ComboBox delay = new() { ItemsSource = new[] { 1, 3, 5, 10 }, SelectedIndex = 1 };
    private readonly ComboBox tool = new() { ItemsSource = new[] { "Crop", "Arrow", "Rectangle", "Highlight", "Redact", "Text" }, SelectedIndex = 0 };
    private readonly ComboBox color = new() { ItemsSource = new[] { "Amber", "White", "Red", "Black", "Teal" }, SelectedIndex = 0 };
    private readonly ComboBox preset = new() { ItemsSource = CapturePreset.All, SelectedIndex = 0 };
    private readonly ComboBox fit = new() { ItemsSource = new[] { "Fit · keep entire image", "Fill · center crop" }, SelectedIndex = 0 };
    private readonly Slider zoom = new() { Minimum = 10, Maximum = 300, Value = 100, Width = 150 };
    private readonly Slider stroke = new() { Minimum = 1, Maximum = 20, Value = 4, Width = 100 };
    private readonly Slider font = new() { Minimum = 14, Maximum = 100, Value = 28, Width = 100 };
    private readonly Slider quality = new() { Minimum = 50, Maximum = 100, Value = 95, Width = 130 };
    private readonly CheckBox cursor = new() { Content = "Include pointer", IsChecked = true };
    private readonly TextBox annotation = new() { Text = "Annotation", Margin = new Thickness(0,8,0,8) };
    private readonly TextBlock dimensions = new() { Margin = new Thickness(0,8,0,8) };
    private readonly TextBlock empty = new() { Text = "Your canvas is ready. Capture the desktop or open an image to begin.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(24) };
    private readonly ActionRouter router;
    private Point? dragStart;
    private Rect? crop;
    private string? lastVideo, imageInput;
    public CaptureWindow(ActionRouter router) : base("Screen Capture Studio")
    {
        lastVideo=OperationJournal.Load().FirstOrDefault(x=>x.Module == "Recording" && x.Outcome == "Completed" && File.Exists(x.Output))?.Output;
        this.router = router; Width = 1180; MinWidth = 860;
        Note("Desktop screenshots and local video, ready to edit and share.");
        var target = new WrapPanel(); target.Children.Add(Field("Capture source", source, 210)); target.Children.Add(Field("Countdown · seconds", delay, 145)); target.Children.Add(cursor); Body.Children.Add(target);
        var tabs = new TabControl { Margin = new Thickness(0,12,0,0) }; Body.Children.Add(tabs);
        var screenshots = new StackPanel { Margin = new Thickness(0,16,0,0) }; var recording = new StackPanel { Margin = new Thickness(0,16,0,0) };
        tabs.Items.Add(new TabItem { Header = "Screenshots", Content = screenshots }); tabs.Items.Add(new TabItem { Header = "Recording", Content = recording });
        var captureActions = new WrapPanel(); screenshots.Children.Add(captureActions);
        var captureButton = Action("Capture screenshot", CaptureAsync, captureActions); captureButton.Style = (Style)FindResource("Primary");
        Action("Open image", OpenImageAsync, captureActions);
        var editor = Card(screenshots, "Edit your capture"); var tools = new WrapPanel(); editor.Children.Add(tools);
        tools.Children.Add(Field("Tool",tool,135)); var colorField=Field("Color",color,105); var strokeField=Field("Stroke",stroke,115); var fontField=Field("Text size",font,115); tools.Children.Add(colorField); tools.Children.Add(strokeField); tools.Children.Add(fontField); tools.Children.Add(Field("View zoom · %",zoom,165));
        editor.Children.Add(annotation);
        var viewport = new Grid(); viewport.SetResourceReference(Grid.BackgroundProperty,"Background"); canvas.Children.Add(image); canvas.Children.Add(selection); preview.Content = canvas; viewport.Children.Add(preview); viewport.Children.Add(empty); editor.Children.Add(viewport);
        var editActions = new WrapPanel { Margin = new Thickness(0,8,0,0) }; editor.Children.Add(editActions);
        Action("Apply crop", () => { if (bitmap is null || crop is not Rect area) throw new InvalidOperationException("Choose Crop and drag over the image first."); Commit(Annotate(bitmap,"Crop",area,"")); return Task.CompletedTask; },editActions);
        Action("Undo", () => { if (undo.Count > 0 && bitmap is not null) { redo.Push(bitmap); SetImage(undo.Pop()); } return Task.CompletedTask; },editActions);
        Action("Redo", () => { if (redo.Count > 0 && bitmap is not null) { PushUndo(); SetImage(redo.Pop()); } return Task.CompletedTask; },editActions);
        Action("Rotate 90°", () => { if (bitmap is not null) { var rotated = new TransformedBitmap(bitmap,new RotateTransform(90)); rotated.Freeze(); Commit(rotated); } return Task.CompletedTask; },editActions);
        Action("Reset to original", () => { if (original is not null && bitmap is not null) Commit(original); return Task.CompletedTask; },editActions);
        var export = Card(screenshots,"Export for sharing"); var exportOptions = new WrapPanel(); export.Children.Add(exportOptions); exportOptions.Children.Add(Field("Output size",preset,240)); exportOptions.Children.Add(Field("Framing",fit,215)); exportOptions.Children.Add(Field("JPEG quality",quality,145)); export.Children.Add(dimensions);
        var exportActions = new WrapPanel(); export.Children.Add(exportActions);
        Action("Preview export", () => { new ImagePreviewWindow(ExportImage()) { Owner = this }.Show(); return Task.CompletedTask; },exportActions);
        Action("Export PNG", () => SaveImageAsync(false),exportActions); Action("Export JPEG", () => SaveImageAsync(true),exportActions);
        Action("Copy image", () => { System.Windows.Clipboard.SetImage(ExportImage()); Status.Text = "Edited image copied at the selected export size."; return Task.CompletedTask; },exportActions);
        Action("Pin to Clipboard Shelf", () => { router.Shelf.AddImage(ExportImage()); return Task.CompletedTask; },exportActions);
        Action("Export PDF", ExportPdfAsync,exportActions);
        var hint = new TextBlock { Text = "PNG preserves screenshot detail. Larger exports cannot restore detail missing from a small source; social platforms may recompress uploads.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0,10,0,0) }; hint.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); export.Children.Add(hint);
        void ToolOptions()
        {
            var mode=(string)tool.SelectedItem; colorField.Visibility=mode is "Crop" or "Redact" or "Highlight" ? Visibility.Collapsed : Visibility.Visible; strokeField.Visibility=mode is "Arrow" or "Rectangle" ? Visibility.Visible : Visibility.Collapsed; fontField.Visibility=annotation.Visibility=mode == "Text" ? Visibility.Visible : Visibility.Collapsed;
        }
        tool.SelectionChanged += (_, _) => ToolOptions(); ToolOptions();
        var editorBorder=(Border)editor.Parent; var exportBorder=(Border)export.Parent; screenshots.Children.Remove(editorBorder); screenshots.Children.Remove(exportBorder);
        var columns=new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(290) }); editorBorder.Margin=new Thickness(0,0,16,0); columns.Children.Add(editorBorder);
        var exports=new ScrollViewer { Content=exportBorder, VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; Grid.SetColumn(exports,1); columns.Children.Add(exports); screenshots.Children.Add(columns);
        foreach(var field in exportOptions.Children.OfType<StackPanel>()) field.Width=220;
        foreach(var button in exportActions.Children.OfType<Button>()) { button.Padding=new Thickness(10,8,10,8); button.FontSize=12; }
        void FitEditor() { preview.Height=Math.Clamp(ActualHeight-460,160,400); exports.Height=Math.Max(300,ActualHeight-310); }
        SizeChanged += (_, _) => FitEditor(); Loaded += (_, _) => FitEditor();
        BuildRecording(recording);
        zoom.ValueChanged += (_, _) => { canvas.LayoutTransform = new ScaleTransform(zoom.Value/100,zoom.Value/100); selection.StrokeThickness=200/zoom.Value; };
        preset.SelectionChanged += (_, _) => UpdateDimensions(); fit.SelectionChanged += (_, _) => UpdateDimensions();
        canvas.MouseLeftButtonDown += (_, e) => { if (bitmap is null) return; dragStart = e.GetPosition(canvas); canvas.CaptureMouse(); selection.Visibility = Visibility.Visible; };
        canvas.MouseMove += (_, e) => { if (dragStart is Point start) ShowSelection(Clamped(start,e.GetPosition(canvas))); };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (bitmap is null || dragStart is not Point start) return; var area = Clamped(start,e.GetPosition(canvas)); canvas.ReleaseMouseCapture(); dragStart = null;
            try
            {
                if ((string)tool.SelectedItem == "Text" && area.Width < 2) area = new Rect(Math.Clamp(start.X,0,bitmap.PixelWidth-1),Math.Clamp(start.Y,0,bitmap.PixelHeight-1),1,1);
                if (area.Width < 1 || area.Height < 1) return;
                if ((string)tool.SelectedItem == "Crop") { crop = area; ShowSelection(area); Status.Text = $"Selected {area.Width:0} × {area.Height:0} pixels. Apply crop to confirm."; }
                else { var brush = (string)color.SelectedItem switch { "White" => Colors.White, "Red" => Colors.Red, "Black" => Colors.Black, "Teal" => Colors.Teal, _ => Colors.Orange }; Commit(Annotate(bitmap,(string)tool.SelectedItem,area,annotation.Text,brush,stroke.Value,font.Value,start,e.GetPosition(canvas))); }
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        };
        AllowDrop = true; Drop += async (_, e) => { if (IsBusy || e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return; try { await LoadImageAsync(paths[0]); } catch(Exception ex) { Status.Text = ex.Message; } };
    }
    private static StackPanel Field(string label, UIElement control, double width)
    {
        var panel = new StackPanel { Width = width, Margin = new Thickness(0,0,12,8) }; var text = new TextBlock { Text = label, FontSize = 12, Margin = new Thickness(0,0,0,7) }; text.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); panel.Children.Add(text); panel.Children.Add(control); return panel;
    }
    private StackPanel Card(Panel parent, string title)
    {
        var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,14) }); parent.Children.Add(new Border { Child = panel, Style = (Style)FindResource("Card"), Padding = new Thickness(18), Margin = new Thickness(0,0,0,16) }); return panel;
    }
    private void BuildRecording(Panel parent)
    {
        var card = Card(parent,"Record your screen"); card.Children.Add(new TextBlock { Text = "Local H.264 MP4 · video only · no microphone or system audio", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,16) });
        var size = new ComboBox { ItemsSource = CapturePreset.All, SelectedIndex = 2 }; var fps = new ComboBox { ItemsSource = new[] { 15,24,30 }, SelectedIndex = 2 }; var framing = new ComboBox { ItemsSource = new[] { "Fit with padding", "Fill / center crop" }, SelectedIndex = 0 };
        var settings = new WrapPanel(); settings.Children.Add(Field("Recording size",size,245)); settings.Children.Add(Field("Target frames / second",fps,180)); settings.Children.Add(Field("Framing",framing,200)); card.Children.Add(settings);
        card.Children.Add(new TextBlock { Text = "A floating timer provides Pause, Resume, and Stop. BrainiacDesk panels hide before recording. Maximum duration is two hours. Actual frame rate depends on your computer.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,16) });
        var actions = new WrapPanel(); card.Children.Add(actions);
        var start = Action("Start recording", async () =>
        {
            var path = SaveFile("MP4 video (silent)|*.mp4",".mp4"); if (path is null) return; if (File.Exists(path)) throw new IOException("Choose a new filename so existing recordings stay intact.");
            var target = source.SelectedIndex; var wait = (int)delay.SelectedItem; var pointer = cursor.IsChecked == true; var outputPreset = (CapturePreset)size.SelectedItem; var frameRate = (int)fps.SelectedItem; var fill = framing.SelectedIndex == 1;
            using var hidden = new CaptureVisibilityScope(); await Task.Delay(TimeSpan.FromSeconds(wait)); var area = ChooseArea(target); if (area is null) { Status.Text = "Recording cancelled."; return; }
            await Task.Delay(180); using var session = new VideoRecording(); var controller = new RecordingController(session); controller.Show();
            try { await session.RunAsync(area.Value,path,outputPreset,fill,pointer,frameRate); lastVideo = path; OperationJournal.Record("Recording", "Desktop", path, "Completed"); Status.Text = $"Saved {session.Elapsed:hh\\:mm\\:ss} · {session.Frames} frames · {path}"; }
            finally { controller.Close(); }
        },actions); start.Style = (Style)FindResource("Primary");
        Action("Edit last recording", () => { if (lastVideo is null) throw new InvalidOperationException("Record a clip first or open an MP4 below."); new VideoEditorWindow(lastVideo) { Owner = this }.Show(); return Task.CompletedTask; },actions);
        Action("Open MP4 editor", () => { var paths = PickFiles("MP4 video|*.mp4"); if(paths.Length > 0) new VideoEditorWindow(paths[0]) { Owner = this }.Show(); return Task.CompletedTask; },actions);
    }
    private Drawing.Rectangle? ChooseArea(int mode) => mode switch { 0 => CaptureImaging.Primary, 1 => CaptureImaging.Desktop, 2 => ChooseRegion(), _ => CaptureImaging.Foreground() };
    private static Drawing.Rectangle? ChooseRegion() { var picker = new RegionSelectionWindow(); return picker.ShowDialog() == true ? picker.Selection : null; }
    private async Task CaptureAsync()
    {
        var mode = source.SelectedIndex; var wait = (int)delay.SelectedItem; var pointer = cursor.IsChecked == true;
        using (new CaptureVisibilityScope()) { await Task.Delay(TimeSpan.FromSeconds(wait)); var area = ChooseArea(mode); if (area is null) { Status.Text = "Capture cancelled."; return; } await Task.Delay(180); using var captured = CaptureImaging.Grab(area.Value,pointer); original = CaptureImaging.ToSource(captured); imageInput = null; undo.Clear(); redo.Clear(); SetImage(original); }
        Activate(); Status.Text = $"Desktop capture ready · {bitmap!.PixelWidth} × {bitmap.PixelHeight} original pixels.";
    }
    private Task OpenImageAsync() { var paths = PickFiles("Images|*.png;*.jpg;*.jpeg;*.bmp"); return paths.Length == 0 ? Task.CompletedTask : LoadImageAsync(paths[0]); }
    private async Task LoadImageAsync(string path)
    {
        var loaded = await Task.Run(() => { using var stream = File.OpenRead(path); var frame = BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.None).Frames[0]; if((long)frame.PixelWidth*frame.PixelHeight > 32_000_000) throw new InvalidDataException("Editor limit is 32 megapixels."); var copy = new WriteableBitmap(frame); copy.Freeze(); return copy; });
        original = loaded; imageInput = Path.GetFullPath(path); undo.Clear(); redo.Clear(); SetImage(loaded); Status.Text = "Image opened. Exports use a separate file you choose.";
    }
    private Rect Clamped(Point a, Point b)
    {
        if(bitmap is null) return Rect.Empty;
        return new Rect(new Point(Math.Clamp(Math.Min(a.X,b.X),0,bitmap.PixelWidth-1),Math.Clamp(Math.Min(a.Y,b.Y),0,bitmap.PixelHeight-1)),new Point(Math.Clamp(Math.Max(a.X,b.X),0,bitmap.PixelWidth),Math.Clamp(Math.Max(a.Y,b.Y),0,bitmap.PixelHeight)));
    }
    private void ShowSelection(Rect rect) { Canvas.SetLeft(selection,rect.Left); Canvas.SetTop(selection,rect.Top); selection.Width = rect.Width; selection.Height = rect.Height; selection.Visibility = Visibility.Visible; }
    private void PushUndo() { if(bitmap is null) return; if(undo.Count >= 10 || (long)bitmap.PixelWidth*bitmap.PixelHeight*4*(undo.Count+redo.Count+1)>256_000_000) undo.Clear(); undo.Push(bitmap); }
    private void Commit(BitmapSource next) { PushUndo(); redo.Clear(); SetImage(next); Status.Text = "Edit applied. Undo or reset remains available."; }
    private void SetImage(BitmapSource value) { bitmap = value; image.Source = value; image.Width = canvas.Width = value.PixelWidth; image.Height = canvas.Height = value.PixelHeight; empty.Visibility = Visibility.Collapsed; selection.Visibility = Visibility.Collapsed; crop = null; zoom.Value = Math.Clamp(Math.Min(Math.Max(500,preview.ViewportWidth)/value.PixelWidth,320.0/value.PixelHeight)*100,10,300); UpdateDimensions(); }
    private void UpdateDimensions() { if(bitmap is null) { dimensions.Text = "Capture or open an image to see export dimensions."; return; } var selected = (CapturePreset)preset.SelectedItem; dimensions.Text = $"Source {bitmap.PixelWidth} × {bitmap.PixelHeight} → export {(selected.Width == 0 ? bitmap.PixelWidth : selected.Width)} × {(selected.Height == 0 ? bitmap.PixelHeight : selected.Height)} pixels"; }
    private BitmapSource ExportImage() => CaptureImaging.Resize(bitmap ?? throw new InvalidOperationException("Capture or open an image first."),(CapturePreset)preset.SelectedItem,fit.SelectedIndex == 1);
    private Task SaveImageAsync(bool jpeg)
    {
        var rendered = ExportImage(); var path = SaveFile(jpeg ? "JPEG|*.jpg" : "PNG|*.png",jpeg ? ".jpg" : ".png"); if(path is null) return Task.CompletedTask;
        if (imageInput is not null && string.Equals(Path.GetFullPath(path),imageInput,StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a new filename to preserve your original image.");
        BitmapEncoder encoder = jpeg ? new JpegBitmapEncoder { QualityLevel = (int)quality.Value } : new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
        WriteExport(path,stream => encoder.Save(stream)); OperationJournal.Record("Capture", "Edited image", path, "Completed"); Status.Text = $"Saved {rendered.PixelWidth} × {rendered.PixelHeight} · {new FileInfo(path).Length:N0} bytes"; return Task.CompletedTask;
    }
    private Task ExportPdfAsync()
    {
        var rendered = ExportImage(); var path = SaveFile("PDF|*.pdf",".pdf"); if(path is null) return Task.CompletedTask;
        var temporary = Path.Combine(Path.GetTempPath(),"brainiac-"+Guid.NewGuid().ToString("N")+".png");
        try { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered)); using(var stream=File.Create(temporary)) encoder.Save(stream); ConversionEngine.Pdf([temporary],path,"Combined PDF",CancellationToken.None); Status.Text = "Screenshot exported to PDF."; }
        finally { if(File.Exists(temporary)) File.Delete(temporary); } return Task.CompletedTask;
    }
    private static void WriteExport(string path, Action<Stream> write) { var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp"; try { using(var stream=new FileStream(temp,FileMode.CreateNew)) write(stream); File.Move(temp,path,true); } finally { if(File.Exists(temp)) File.Delete(temp); } }
    public static BitmapSource Annotate(BitmapSource source, string mode, Rect area, string text, Color? color = null, double stroke = 4, double textSize = 24, Point? arrowStart = null, Point? arrowEnd = null)
    {
        if(area.Width < 1 || area.Height < 1 || area.Left < 0 || area.Top < 0 || area.Right > source.PixelWidth || area.Bottom > source.PixelHeight) throw new ArgumentException("Selection is outside the image.");
        if(mode == "Crop") { var crop = new CroppedBitmap(source,new Int32Rect((int)area.X,(int)area.Y,(int)area.Width,(int)area.Height)); crop.Freeze(); return crop; }
        var visual = new DrawingVisual(); using(var dc=visual.RenderOpen())
        {
            dc.DrawImage(source,new Rect(0,0,source.PixelWidth,source.PixelHeight)); var brush = new SolidColorBrush(color ?? Colors.Orange); var pen = new Pen(brush,Math.Clamp(stroke,1,20));
            switch(mode)
            {
                case "Redact": dc.DrawRectangle(Brushes.Black,null,area); break;
                case "Highlight": dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100,255,210,0)),null,area); break;
                case "Rectangle": dc.DrawRectangle(null,pen,area); break;
                case "Arrow": var a=arrowStart ?? area.TopLeft; var b=arrowEnd ?? area.BottomRight; a=new Point(Math.Clamp(a.X,0,source.PixelWidth),Math.Clamp(a.Y,0,source.PixelHeight)); b=new Point(Math.Clamp(b.X,0,source.PixelWidth),Math.Clamp(b.Y,0,source.PixelHeight)); var vector=b-a; if(vector.Length < 1) break; dc.DrawLine(pen,a,b); vector.Normalize(); var perpendicular=new Vector(-vector.Y,vector.X); dc.DrawLine(pen,b,b-vector*18+perpendicular*9); dc.DrawLine(pen,b,b-vector*18-perpendicular*9); break;
                case "Text": if(text.Length>500) throw new ArgumentException("Text annotation limit is 500 characters."); dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(Application.Current?.TryFindResource("AppFont") as FontFamily ?? new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),Math.Clamp(textSize,14,100),brush,1),area.TopLeft); break;
                default: throw new ArgumentException("Unknown annotation tool.");
            }
        }
        var output=new RenderTargetBitmap(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Pbgra32); output.Render(visual); output.Freeze(); return output;
    }
}
