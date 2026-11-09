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
        var splashPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(32) };
        splashPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "BrainiacDesk Ultimate", FontSize = 30, FontWeight = FontWeights.SemiBold });
        splashPanel.Children.Add(new System.Windows.Controls.TextBlock { Text = "One Desktop. Unlimited Possibilities.", Margin = new Thickness(0, 12, 0, 24) });
        splashPanel.Children.Add(new System.Windows.Controls.ProgressBar { IsIndeterminate = true, Height = 4 });
        var splash = new Window { Title = "Initializing BrainiacDesk", Width = 510, Height = 220, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = splashPanel };
        splash.Show();
        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Render);
        services = new ServiceCollection().AddLogging().AddSingleton<ISettingsStore>(_ => new SettingsStore(Path.Combine(LocalPaths.Data, "settings.db"))).AddSingleton<ShellViewModel>().AddSingleton<MainWindow>().BuildServiceProvider();
        try
        {
            var model = services.GetRequiredService<ShellViewModel>();
            await model.InitializeAsync();
            var window = services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            splash.Close();
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show("BrainiacDesk could not initialize local settings. Your data was not reset.\n\n" + ex.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { services?.Dispose(); base.OnExit(e); }
}