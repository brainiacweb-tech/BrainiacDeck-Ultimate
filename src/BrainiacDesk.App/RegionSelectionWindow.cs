using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public sealed class RegionSelectionWindow : Window
{
    public Drawing.Rectangle? Selection { get; private set; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    public RegionSelectionWindow()
    {
        var bounds = CaptureImaging.Desktop; using var screenshot = CaptureImaging.Grab(bounds, false); var snapshot = CaptureImaging.ToSource(screenshot);
        Title = "Select desktop region · BrainiacDesk"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.Manual; Background = Brushes.Black; Cursor = Cursors.Cross;
        Left = bounds.Left; Top = bounds.Top; Width = Math.Max(1, bounds.Width); Height = Math.Max(1, bounds.Height);
        var canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
        var root = new Grid(); root.Children.Add(new Image { Source = snapshot, Stretch = Stretch.Fill }); root.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(60,0,0,0)), IsHitTestVisible = false }); root.Children.Add(canvas);
        var rectangle = new System.Windows.Shapes.Rectangle { Stroke = Brushes.Orange, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(20,255,255,255)), IsHitTestVisible = false }; canvas.Children.Add(rectangle);
        var label = new TextBlock { Text = "Drag to select an area · Esc to cancel", Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(16,24,39)), Padding = new Thickness(16), FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16), IsHitTestVisible = false }; root.Children.Add(label); Content = root;
        SourceInitialized += (_, _) => SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
        Point? start = null;
        canvas.MouseLeftButtonDown += (_, e) => { start = e.GetPosition(canvas); canvas.CaptureMouse(); };
        canvas.MouseMove += (_, e) => { if (start is not Point first) return; var point = e.GetPosition(canvas); var rect = new Rect(first, point); Canvas.SetLeft(rectangle, rect.Left); Canvas.SetTop(rectangle, rect.Top); rectangle.Width = rect.Width; rectangle.Height = rect.Height; label.Text = $"{Math.Round(rect.Width * bounds.Width / canvas.ActualWidth)} × {Math.Round(rect.Height * bounds.Height / canvas.ActualHeight)} pixels · Release to capture"; };
        canvas.MouseLeftButtonUp += (_, e) =>
        {
            if (start is not Point first) return; var point = e.GetPosition(canvas); canvas.ReleaseMouseCapture(); var rect = new Rect(first, point);
            var selected = CaptureGeometry.MapSelection(rect,new Size(canvas.ActualWidth,canvas.ActualHeight),bounds);
            if (selected.Width < 16 || selected.Height < 16) { start = null; label.Text = "Select at least 16 × 16 pixels · Esc to cancel"; return; }
            Selection = selected; DialogResult = true;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Selection = null; DialogResult = false; } };
        Loaded += (_, _) => { Activate(); Focus(); };
    }
}