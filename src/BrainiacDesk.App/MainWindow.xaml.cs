using System;
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
        tray = new Forms.NotifyIcon { Text = "BrainiacDesk Ultimate", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Restore();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && model.MinimizeToTray) Hide(); };
    }
    private void Restore() { Show(); WindowState = WindowState.Normal; Activate(); }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (model.ConfirmExit && MessageBox.Show(this, "Exit BrainiacDesk?", "BrainiacDesk", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { e.Cancel = true; return; }
        tray.Visible = false;
        tray.Dispose();
        base.OnClosing(e);
        Application.Current.Shutdown();
    }
}