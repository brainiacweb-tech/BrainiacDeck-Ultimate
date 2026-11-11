using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BrainiacDesk.Core;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;
namespace BrainiacDesk.App;
public partial class MainWindow : Window
{
    private readonly ShellViewModel model;
    private readonly Forms.NotifyIcon tray;
    public MainWindow(ShellViewModel model)
    {
        InitializeComponent();
        NativeWindowTheme.Initialize(this);
        this.model = model;
        DataContext = model; model.RefreshLocalData();
        model.NavigationRequested+=ShowShellPage;
        SizeChanged+=(_,_)=>UpdateResponsiveLayout(); Loaded+=(_,_)=>UpdateResponsiveLayout();
        searchDelay.Tick+=async (_,_)=> { searchDelay.Stop(); await RefreshSearchAsync(); };
        OperationJournal.Changed+=JournalChanged;
        DataContext = model; Activated += (_, _) => { model.RefreshLocalData(); UpdateWorkspaceName(); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open BrainiacDeck", null, (_, _) => Restore());
        menu.Items.Add("Exit", null, (_, _) => Close());
        tray = new Forms.NotifyIcon { Text = "BrainiacDeck Ultimate", Icon = LoadTrayIcon(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Restore();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && model.MinimizeToTray) Hide(); };
    }
    private static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/BrainiacDesk.App;component/Brand/BrainiacDesk.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(stream); return (System.Drawing.Icon)icon.Clone();
    }
    private ActionRouter? router;
    private ShortcutService? shortcuts;
    private CommandWindow? commands;
    private ToolWindow? activeToolPage;
    private PromptEditorWindow? promptPage;
    private TeleprompterWindow? promptOverlay;
    private ShortcutWindow? shortcutPage;
    public void ShowToolPage(ToolWindow tool)
    {
        if(!IsVisible) Show(); if(WindowState==WindowState.Minimized) WindowState=WindowState.Normal; Activate(); SearchPopup.IsOpen=false;
        var content=tool.AttachToWorkspace(this);
        activeToolPage=tool; ToolPageHost.Content=content;
        ToolPageHost.Visibility=Visibility.Visible; PageScroll.Visibility=Visibility.Collapsed;
        tool.Closed-=ToolPageClosed; tool.Closed+=ToolPageClosed;
        content.Focus();
    }
    private void ToolPageClosed(object? sender,EventArgs e)
    {
        if(!ReferenceEquals(activeToolPage,sender)) return;
        ToolPageHost.Content=null; activeToolPage=null;
        ToolPageHost.Visibility=Visibility.Collapsed; PageScroll.Visibility=Visibility.Visible;
        model.NavigateCommand.Execute("Dashboard");
    }
    private void ShowShellPage()
    {
        ToolPageHost.Content=null; activeToolPage=null;
        ToolPageHost.Visibility=Visibility.Collapsed; PageScroll.Visibility=Visibility.Visible;
    }
    public void OpenPromptWorkspace(PromptScript? script=null)
    {
        if(promptPage is not null && script is not null) { promptPage.Close(); if(promptPage is not null) return; }
        if(promptPage is null)
        {
            var library=new ScriptLibrary(); if(script is not null) library.Select(script.Id);
            promptPage=new PromptEditorWindow(null,library,script?.Text ?? library.Scripts.FirstOrDefault(x=>x.Id==library.Active)?.Text ?? "",text=> {
                if(promptOverlay is null) { promptOverlay=new TeleprompterWindow(); promptOverlay.Closed+=(_,_)=>promptOverlay=null; }
                promptOverlay.SetScript(text); promptOverlay.Show();
            });
            promptPage.Closed+=(_,_)=>promptPage=null;
        }
        model.ActiveNavigation="prompt"; ShowToolPage(promptPage);
    }

    private ActionRouter Router => router ??= new ActionRouter(this);
    private void OpenConverter(object sender, RoutedEventArgs e) => Router.Open("convert");
    private void OpenShelf(object sender, RoutedEventArgs e) => Router.Open("shelf");
    private void OpenTeleprompter(object sender, RoutedEventArgs e) => Router.Open("prompt");
    private void OpenTool(object sender, RoutedEventArgs e) { try { if (sender is System.Windows.Controls.Button { Tag: string id }) { model.ActiveNavigation=id; Router.Open(id); } } catch (Exception ex) { model.Status = "Could not open tool: " + ex.Message; } }
    private void OpenCommands(object sender, RoutedEventArgs e) => ShowCommands();
    private void ShowCommands() { try { if (commands is null) { commands = new CommandWindow(Router) { Owner = this }; commands.Closed += (_, _) => commands = null; } ShowToolPage(commands); } catch (Exception ex) { model.Status = ex.Message; } }
    private void OpenShortcuts(object sender, RoutedEventArgs e) { if (shortcuts is not null) { if(shortcutPage is null) { shortcutPage=new ShortcutWindow(shortcuts,Router.Edge); shortcutPage.Closed+=(_,_)=>shortcutPage=null; } ShowToolPage(shortcutPage); } }
    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowPlacement.FitToWorkArea(this);
        shortcuts = new ShortcutService(new System.Windows.Interop.WindowInteropHelper(this).Handle, ShowCommands);
        try { var preference = LocalConfiguration.Load("shortcut.json", new ShortcutPreference()); if (preference.Enabled && !shortcuts.Set(6, (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(Enum.Parse<System.Windows.Input.Key>(preference.Key)))) model.Status = "Ctrl+Shift shortcut is occupied. Open Shortcut Studio to choose another."; }
        catch (Exception ex) { model.Status = "Could not load shortcut preference: " + ex.Message; }
        try { await Router.Edge.InitializeAsync(); } catch(Exception ex) { model.Status="Brainiac Edge initialization failed: "+ex.Message; }
    }
    private readonly DispatcherTimer searchDelay=new() { Interval=TimeSpan.FromMilliseconds(180) };
    private int searchGeneration;
    private void UpdateWorkspaceName()
    {
        try { model.WorkspaceLabel=WorkspaceWindow.LoadWorkspaces().FirstOrDefault(x=>x.Id==Router.Edge.Model.WorkspaceId)?.Name ?? "Local workspace"; } catch(Exception ex) { model.Status="Workspace label unavailable: "+ex.Message; }
    }
    private void JournalChanged()=>Dispatcher.BeginInvoke(()=>model.RefreshLocalData());
    private void UpdateResponsiveLayout()
    {
        HeroTitle.FontSize=ActualHeight<800 ? 28 : 32; HeroTitle.LineHeight=ActualHeight<800 ? 34 : 39; HeroBanner.Padding=new Thickness(ActualHeight<800 ? 22 : 28);
        var wide=ActualWidth>=1240; ContextPanel.Visibility=wide ? Visibility.Visible : Visibility.Collapsed; ContextColumn.Width=wide ? new GridLength(280) : new GridLength(0); DashboardMain.Margin=new Thickness(0,0,wide ? 20 : 0,0);
        var art=ActualWidth>=1080; HeroArt.Visibility=art ? Visibility.Visible : Visibility.Collapsed; HeroArtColumn.Width=new GridLength(art ? (wide ? 200 : 270) : 0);
        SidebarColumn.Width=new GridLength(ActualWidth<960 ? 220 : 248); WorkspaceChip.Visibility=ActualWidth<1100 ? Visibility.Collapsed : Visibility.Visible;
        SearchPopup.Width=Math.Min(560,Math.Max(260,GlobalSearch.ActualWidth));
        foreach(var grid in Descendants(Toolkit).OfType<System.Windows.Controls.Primitives.UniformGrid>()) grid.Columns=ActualWidth<980 ? 1 : 2;
    }
    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root; for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    private void LaunchTile(object sender,RoutedEventArgs e) { if(sender is FrameworkElement { DataContext:ToolTile tile }) { model.ActiveNavigation=tile.Id; Router.Open(tile.Id); } }
    private void CreateProject(object sender,RoutedEventArgs e)=>Router.Open("workspaces");
    private void ExploreTools(object sender,RoutedEventArgs e) { model.NavigateCommand.Execute("Dashboard"); Dispatcher.BeginInvoke(()=>Toolkit.BringIntoView()); }
    private void NewScript(object sender,RoutedEventArgs e)=>OpenPromptWorkspace();
    private void OpenDashboardItem(object sender,RoutedEventArgs e) { if(sender is FrameworkElement { DataContext:DashboardItem item }) OpenItem(item); }
    private void OpenItem(DashboardItem item)
    {
        try
        {
            SearchPopup.IsOpen=false;
            switch(item.Payload)
            {
                case DeskAction action: Router.Open(action.Id); break;
                case Workspace workspace: model.WorkspaceLabel=workspace.Name; Router.Edge.SelectWorkspace(workspace.Id); foreach(var id in workspace.Actions) Router.Open(id); break;
                case PromptScript script: OpenPromptWorkspace(script); break;
                case OperationRecord operation:
                    if(!File.Exists(operation.Output) && !Directory.Exists(operation.Output)) throw new FileNotFoundException("The original output is missing. Your history was retained.");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(operation.Output) { UseShellExecute=true }); break;
                case EdgeItem pin: Router.Edge.SelectWorkspace(pin.WorkspaceId); Router.Edge.Model.Kind="All"; Router.Edge.Model.Query=pin.Title; Router.Edge.Show(); break;
            }
        }
        catch(Exception ex) { model.Status=ex.Message; }
    }
    private void GlobalSearchChanged(object sender,TextChangedEventArgs e) { if(model is null) return; searchDelay.Stop(); searchGeneration++; SearchResults.ItemsSource=null; SearchPopup.IsOpen=false; if(GlobalSearch.Text.Trim().Length==0) { SearchPopup.IsOpen=false; return; } searchDelay.Start(); }
    private async Task RefreshSearchAsync()
    {
        var request=++searchGeneration; var query=GlobalSearch.Text;
        try
        {
            var items=await Task.Run(()=>BrainiacDesk.App.DashboardData.SearchAsync(query,Router.Edge.Store)); if(request!=searchGeneration) return;
            SearchResults.ItemsSource=items; SearchResults.SelectedIndex=items.Length>0 ? 0 : -1; SearchStatus.Text=items.Length==0 ? "No matching tools or saved content." : $"{items.Length} results from your local workspace"; SearchPopup.IsOpen=true;
        }
        catch(Exception ex) { model.Status="Search unavailable: "+ex.Message; }
    }
    private void GlobalSearchKey(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { SearchPopup.IsOpen=false; e.Handled=true; } else if(e.Key==Key.Down && SearchResults.Items.Count>0) { SearchResults.Focus(); SearchResults.SelectedIndex=0; e.Handled=true; } else if(e.Key==Key.Enter && SearchResults.SelectedItem is DashboardItem item) { OpenItem(item); e.Handled=true; } }
    private void OpenSearchResult(object sender,MouseButtonEventArgs e) { if(SearchResults.SelectedItem is DashboardItem item) OpenItem(item); }
    private void SearchResultKey(object sender,KeyEventArgs e) { if(e.Key==Key.Enter && SearchResults.SelectedItem is DashboardItem item) { OpenItem(item); e.Handled=true; } else if(e.Key==Key.Escape) { SearchPopup.IsOpen=false; GlobalSearch.Focus(); } }
    private void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (Application.Current.Windows.OfType<ToolWindow>().Any(window => window.IsBusy)) { e.Cancel = true; model.Status = "Wait for active operations or cancel them before exiting."; return; }
        if (model.ConfirmExit && MessageBox.Show(this, "Exit BrainiacDesk?", "BrainiacDesk", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
        // Ask editors to preserve or discard changes before disposing native services.
        foreach(var tool in Application.Current.Windows.OfType<ToolWindow>().ToArray())
        {
            bool closed=false; EventHandler handler=(_,_)=>closed=true; tool.Closed+=handler; tool.Close(); tool.Closed-=handler;
            if(!closed) { e.Cancel=true; return; }
        }
        promptOverlay?.Close(); model.NavigationRequested-=ShowShellPage;
        OperationJournal.Changed-=JournalChanged; searchDelay.Stop();
        router?.Dispose();
        shortcuts?.Dispose();
        tray.Visible = false;
        var icon = tray.Icon; tray.Dispose(); icon?.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }
}