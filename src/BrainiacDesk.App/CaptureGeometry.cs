using System;
using System.Windows;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public static class CaptureGeometry
{
    public static Drawing.Rectangle MapSelection(Rect selection, Size view, Drawing.Rectangle pixels)
    {
        if(!double.IsFinite(view.Width) || !double.IsFinite(view.Height) || view.Width <= 0 || view.Height <= 0 || !double.IsFinite(selection.Left) || !double.IsFinite(selection.Top) || !double.IsFinite(selection.Right) || !double.IsFinite(selection.Bottom)) throw new ArgumentException("Invalid capture coordinates.");
        int left=(int)Math.Clamp(Math.Floor(selection.Left*pixels.Width/view.Width),0,pixels.Width), top=(int)Math.Clamp(Math.Floor(selection.Top*pixels.Height/view.Height),0,pixels.Height);
        int right=(int)Math.Clamp(Math.Ceiling(selection.Right*pixels.Width/view.Width),0,pixels.Width), bottom=(int)Math.Clamp(Math.Ceiling(selection.Bottom*pixels.Height/view.Height),0,pixels.Height);
        return new(pixels.Left+left,pixels.Top+top,Math.Max(0,right-left),Math.Max(0,bottom-top));
    }
}
