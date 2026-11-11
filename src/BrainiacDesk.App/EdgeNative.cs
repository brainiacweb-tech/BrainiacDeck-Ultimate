using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using BrainiacDesk.Core;
using Forms=System.Windows.Forms;
namespace BrainiacDesk.App;
public static class EdgeNative
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(System.Drawing.Point point,uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    public static bool Fullscreen(Forms.Screen screen, IntPtr own=default)
    {
        if(SHQueryUserNotificationState(out var state)>=0 && state is 2 or 3 or 4) return true;
        var foreground=GetForegroundWindow(); if(foreground==IntPtr.Zero || foreground==own || !GetWindowRect(foreground,out var r)) return false;
        // Explorer desktop windows are not fullscreen applications.
        var name=new System.Text.StringBuilder(128); GetClassName(foreground,name,name.Capacity);
        if(name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        var b=screen.Bounds; return r.Left<=b.Left && r.Top<=b.Top && r.Right>=b.Right && r.Bottom>=b.Bottom;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,System.Text.StringBuilder value,int count);
    public static double Scale(Forms.Screen screen) { var p=new System.Drawing.Point(screen.Bounds.Left+1,screen.Bounds.Top+1); return GetDpiForMonitor(MonitorFromPoint(p,2),0,out var x,out _)>=0 && x>0 ? x/96.0 : 1; }
    public static void PlaceSensor(Window window, System.Drawing.Rectangle bounds) => SetWindowPos(new WindowInteropHelper(window).EnsureHandle(),IntPtr.Zero,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0014);
    public static void Place(Window window,Forms.Screen screen,EdgeSettings settings)
    {
        var scale=Scale(screen); var work=screen.WorkingArea; var w=Math.Min((int)(settings.Width*scale),work.Width); var h=Math.Min((int)(settings.Height*scale),work.Height);
        var along=settings.Alignment=="Left" ? 0 : settings.Alignment=="Right" ? 1 : .5;
        int x=work.Left+(int)((work.Width-w)*along), y=settings.Dock=="Bottom" ? work.Bottom-h : work.Top;
        if(settings.Dock is "Left" or "Right") { x=settings.Dock=="Left" ? work.Left : work.Right-w; y=work.Top+(int)((work.Height-h)*along); }
        window.Width=w/scale; window.Height=h/scale; var handle=new WindowInteropHelper(window).EnsureHandle(); SetWindowPos(handle,IntPtr.Zero,x,y,w,h,0x0014);
    }
    public static IntPtr NoMouseActivate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) { if(message==0x21) { handled=true; return new IntPtr(3); } return IntPtr.Zero; }
}
/// <summary>Thin WPF edge sensors. No global mouse hook or continuous hidden polling.</summary>
public sealed class EdgeActivation : IDisposable
{
    private readonly List<Window> strips=[];
    private readonly Action<Forms.Screen> reveal;
    private readonly Func<bool> suspended;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer dwell;
    private Forms.Screen? candidate;
    private EdgeSettings settings=new();
    private bool waitForLeave, disposed;
    public event Action? DisplaysChanged;
    public EdgeActivation(Action<Forms.Screen> reveal,Func<bool> suspended)
    {
        this.reveal=reveal; this.suspended=suspended; dispatcher=Dispatcher.CurrentDispatcher;
        dwell=new DispatcherTimer(); dwell.Tick+=Dwell;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=Displays;
    }
    public void Configure(EdgeSettings value)
    {
        if(disposed) return;
        settings=value; dwell.Stop(); candidate=null; foreach(var strip in strips) strip.Close(); strips.Clear(); if(!value.Enabled) return;
        foreach(var screen in Forms.Screen.AllScreens.Where(s=>value.Monitor=="Pointer" || s.DeviceName==value.Monitor))
        {
            var scale=EdgeNative.Scale(screen); var b=screen.Bounds; int size=value.Sensitivity;
            var strip=new Window { Title="Brainiac Edge activation",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Opacity=.01,ShowActivated=false,ShowInTaskbar=false,Topmost=true,WindowStartupLocation=WindowStartupLocation.Manual,MinWidth=0,MinHeight=0,Width=(value.Dock is "Left" or "Right" ? size : b.Width)/scale,Height=(value.Dock is "Left" or "Right" ? b.Height : size)/scale,Left=(value.Dock=="Right" ? b.Right-size : b.Left)/scale,Top=(value.Dock=="Bottom" ? b.Bottom-size : b.Top)/scale,Content=new Border { Background=Brushes.Black },AllowDrop=true };
            HwndSource? source=null; strip.SourceInitialized+=(_,_)=> { source=HwndSource.FromHwnd(new WindowInteropHelper(strip).Handle); source.AddHook(EdgeNative.NoMouseActivate); };
            strip.Closed+=(_,_)=>source?.RemoveHook(EdgeNative.NoMouseActivate);
            strip.MouseEnter+=(_,_)=>Begin(screen); strip.MouseLeave+=(_,_)=> { dwell.Stop(); candidate=null; waitForLeave=false; };
            strip.DragEnter+=(_,e)=> { e.Effects=DragDropEffects.Copy; Begin(screen); e.Handled=true; }; strip.DragLeave+=(_,_)=> { dwell.Stop(); candidate=null; waitForLeave=false; };
            var physical=new System.Drawing.Rectangle(value.Dock=="Right" ? b.Right-size : b.Left,value.Dock=="Bottom" ? b.Bottom-size : b.Top,value.Dock is "Left" or "Right" ? size : b.Width,value.Dock is "Left" or "Right" ? b.Height : size);
            strip.Show(); EdgeNative.PlaceSensor(strip,physical); strips.Add(strip);
        }
    }
    public void DisarmUnderPointer()
    {
        var point=Forms.Cursor.Position; var b=Forms.Screen.FromPoint(point).Bounds;
        waitForLeave=EdgeRules.AtEdge(point.X,point.Y,b.Left,b.Top,b.Right,b.Bottom,settings.Dock,settings.Sensitivity); dwell.Stop(); candidate=null;
    }
    private void Begin(Forms.Screen screen) { if(waitForLeave || suspended()) return; candidate=screen; dwell.Interval=TimeSpan.FromMilliseconds(settings.DwellMilliseconds); dwell.Start(); }
    private void Dwell(object? sender,EventArgs args) { dwell.Stop(); if(candidate is { } screen && !suspended() && (!settings.RespectFullscreen || !EdgeNative.Fullscreen(screen))) reveal(screen); candidate=null; }
    private void Displays(object? sender,EventArgs args) => dispatcher.BeginInvoke(()=> { if(disposed) return; Configure(settings); DisplaysChanged?.Invoke(); });
    public void Dispose() { disposed=true; Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=Displays; dwell.Stop(); foreach(var strip in strips) strip.Close(); strips.Clear(); }
}
