using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace BrainiacDesk.App;
public class ToolWindow : Window
{
    private FrameworkElement? workspaceContent;
    private bool workspaceLoaded;
    private readonly Button closePage=new() { Content="Close tool",Visibility=Visibility.Collapsed,ToolTip="Close this tool and release its session resources" };
    public bool IsEmbedded => workspaceContent is not null;
    public Window DialogOwner => IsEmbedded ? Owner ?? this : this;
    protected double LayoutWidth => workspaceContent?.ActualWidth ?? ActualWidth;
    protected double LayoutHeight => workspaceContent?.ActualHeight ?? ActualHeight;
    protected event EventHandler? WorkspaceSizeChanged;
    public FrameworkElement AttachToWorkspace(Window owner)
    {
        if(workspaceContent is not null) return workspaceContent;
        Owner=owner; closePage.Visibility=Visibility.Visible;
        // Clipboard listeners need an HWND even though their view is hosted in the shell.
        System.Windows.Interop.WindowInteropHelper helper=new(this); helper.EnsureHandle();
        workspaceContent=(FrameworkElement)Content; Content=null;
        workspaceContent.Loaded+=(_,_)=> { if(workspaceLoaded) return; workspaceLoaded=true; RaiseEvent(new RoutedEventArgs(LoadedEvent,this)); };
        workspaceContent.SizeChanged+=(_,_)=>WorkspaceSizeChanged?.Invoke(this,EventArgs.Empty);
        return workspaceContent;
    }
    public void OpenInWorkspace()
    {
        if(Application.Current.MainWindow is MainWindow shell) shell.ShowToolPage(this);
        else Show();
    }
    private int activeOperations;
    public bool IsBusy => activeOperations > 0;
    protected readonly StackPanel Body = new() { Margin = new Thickness(24) };
    protected readonly TextBlock Status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 6) };
    public ToolWindow(string title)
    {
        SetResourceReference(BackgroundProperty,"Background"); SetResourceReference(ForegroundProperty,"Ink"); NativeWindowTheme.Initialize(this);
        Title = title + " · BrainiacDesk"; Width = 980; Height = 800; MinWidth = 560; MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
        SourceInitialized += (_, _) => WindowPlacement.FitToWorkArea(this);
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/BrainiacDesk.App;component/Brand/BrainiacDeck-logo.png"));
        var layout = new Grid(); layout.SetResourceReference(Grid.BackgroundProperty, "Background"); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(28, 24, 28, 24) }; header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        header.Children.Add(new TextBlock { Text = title, TextWrapping=TextWrapping.Wrap, FontFamily=(System.Windows.Media.FontFamily)FindResource("AppFont"), FontSize = 28, FontWeight = FontWeights.Bold });
        var brand = new TextBlock { Text = "BRAINIACDECK", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; brand.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Grid.SetColumn(brand,1); brand.Margin=new Thickness(20,0,0,0); header.Children.Add(brand);
        closePage.Click+=(_,_)=>Close(); Grid.SetColumn(closePage,1); header.Children.Remove(brand); header.Children.Add(closePage);
        var top = new Border { Child = header, BorderThickness = new Thickness(0, 0, 0, 1) }; top.SetResourceReference(Border.BorderBrushProperty, "Edge"); top.SetResourceReference(Border.BackgroundProperty,"Surface"); layout.Children.Add(top);
        var form=new Border { Child=Body,CornerRadius=new CornerRadius(22),Margin=new Thickness(20),BorderThickness=new Thickness(1) }; form.SetResourceReference(Border.BackgroundProperty,"Surface"); form.SetResourceReference(Border.BorderBrushProperty,"Edge");
        var scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
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
    protected static void Accessible(DependencyObject control,string name) => System.Windows.Automation.AutomationProperties.SetName(control,name);
    protected void Note(string text) => Body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16), FontSize = 12, LineHeight = 20 });
    protected Button Action(string text, Func<Task> action, Panel? parent = null)
    {
        var button = new Button { Content = text }; button.SetResourceReference(IconAssist.SourceProperty, "Icon.Nav." + IconAssist.ForAction(text));
        button.Click += async (_, _) => { button.IsEnabled = false; activeOperations++; try { await action(); } catch (Exception ex) { Status.Text = ex.Message; } finally { activeOperations--; button.IsEnabled = true; } };
        (parent ?? Body).Children.Add(button); return button;
    }
    protected TextBox Input(string text = "", int height = 40)
    {
        var input = new TextBox { Text = text, MinHeight = height, Height = height > 50 ? height : double.NaN, Margin = new Thickness(0, 6, 0, 6), Padding = new Thickness(8), AcceptsReturn = height > 50, TextWrapping = height > 50 ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalContentAlignment = height > 50 ? VerticalAlignment.Top : VerticalAlignment.Center, VerticalScrollBarVisibility = height > 50 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden };
        Body.Children.Add(input); return input;
    }
    protected static string[] PickFiles(string filter = "All files|*.*") { var dialog = new OpenFileDialog { Multiselect = true, Filter = filter }; return dialog.ShowDialog() == true ? dialog.FileNames : []; }
    protected static string? PickFolder() { var dialog = new OpenFolderDialog(); return dialog.ShowDialog() == true ? dialog.FolderName : null; }
    protected static string? SaveFile(string filter, string extension) { var dialog = new SaveFileDialog { Filter = filter, DefaultExt = extension, AddExtension = true }; return dialog.ShowDialog() == true ? dialog.FileName : null; }
    protected static string AppData => BrainiacDesk.Core.LocalPaths.Data;
}