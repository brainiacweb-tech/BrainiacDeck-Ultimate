using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using BrainiacDesk.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
namespace BrainiacDesk.App;
public partial class ShellViewModel(ISettingsStore store, ILogger<ShellViewModel> logger) : ObservableObject
{
    public event Action? NavigationRequested;
    private UserSettings settings = new();
    public ToolTile[] ToolItems => BrainiacDesk.App.DashboardData.Tools;
    [ObservableProperty] private string activeNavigation="Dashboard";
    [ObservableProperty] private string page = "Dashboard";
    [ObservableProperty] private string status = "Ready · your local creative workspace.";
    [ObservableProperty] private bool isWelcome;
    [ObservableProperty] private bool isDashboard = true;
    [ObservableProperty] private bool isSettings;
    [ObservableProperty] private bool isHelp;
    [ObservableProperty] private bool isAbout;
    [ObservableProperty] private bool isDark;
    [ObservableProperty] private bool isProjects;
    [ObservableProperty] private bool isActivity;
    [ObservableProperty] private bool isFiles;
    [ObservableProperty] private bool hasContinuation;
    [ObservableProperty] private bool hasActivity;
    [ObservableProperty] private bool hasProjects;
    [ObservableProperty] private bool hasFiles;
    [ObservableProperty] private int scriptCount;
    [ObservableProperty] private int workspaceCount;
    [ObservableProperty] private int outputCount;
    public ObservableCollection<DashboardItem> ContinueItems { get; }=[];
    public ObservableCollection<DashboardItem> ProjectItems { get; }=[];
    public ObservableCollection<DashboardItem> OutputItems { get; }=[];
    public ObservableCollection<DashboardActivity> ActivityItems { get; }=[];
    [ObservableProperty] private string workspaceLabel="Local workspace";
    [ObservableProperty] private bool minimizeToTray = true;
    [ObservableProperty] private bool confirmExit = true;
    public void RefreshLocalData()
    {
        OnPropertyChanged(nameof(DashboardData));
        try
        {
            var projects=BrainiacDesk.App.DashboardData.Workspaces(); var scripts=BrainiacDesk.App.DashboardData.Scripts(); var outputs=BrainiacDesk.App.DashboardData.Outputs();
            ProjectItems.Clear(); foreach(var item in projects) ProjectItems.Add(item);
            OutputItems.Clear(); foreach(var item in outputs) OutputItems.Add(item);
            ContinueItems.Clear(); foreach(var item in scripts.Take(2).Concat(projects.Take(2)).Take(4)) ContinueItems.Add(item);
            ActivityItems.Clear(); foreach(var item in BrainiacDesk.App.DashboardData.Activity()) ActivityItems.Add(item);
            ScriptCount=scripts.Length; WorkspaceCount=projects.Length; OutputCount=outputs.Length;
            HasContinuation=ContinueItems.Count>0; HasProjects=ProjectItems.Count>0; HasFiles=OutputItems.Count>0; HasActivity=ActivityItems.Count>0;
        }
        catch(Exception ex) { Status="Local dashboard data could not be loaded: "+ex.Message; }
    }
    public string DashboardData => OperationJournal.Summary();
    public string DataLocation => LocalPaths.Data;
    public async Task InitializeAsync()
    {
        settings = await store.LoadAsync();
        IsDark = settings.Theme != "Light";
        MinimizeToTray = settings.MinimizeToTray;
        ConfirmExit = settings.ConfirmExit;
        ApplyTheme();
        if (!settings.OnboardingComplete) Navigate("Welcome");
    }
    [RelayCommand] private void Navigate(string destination)
    {
        RefreshLocalData();
        Page = destination; ActiveNavigation=destination; NavigationRequested?.Invoke();
        IsWelcome = destination == "Welcome";
        IsDashboard = destination == "Dashboard";
        IsSettings = destination == "Settings";
        IsHelp = destination == "Help";
        IsAbout = destination == "About";
        IsProjects=destination=="Projects"; IsActivity=destination=="Recent Activity"; IsFiles=destination=="Saved Files";
    }
    [RelayCommand] private async Task CompleteWelcomeAsync()
    {
        settings = settings with { OnboardingComplete = true };
        if (await PersistAsync()) Navigate("Dashboard");
    }
    [RelayCommand] private async Task SaveAsync()
    {
        settings = settings with { Theme = IsDark ? "Dark" : "Light", MinimizeToTray = MinimizeToTray, ConfirmExit = ConfirmExit };
        if (await PersistAsync()) ApplyTheme();
    }
    private async Task<bool> PersistAsync()
    {
        try { await store.SaveAsync(settings); Status = "Settings saved on this device."; return true; }
        catch (Exception ex) { logger.LogError(ex, "Settings save failed"); Status = "Could not save settings: " + ex.Message; return false; }
    }
    private void ApplyTheme()
    {
        ThemePalette.Apply(IsDark);
    }
}
