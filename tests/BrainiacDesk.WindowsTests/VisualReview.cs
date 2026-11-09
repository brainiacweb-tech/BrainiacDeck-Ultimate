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
        var model = new ShellViewModel(new SettingsStore(Path.Combine(BrainiacDesk.Core.LocalPaths.Data, "settings.db")), NullLogger<ShellViewModel>.Instance);
        model.InitializeAsync().GetAwaiter().GetResult(); model.NavigateCommand.Execute("Dashboard"); model.ConfirmExit = false;
        var main = new MainWindow(model); app.MainWindow = main; main.Show();
        void Capture(Window window, string name)
        {
            window.UpdateLayout();
            var content = (FrameworkElement)window.Content;
            var image = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); image.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(Path.Combine(folder, name + ".png")); encoder.Save(stream); Console.WriteLine("Rendered: " + name);
        }
        Capture(main, "dashboard-dark");
        var converter = new ConverterWindow { Owner = main }; converter.Show(); Capture(converter, "converter-dark"); converter.Close();
        model.IsDark = false; model.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult(); Capture(main, "dashboard-light");
        main.Close();
    }
}
