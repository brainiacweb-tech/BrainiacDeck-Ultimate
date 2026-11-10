using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public sealed class VideoEditorWindow : ToolWindow
{
    private readonly string input;
    private readonly MediaElement player = new() { Height = 230, LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform, IsMuted = true };
    private readonly Canvas canvas = new() { Background = Brushes.Transparent };
    private readonly Image image = new();
    private readonly System.Windows.Shapes.Rectangle marker = new() { Stroke = Brushes.Orange, StrokeThickness = 3, IsHitTestVisible = false };
    private readonly Slider magnification = new() { Minimum = 1, Maximum = 4, Value = 1, Width = 200, AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft };
    private readonly TextBlock cropLabel = new() { Margin = new Thickness(0,8,0,12), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox start = new() { Text = "0", Width = 110 };
    private readonly TextBox end = new() { Text = "30", Width = 110 };
    private readonly ComboBox size = new() { ItemsSource = CapturePreset.All, SelectedIndex = 2 };
    private readonly ComboBox fit = new() { ItemsSource = new[] { "Fit with padding", "Fill / center crop" }, SelectedIndex = 0 };
    private int width, height;
    private Drawing.Rectangle? selected;
    private CancellationTokenSource? cancellation;
    public VideoEditorWindow(string path) : base("Video Editor")
    {
        input = Path.GetFullPath(path); Width = 1100;
        Note("Trim, crop, and zoom your clip, then export a new H.264 MP4. This editor exports video only; imported audio is not included. Your original file is preserved.");
        var previewTabs = new TabControl { Margin = new Thickness(0,0,0,16) }; var playbackPanel = new StackPanel { Margin = new Thickness(0,16,0,0) }; previewTabs.Items.Add(new TabItem { Header = "Playback", Content = playbackPanel }); Body.Children.Add(previewTabs);
        player.Source = new Uri(input); playbackPanel.Children.Add(player);
        var playback = new WrapPanel(); playbackPanel.Children.Add(playback);
        Action("Play preview", () => { player.Play(); return Task.CompletedTask; },playback);
        Action("Pause preview", () => { player.Pause(); return Task.CompletedTask; },playback);
        Action("Restart preview", () => { player.Position = TimeSpan.Zero; player.Play(); return Task.CompletedTask; },playback);
        player.MediaOpened += (_, _) => { if(player.NaturalDuration.HasTimeSpan) { end.Text = player.NaturalDuration.TimeSpan.TotalSeconds.ToString("0.###",CultureInfo.InvariantCulture); Status.Text = $"Source duration {player.NaturalDuration.TimeSpan:mm\\:ss}. Preview is muted."; } };
        player.MediaFailed += (_, e) => Status.Text = "Preview unavailable: " + e.ErrorException.Message;
        var cropPanel = new StackPanel(); var cropCard = new Border { Child = cropPanel, Style = (Style)FindResource("Card"), Padding = new Thickness(18), Margin = new Thickness(0,16,0,16) }; previewTabs.Items.Add(new TabItem { Header = "Crop & Zoom", Content = cropCard, IsSelected = true });
        cropPanel.ToolTip = "Drag to select the area to keep. Zoom further into its center.";
        canvas.Children.Add(image); canvas.Children.Add(marker); cropPanel.Children.Add(new Viewbox { Child = canvas, Height = 180, Stretch = Stretch.Uniform });
        var zoomRow = new WrapPanel { Margin = new Thickness(0,10,0,0) }; zoomRow.Children.Add(new TextBlock { Text = "Zoom · 1× to 4×", Margin = new Thickness(0,10,16,0) }); zoomRow.Children.Add(magnification); cropPanel.Children.Add(zoomRow); cropPanel.Children.Add(cropLabel);
        Action("Reset crop / zoom", () => { selected = null; magnification.Value = 1; UpdateCrop(); return Task.CompletedTask; },zoomRow);
        Point? drag = null;
        canvas.MouseLeftButtonDown += (_, e) => { if(width == 0) return; drag = e.GetPosition(canvas); canvas.CaptureMouse(); };
        canvas.MouseMove += (_, e) => { if(drag is not Point point) return; var area = new Rect(point,e.GetPosition(canvas)); Canvas.SetLeft(marker,area.Left); Canvas.SetTop(marker,area.Top); marker.Width = area.Width; marker.Height = area.Height; };
        canvas.MouseLeftButtonUp += (_, e) => { if(drag is not Point point) return; canvas.ReleaseMouseCapture(); drag = null; var area = new Rect(point,e.GetPosition(canvas)); var mapped=CaptureGeometry.MapSelection(area,new Size(width,height),new Drawing.Rectangle(0,0,width,height)); if(mapped.Width >= 16 && mapped.Height >= 16) { selected = mapped; UpdateCrop(); } };
        magnification.ValueChanged += (_, _) => UpdateCrop();
        var settings = new WrapPanel(); Body.Children.Add(settings); settings.Children.Add(Field("Start · seconds",start,130)); settings.Children.Add(Field("End · seconds",end,130)); settings.Children.Add(Field("Export size",size,250)); settings.Children.Add(Field("Framing",fit,210));
        var actions = new WrapPanel(); Body.Children.Add(actions);
        var export = Action("Export edited MP4", async () =>
        {
            if(width == 0 || cancellation is not null) throw new InvalidOperationException("Wait until the video preview loads.");
            if(!double.TryParse(start.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var from) || !double.TryParse(end.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out var to)) throw new ArgumentException("Enter trim times in seconds, using a decimal point.");
            var path = SaveFile("MP4 video (silent)|*.mp4",".mp4"); if(path is null) return;
            if(File.Exists(path) || Path.GetFullPath(path).Equals(input,StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a new filename to preserve your original.");
            var crop = EffectiveCrop(); var preset=(CapturePreset)size.SelectedItem; var fill=fit.SelectedIndex == 1;
            cancellation = new(); player.Pause();
            try { var progress = new Progress<double>(value => Status.Text = $"Exporting edited video · {value:P0}"); await Task.Run(() => VideoEditing.Export(input,path,from,to,crop,preset,fill,progress,cancellation.Token)); Status.Text = "Edited MP4 saved: " + path; }
            catch(OperationCanceledException) { Status.Text = "Video export cancelled; original preserved."; }
            finally { cancellation.Dispose(); cancellation = null; }
        },actions); export.Style=(Style)FindResource("Primary");
        Action("Cancel export", () => { cancellation?.Cancel(); return Task.CompletedTask; },actions);
        Loaded += async (_, _) =>
        {
            try
            {
                var result = await Task.Run(() => { using var reader = new NativeVideoReader(input); using var frame = reader.Read() ?? throw new InvalidDataException("Video contains no frames."); return (Frame: CaptureImaging.ToSource(frame.Bitmap), Duration: reader.DurationSeconds); });
                width = result.Frame.PixelWidth; height = result.Frame.PixelHeight; image.Source=result.Frame; marker.StrokeThickness=Math.Max(1,result.Frame.PixelHeight/180.0*2); if(result.Duration > 0) end.Text = result.Duration.ToString("0.###",CultureInfo.InvariantCulture); image.Width=canvas.Width=width; image.Height=canvas.Height=height; UpdateCrop(); Status.Text=$"Loaded {width} × {height}. Choose your crop, zoom and trim range. Export creates a separate silent MP4.";
            }
            catch(Exception ex) { Status.Text = "Cannot open video: " + ex.Message; }
        };
        Body.Children.Remove(settings); Body.Children.Remove(actions); var location=Body.Children.IndexOf(previewTabs); Body.Children.Insert(location,settings); Body.Children.Insert(location+1,actions);
        Closing += (_, _) => cancellation?.Cancel(); Closed += (_, _) => { player.Stop(); player.Close(); };
    }
    private static StackPanel Field(string label, UIElement control, double width) { var panel=new StackPanel { Width=width, Margin=new Thickness(0,0,12,16) }; panel.Children.Add(new TextBlock { Text=label, FontSize=12, Margin=new Thickness(0,0,0,8) }); panel.Children.Add(control); return panel; }
    private Drawing.Rectangle EffectiveCrop()
    {
        return VideoEditing.ZoomCrop(selected ?? new Drawing.Rectangle(0,0,width,height),magnification.Value);
    }
    private void UpdateCrop() { if(width == 0) return; var area=EffectiveCrop(); Canvas.SetLeft(marker,area.X); Canvas.SetTop(marker,area.Y); marker.Width=area.Width; marker.Height=area.Height; cropLabel.Text=$"Keep {area.Width} × {area.Height} pixels at {area.X}, {area.Y} · zoom {magnification.Value:0.0}×"; }
}
