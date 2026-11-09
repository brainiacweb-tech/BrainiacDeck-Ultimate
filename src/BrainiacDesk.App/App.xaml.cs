using System;
using System.IO;
using System.Windows;
using BrainiacDesk.Core;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace BrainiacDesk.App;
public partial class App : Application
{
    private ServiceProvider? services;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        services = new ServiceCollection().AddLogging().AddSingleton<ISettingsStore>(_ => new SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrainiacDesk", "settings.db"))).AddSingleton<ShellViewModel>().AddSingleton<MainWindow>().BuildServiceProvider();
        try
        {
            var model = services.GetRequiredService<ShellViewModel>();
            await model.InitializeAsync();
            var window = services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("BrainiacDesk could not initialize local settings. Your data was not reset.\n\n" + ex.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { services?.Dispose(); base.OnExit(e); }
}