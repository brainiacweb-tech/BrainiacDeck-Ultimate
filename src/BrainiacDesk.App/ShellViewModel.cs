using System;
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
    private UserSettings settings = new();
    [ObservableProperty] private string page = "Dashboard";
    [ObservableProperty] private string status = "Local storage ready. Clipboard monitoring is off.";
    [ObservableProperty] private bool isWelcome;
    [ObservableProperty] private bool isDashboard = true;
    [ObservableProperty] private bool isSettings;
    [ObservableProperty] private bool isHelp;
    [ObservableProperty] private bool isAbout;
    [ObservableProperty] private bool isDark = true;
    [ObservableProperty] private bool minimizeToTray = true;
    [ObservableProperty] private bool confirmExit = true;
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
        Page = destination;
        IsWelcome = destination == "Welcome";
        IsDashboard = destination == "Dashboard";
        IsSettings = destination == "Settings";
        IsHelp = destination == "Help";
        IsAbout = destination == "About";
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
        var resources = Application.Current.Resources;
        void Set(string key, string value) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        Set("Background", IsDark ? "#101827" : "#F3F5F8"); Set("Surface", IsDark ? "#182438" : "#FFFFFF");
        Set("Ink", IsDark ? "#FFFFFF" : "#101827"); Set("Muted", IsDark ? "#AAB6C5" : "#48566A"); Set("Edge", IsDark ? "#304054" : "#CBD2DC");
        Set("Accent", IsDark ? "#F59E0B" : "#B45309");
        IconAssist.RefreshTheme();
    }
}