using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
namespace BrainiacDesk.App;
public static class NativeWindowTheme
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
    public static void Initialize(Window window) => window.SourceInitialized+=(_,_)=>Apply(window);
    public static void Apply(Window window)
    {
        if(window.WindowStyle==WindowStyle.None) return;
        var handle=new WindowInteropHelper(window).Handle; if(handle==IntPtr.Zero) return;
        int dark=ThemePalette.IsDark ? 1 : 0;
        // Unsupported attributes simply return an HRESULT on older Windows versions.
        _=DwmSetWindowAttribute(handle,20,ref dark,sizeof(int));
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)) return;
        int Colour(string key) { var c=((SolidColorBrush)Application.Current.FindResource(key)).Color; return c.R | (c.G<<8) | (c.B<<16); }
        int caption=Colour("Background"),text=Colour("Ink"),border=Colour("Edge");
        _=DwmSetWindowAttribute(handle,35,ref caption,sizeof(int));
        _=DwmSetWindowAttribute(handle,36,ref text,sizeof(int));
        _=DwmSetWindowAttribute(handle,34,ref border,sizeof(int));
    }
}
