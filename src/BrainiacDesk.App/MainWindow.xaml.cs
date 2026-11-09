using System;
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
        this.model = model;
        DataContext = model;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open BrainiacDesk", null, (_, _) => Restore());
        menu.Items.Add("Exit", null, (_, _) => Close());
        tray = new Forms.NotifyIcon { Text = "BrainiacDesk Ultimate", Icon = LoadTrayIcon(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Restore();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && model.MinimizeToTray) Hide(); };
    }
    private static System.Drawing.Icon LoadTrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Brand/BrainiacDesk.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(stream); return (System.Drawing.Icon)icon.Clone();
    }
    private ActionRouter? router;
    private ShortcutService? shortcuts;
    private CommandWindow? commands;
    private ActionRouter Router => router ??= new ActionRouter(this);
    private void OpenConverter(object sender, RoutedEventArgs e) => Router.Open("convert");
    private void OpenShelf(object sender, RoutedEventArgs e) => Router.Open("shelf");
    private void OpenTeleprompter(object sender, RoutedEventArgs e) => Router.Open("prompt");
    private void OpenTool(object sender, RoutedEventArgs e) { try { if (sender is System.Windows.Controls.Button { Tag: string id }) Router.Open(id); } catch (Exception ex) { model.Status = "Could not open tool: " + ex.Message; } }
    private void OpenCommands(object sender, RoutedEventArgs e) => ShowCommands();
    private void ShowCommands() { try { if (commands is null) { commands = new CommandWindow(Router) { Owner = this }; commands.Closed += (_, _) => commands = null; } commands.Show(); commands.Activate(); } catch (Exception ex) { model.Status = ex.Message; } }
    private void OpenShortcuts(object sender, RoutedEventArgs e) { if (shortcuts is not null) new ShortcutWindow(shortcuts) { Owner = this }.Show(); }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        shortcuts = new ShortcutService(new System.Windows.Interop.WindowInteropHelper(this).Handle, ShowCommands);
        try { var preference = LocalConfiguration.Load("shortcut.json", new ShortcutPreference()); if (preference.Enabled && !shortcuts.Set(6, (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(Enum.Parse<System.Windows.Input.Key>(preference.Key)))) model.Status = "Ctrl+Shift shortcut is occupied. Open Shortcut Studio to choose another."; }
        catch (Exception ex) { model.Status = "Could not load shortcut preference: " + ex.Message; }
    }
    private void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (OwnedWindows.OfType<ToolWindow>().Any(window => window.IsBusy)) { e.Cancel = true; model.Status = "Wait for active operations or cancel them before exiting."; return; }
        if (model.ConfirmExit && MessageBox.Show(this, "Exit BrainiacDesk?", "BrainiacDesk", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
        shortcuts?.Dispose();
        tray.Visible = false;
        var icon = tray.Icon; tray.Dispose(); icon?.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }
}