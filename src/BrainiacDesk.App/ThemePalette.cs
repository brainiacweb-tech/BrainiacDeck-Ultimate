using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace BrainiacDesk.App;
public static class ThemePalette
{
    public static bool IsDark { get; private set; }
    public static void Apply(bool dark)
    {
        IsDark=dark;
        var values=new Dictionary<string,(string Light,string Dark)>
        {
            ["Background"]=("#EFF7FF","#101B38"),["Surface"]=("#FFFFFF","#18243D"),["SurfaceAlt"]=("#F5F9FF","#121E35"),
            ["Ink"]=("#111827","#F1F5FC"),["Muted"]=("#64748B","#AFBDD3"),["Subtle"]=("#94A3B8","#92A2BC"),["Edge"]=("#E5EDF5","#30415E"),
            ["Accent"]=("#087CFA","#72B6FF"),["AccentInk"]=("#FFFFFF","#101B38"),["Selected"]=("#DCEEFF","#223F65"),["Hero"]=("#DCEEFF","#1D3658"),
            ["BlueCard"]=("#DCEEFF","#213A58"),["LavenderCard"]=("#EAE5FF","#342E50"),["MintCard"]=("#DDF8EA","#1C403A"),["PeachCard"]=("#FFF0E1","#443529"),["PinkCard"]=("#FFE8EE","#442D40"),["CyanCard"]=("#DDF7FF","#1B3E50"),["IllustrationInk"]=("#101B38","#0D1628")
        };
        foreach(var entry in values) { var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? entry.Value.Dark : entry.Value.Light)); brush.Freeze(); Application.Current.Resources[entry.Key]=brush; }
        IconAssist.RefreshTheme();
        foreach(Window window in Application.Current.Windows) NativeWindowTheme.Apply(window);
    }
}
