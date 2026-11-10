using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
namespace BrainiacDesk.App;
public sealed class RecordingController : Window
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(IntPtr handle, uint affinity);
    public RecordingController(VideoRecording recording)
    {
        Title = "Recording · BrainiacDesk"; Width = 390; Height = 155; ResizeMode = ResizeMode.NoResize; WindowStyle = WindowStyle.ToolWindow; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual; Left = SystemParameters.WorkArea.Right - 410; Top = SystemParameters.WorkArea.Top + 16;
        SetResourceReference(BackgroundProperty,"Surface");
        var panel = new StackPanel { Margin = new Thickness(14) }; var elapsed = new TextBlock { Text = "RECORDING · 00:00:00 · Video only", FontSize = 16, FontWeight = FontWeights.SemiBold }; panel.Children.Add(elapsed);
        var row = new StackPanel { Orientation = Orientation.Horizontal }; var pause = new Button { Content = "Pause" }; var stop = new Button { Content = "Stop and save" }; pause.Click += (_, _) => { recording.TogglePause(); pause.Content = recording.Paused ? "Resume" : "Pause"; }; stop.Click += (_, _) => { stop.IsEnabled = false; pause.IsEnabled = false; recording.Stop(); }; row.Children.Add(pause); row.Children.Add(stop); panel.Children.Add(row); Content = panel;
        timer.Tick += (_, _) => elapsed.Text = $"{(recording.Stopping ? "SAVING" : recording.Paused ? "PAUSED" : "RECORDING")} · {recording.Elapsed:hh\\:mm\\:ss} · Video only"; timer.Start();
        SourceInitialized += (_, _) => { if (!SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle,0x11)) Title = "Recording · toolbar may appear in video"; };
        Closing += (_, _) => recording.Stop(); Closed += (_, _) => timer.Stop();
    }
}