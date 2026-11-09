using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace BrainiacDesk.App;
public sealed class TeleprompterWindow : ToolWindow
{
    private readonly TextBox editor;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly Stopwatch clock = new();
    private readonly Slider speed = new() { Minimum = 5, Maximum = 180, Value = 35 };
    private readonly TextBlock reading = new() { FontSize = 32, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(50) };
    private readonly ScrollViewer scroll = new();
    private bool playing;
    private double last;
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    public TeleprompterWindow(string text = "") : base("Floating Teleprompter")
    {
        Topmost = true; Note("Paste or import your script, then start reading. Space pauses while the reading panel is focused; Home restarts. Saved scripts are ordinary local text files.");
        editor = Input(text, 120);
        var controls = new WrapPanel(); Body.Children.Add(controls);
        Action("Import TXT / Markdown", async () => { var paths = PickFiles("Scripts|*.txt;*.md"); if (paths.Length > 0) { if (new FileInfo(paths[0]).Length > 2_000_000) throw new InvalidDataException("Script exceeds 2 MB."); editor.Text = await File.ReadAllTextAsync(paths[0]); } }, controls);
        Action("Save script", async () => { var path = SaveFile("Text|*.txt", ".txt"); if (path is not null) await File.WriteAllTextAsync(path, editor.Text); }, controls);
        Action("Start / pause", () => { Toggle(); return Task.CompletedTask; }, controls);
        Action("Restart", () => { scroll.ScrollToTop(); last = clock.Elapsed.TotalSeconds; return Task.CompletedTask; }, controls);
        Note("Scroll speed · pixels per second"); Body.Children.Add(speed);
        var options = new WrapPanel(); Body.Children.Add(options);
        void SliderOption(string label, double minimum, double maximum, double value, Action<double> change)
        {
            options.Children.Add(new TextBlock { Text = label, Margin = new Thickness(6) }); var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, Width = 100, Margin = new Thickness(6) }; slider.ValueChanged += (_, _) => change(slider.Value); options.Children.Add(slider);
        }
        SliderOption("Font", 16, 80, 32, value => reading.FontSize = value);
        SliderOption("Opacity", 0.35, 1, 1, value => Opacity = value);
        SliderOption("Reading width", 250, 1000, 650, value => reading.MaxWidth = value);
        reading.MaxWidth = 650;
        var mirror = new CheckBox { Content = "Mirror text" }; mirror.Checked += (_, _) => reading.LayoutTransform = new ScaleTransform(-1, 1); mirror.Unchecked += (_, _) => reading.LayoutTransform = Transform.Identity; options.Children.Add(mirror);
        var alignment = new ComboBox { ItemsSource = new[] { "Left", "Center", "Right" }, SelectedIndex = 0 }; alignment.SelectionChanged += (_, _) => reading.TextAlignment = Enum.Parse<TextAlignment>((string)alignment.SelectedItem); options.Children.Add(alignment);
        var exclude = new CheckBox { Content = "Request capture exclusion" }; Body.Children.Add(exclude);
        exclude.Click += (_, _) => { var success = SetWindowDisplayAffinity(new WindowInteropHelper(this).Handle, exclude.IsChecked == true ? 0x11u : 0u); Status.Text = success ? "Capture exclusion preference sent to Windows; recorder support varies and exclusion is not guaranteed." : "Windows rejected the capture exclusion request."; };
        var presets = new ComboBox { ItemsSource = new[] { "Online Meeting", "YouTube Recording", "Public Speaking", "Lecture Presentation", "Interview Practice" }, SelectedIndex = 0 }; Body.Children.Add(presets);
        presets.SelectionChanged += (_, _) => { reading.FontSize = presets.SelectedIndex is 2 or 3 ? 42 : 32; speed.Value = presets.SelectedIndex == 4 ? 25 : 35; };
        var frame = new Grid { Height = 300, Focusable = true, Background = Brushes.Transparent };
        scroll.Content = reading; scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden; frame.Children.Add(scroll);
        frame.Children.Add(new Border { Height = 2, Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false }); Body.Children.Add(frame);
        frame.MouseDown += (_, _) => frame.Focus(); frame.KeyDown += (_, e) => { if (e.Key == Key.Space) { Toggle(); e.Handled = true; } else if (e.Key == Key.Home) { scroll.ScrollToTop(); e.Handled = true; } };
        editor.TextChanged += (_, _) => { reading.Text = editor.Text; Status.Text = $"Estimated reading time: {Math.Ceiling(editor.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length / 130.0)} min at 130 words/min."; };
        reading.Text = text;
        timer.Tick += (_, _) => { var now = clock.Elapsed.TotalSeconds; if (playing) { scroll.ScrollToVerticalOffset(scroll.VerticalOffset + Math.Min(now - last, 0.1) * speed.Value); if (scroll.VerticalOffset >= scroll.ScrollableHeight && scroll.ScrollableHeight > 0) playing = false; } last = now; }; clock.Start(); timer.Start();
        Closed += (_, _) => { timer.Stop(); clock.Stop(); };
    }
    private void Toggle() { playing = !playing; last = clock.Elapsed.TotalSeconds; Status.Text = playing ? "Reading · click the text panel for keyboard controls" : "Paused"; }
}