using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BrainiacDesk.App;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
internal static class FreshInstallTests
{
    private sealed class Host:Application { protected override void OnStartup(StartupEventArgs e) { } }
    public static void Run()
    {
        var prior=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var folder=Path.Combine(Path.GetTempPath(),"BrainiacDeck-Fresh-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",folder);
        var app=new Host { Resources=new ResourceDictionary { Source=new Uri("pack://application:,,,/BrainiacDesk.App;component/Design/Theme.xaml") },ShutdownMode=ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        int count=0; void Check(bool condition,string name) { if(!condition) throw new Exception("FAIL: "+name); count++; Console.WriteLine("PASS: "+name); }
        void Pump() { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(10); }
        void Await(Task task) { while(!task.IsCompleted) Pump(); task.GetAwaiter().GetResult(); }
        IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
        MainWindow? main=null;
        try
        {
            var model=new ShellViewModel(new SettingsStore(Path.Combine(folder,"settings.db")),NullLogger<ShellViewModel>.Instance); Await(model.InitializeAsync()); model.RefreshLocalData(); model.ConfirmExit=false;
            Check(model.IsWelcome,"Fresh profile starts with onboarding");
            Check(model.ContinueItems.Count==0 && model.ProjectItems.Count==0 && model.OutputItems.Count==0,"No seeded projects, continuation cards or outputs");
            Check(model.ActivityItems.Count==0,"No invented activity on first launch");
            Check(model.ScriptCount==0 && model.WorkspaceCount==0 && model.OutputCount==0,"Dashboard counts start at genuine zero values");
            Check(new ScriptLibrary().Scripts.Count==0,"Script library has no sample scripts");
            Check(WorkspaceWindow.LoadWorkspaces().Count==0,"Workspaces have no seeded collections");
            Check(OperationJournal.Load().Count()==0,"Operation history starts empty");
            var edge=new EdgeStore(Path.Combine(folder,"settings.db")); var pins=edge.SearchAsync(""); Await(pins); Check(pins.Result.Count==0,"Edge storage starts without mock pins");
            main=new MainWindow(model); app.MainWindow=main; main.Show(); Pump();
            var shelf=new ClipboardShelfWindow(); shelf.Show(); Pump();
            Check(Walk(shelf).OfType<ListBox>().First().Items.Count==0,"Clipboard Shelf starts empty");
            Check(Walk(shelf).OfType<CheckBox>().Single(x=>Equals(x.Content,"Enable clipboard monitoring for this session (explicit consent)")).IsChecked==false,"Fresh install leaves clipboard monitoring disabled");
            shelf.Close(); model.RefreshLocalData(); Check(model.ActivityItems.Count==0 && model.ScriptCount==0 && model.WorkspaceCount==0,"Opening the shell does not populate sample content");
            Console.WriteLine($"{count} fresh-install checks passed. Isolated profile: {folder}");
        }
        finally { main?.Close(); app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",prior); }
    }
}
