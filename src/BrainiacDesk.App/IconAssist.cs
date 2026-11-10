using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace BrainiacDesk.App;
public static class IconAssist
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(ImageSource), typeof(IconAssist), new PropertyMetadata(null));
    public static void SetSource(DependencyObject target, ImageSource value) => target.SetValue(SourceProperty, value);
    public static ImageSource? GetSource(DependencyObject target) => (ImageSource?)target.GetValue(SourceProperty);
    public static ImageSource Get(string name) => (ImageSource)Application.Current.FindResource("Icon." + name);
    public static void RefreshTheme()
    {
        var resources = Application.Current.Resources;
        var names = new[] { "Dashboard", "Convert", "Shelf", "Prompt", "Capture", "Files", "Workflow", "Workspace", "Shortcuts", "Settings", "Privacy", "Help", "About", "Command", "Arrow", "Upload", "Play", "Cancel", "Folder", "Save", "Search" };
        foreach (var name in names)
        foreach (var prefix in new[] { "", "Nav." })
        {
            var key = "Icon." + prefix + name;
            if (resources[key] is not DrawingImage original) continue;
            var image = original.CloneCurrentValue();
            if (image.Drawing is DrawingGroup group)
                foreach (var child in group.Children)
                    if (child is GeometryDrawing { Pen: not null } shape) shape.Pen.Brush = (Brush)resources[prefix.Length == 0 ? "Accent" : "Muted"];
            resources[key] = image;
        }
    }
    public static string ForAction(string label)
    {
        var text = label.ToLowerInvariant();
        if (text.Contains("cancel") || text.Contains("remove") || text.Contains("delete") || text.Contains("clear")) return "Cancel";
        if (text.Contains("save") || text.Contains("export")) return "Save";
        if (text.Contains("choose") || text.Contains("folder") || text.Contains("select files")) return "Folder";
        if (text.Contains("import") || text.Contains("add files") || text.Contains("load")) return "Upload";
        if (text.Contains("capture") || text.Contains("image") || text.Contains("screenshot")) return "Capture";
        if (text.Contains("copy") || text.Contains("clipboard") || text.Contains("pin")) return "Shelf";
        if (text.Contains("convert") || text.Contains("pdf")) return "Convert";
        if (text.Contains("start") || text.Contains("run") || text.Contains("apply")) return "Play";
        if (text.Contains("find") || text.Contains("search") || text.Contains("inspect")) return "Search";
        return "Arrow";
    }
}
