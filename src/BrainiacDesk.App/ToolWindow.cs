using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace BrainiacDesk.App;
public class ToolWindow : Window
{
    private int activeOperations;
    public bool IsBusy => activeOperations > 0;
    protected readonly StackPanel Body = new() { Margin = new Thickness(24) };
    protected readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 6) };
    public ToolWindow(string title)
    {
        Title = title + " · BrainiacDesk"; Width = 980; Height = 800; MinWidth = 720; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
        SourceInitialized += (_, _) => WindowPlacement.FitToWorkArea(this);
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/BrainiacDesk.App;component/Brand/BrainiacDesk.ico"));
        var layout = new Grid(); layout.SetResourceReference(Grid.BackgroundProperty, "Background"); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(24, 18, 24, 18) };
        header.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.SemiBold });
        var brand = new TextBlock { Text = "BRAINIACDESK", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; brand.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); header.Children.Add(brand);
        var top = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) }; top.SetResourceReference(Border.BorderBrushProperty, "Edge"); layout.Children.Add(top);
        var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        Status.Margin = new Thickness(28, 12, 28, 12); Status.FontSize = 12; Status.Text = "Ready when you are."; Status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var footer = new Border { Child = Status, BorderThickness = new Thickness(0, 1, 0, 0) }; footer.SetResourceReference(Border.BorderBrushProperty, "Edge"); Grid.SetRow(footer, 2); layout.Children.Add(footer);
        Content = layout;
        Loaded += (_, _) => CompactActions();
    }
    private void CompactActions()
    {
        // Keep related actions in compact rows, leaving forms and previews at full width.
        var children = new System.Collections.Generic.List<UIElement>(); foreach (UIElement child in Body.Children) children.Add(child);
        Body.Children.Clear(); WrapPanel? actions = null;
        foreach (var child in children)
        {
            if (child is Button button) { if (actions is null) { actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 12) }; Body.Children.Add(actions); } actions.Children.Add(button); }
            else { actions = null; Body.Children.Add(child); }
        }
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (IsBusy) { e.Cancel = true; Status.Text = "Wait for the active operation to finish, or use its Cancel control, before closing."; }
        base.OnClosing(e);
    }
    protected void Note(string text) => Body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16), FontSize = 13, LineHeight = 21 });
    protected Button Action(string text, Func<Task> action, Panel? parent = null)
    {
        var button = new Button { Content = text }; button.SetResourceReference(IconAssist.SourceProperty, "Icon.Nav." + IconAssist.ForAction(text));
        button.Click += async (_, _) => { button.IsEnabled = false; activeOperations++; try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } finally { activeOperations--; button.IsEnabled = true; } };
        (parent ?? Body).Children.Add(button); return button;
    }
    protected TextBox Input(string text = "", int height = 40)
    {
        var input = new TextBox { Text = text, Height = height, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8), AcceptsReturn = height > 50, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Body.Children.Add(input); return input;
    }
    protected static string[] PickFiles(string filter = "All files|*.*") { var dialog = new OpenFileDialog { Multiselect = true, Filter = filter }; return dialog.ShowDialog() == true ? dialog.FileNames : []; }
    protected static string? PickFolder() { var dialog = new OpenFolderDialog(); return dialog.ShowDialog() == true ? dialog.FolderName : null; }
    protected static string? SaveFile(string filter, string extension) { var dialog = new SaveFileDialog { Filter = filter, DefaultExt = extension, AddExtension = true }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    protected static string AppData => BrainiacDesk.Core.LocalPaths.Data;
}