using System;
using System.Linq;
using System.Windows;
namespace BrainiacDesk.App;
public sealed class CaptureVisibilityScope : IDisposable
{
    private readonly Window[] windows;
    public CaptureVisibilityScope()
    {
        windows = Application.Current.Windows.Cast<Window>().Where(window => window.IsVisible && window.WindowState != WindowState.Minimized).ToArray();
        foreach (var window in windows) window.Hide();
    }
    public void Dispose() { foreach (var window in windows) if (window.IsLoaded) window.Show(); }
}