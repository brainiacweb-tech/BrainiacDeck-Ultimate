using System;
using System.Windows;
namespace BrainiacDesk.App;
public static class WindowPlacement
{
    public static void FitToWorkArea(Window window)
    {
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, area.Width);
        window.MinHeight = Math.Min(window.MinHeight, area.Height);
        window.Width = Math.Min(window.Width, area.Width);
        window.Height = Math.Min(window.Height, area.Height);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = area.Left + Math.Max(0, (area.Width - window.Width) / 2);
        window.Top = area.Top + Math.Max(0, (area.Height - window.Height) / 2);
    }
}
