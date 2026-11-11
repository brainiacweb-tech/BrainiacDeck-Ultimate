using System;
using System.Linq;
using System.Windows;
namespace BrainiacDesk.App;
public sealed class CaptureVisibilityScope : IDisposable
{
    private static int active;
    public static bool IsActive => active > 0;
    private bool disposed;
    private readonly Window[] windows;
    public CaptureVisibilityScope()
    {
        active++;
        windows = Application.Current.Windows.Cast<Window>().Where(window => window.IsVisible && window.WindowState != WindowState.Minimized).ToArray();
        foreach (var window in windows) window.Hide();
    }
    public void Dispose() { if(disposed) return; disposed=true; active--; foreach (var window in windows) if (window.IsLoaded) window.Show(); }
}