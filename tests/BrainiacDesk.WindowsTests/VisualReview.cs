using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BrainiacDesk.App;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
internal static class VisualReview
{
    public static void Run()
    {
        var folder = Path.GetFullPath("artifacts/ui-review"); Directory.CreateDirectory(folder);
        Environment.SetEnvironmentVariable("BRAINIACDESK_DATA", Path.Combine(Path.GetTempPath(), "BrainiacDesk-Design-" + Guid.NewGuid().ToString("N")));
        var app = new App(); app.InitializeComponent();
        foreach (var weight in new[] { FontWeights.Normal, FontWeights.Medium, FontWeights.SemiBold, FontWeights.Bold })
        {
            var face = new Typeface((FontFamily)app.FindResource("AppFont"), FontStyles.Normal, weight, FontStretches.Normal);
            if (!face.TryGetGlyphTypeface(out var glyph) || !glyph.FamilyNames.Values.Any(name => name.Contains("Raleway", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Bundled Raleway did not resolve: " + weight);
            Console.WriteLine("Verified Raleway " + weight + ": " + glyph.FontUri);
        }
        var model = new ShellViewModel(new SettingsStore(Path.Combine(BrainiacDesk.Core.LocalPaths.Data, "settings.db")), NullLogger<ShellViewModel>.Instance);
        model.InitializeAsync().GetAwaiter().GetResult(); model.NavigateCommand.Execute("Dashboard"); model.ConfirmExit = false;
        var main = new MainWindow(model); app.MainWindow = main; main.Show();
        void Capture(Window window, string name)
        {
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var image = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream); Console.WriteLine("Rendered: " + name);
        }
        Capture(main, "dashboard-dark");
        using (new CaptureVisibilityScope())
        {
            if(app.Windows.Cast<Window>().Any(window => window.IsVisible)) throw new InvalidOperationException("App windows were not hidden for desktop capture.");
            var bounds=CaptureImaging.Desktop; using var pixels=CaptureImaging.Grab(new System.Drawing.Rectangle(bounds.Left,bounds.Top,64,64),false);
            if(pixels.Width != 64 || pixels.Height != 64) throw new InvalidOperationException("Desktop capture did not return the requested pixel area.");
            Console.WriteLine("Verified live desktop capture and app-window hiding (pixels discarded, not saved).");
        }
        if(!main.IsVisible) throw new InvalidOperationException("Main window was not restored after capture.");
        var converter = new ConverterWindow { Owner = main }; converter.Show(); Capture(converter, "converter-dark"); converter.Close();
        var capture = new CaptureWindow(new ActionRouter(main)) { Owner = main }; capture.Show(); Capture(capture,"capture-studio-dark");
        System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            yield return root; for(int index=0;index<VisualTreeHelper.GetChildrenCount(root);index++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,index))) yield return child;
        }
        var tabs = Walk(capture).OfType<System.Windows.Controls.TabControl>().First(); tabs.SelectedIndex = 1; Capture(capture,"recording-studio-dark"); capture.Close();
        var fixture = Path.Combine(folder,"video-fixture.mp4"); if(File.Exists(fixture)) File.Delete(fixture);
        using(var writer = new NativeVideoWriter(fixture,160,96,15)) { var pixels=Enumerable.Repeat((byte)180,160*96*4).ToArray(); for(int i=0;i<15;i++) writer.Append(pixels,i*TimeSpan.TicksPerSecond/15,TimeSpan.TicksPerSecond/15); writer.Complete(); }
        var video = new VideoEditorWindow(fixture) { Owner = main }; video.Show(); var ready=System.Diagnostics.Stopwatch.StartNew(); while(!Walk(video).OfType<System.Windows.Controls.Canvas>().Any(node => node.Width == 160) && ready.Elapsed.TotalSeconds < 10) { video.Dispatcher.Invoke(() => {},System.Windows.Threading.DispatcherPriority.ApplicationIdle); System.Threading.Thread.Sleep(20); } Capture(video,"video-editor-dark"); video.Close();
        model.IsDark = false; model.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Capture(main, "dashboard-light");
        main.Close();
    }
}
