using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace BrainiacDesk.App;
public sealed class ShortcutService : IDisposable
{
    private readonly HwndSource source;
    private readonly Action activate;
    public bool Registered { get; private set; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(IntPtr window, int id);
    public ShortcutService(IntPtr handle, Action activate) { source = HwndSource.FromHwnd(handle); this.activate = activate; source.AddHook(Hook); }
    public bool Set(uint modifiers, uint key)
    {
        if (Registered) UnregisterHotKey(source.Handle, 701);
        Registered = key != 0 && RegisterHotKey(source.Handle, 701, modifiers | 0x4000, key); return Registered;
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) { if (message == 0x0312 && wParam.ToInt32() == 701) { activate(); handled = true; } return IntPtr.Zero; }
    public void Dispose() { if (Registered) UnregisterHotKey(source.Handle, 701); Registered = false; source.RemoveHook(Hook); }
}
public sealed record ShortcutPreference(string Key = "Space", bool Enabled = true);