using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BrainiacDesk.App;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
internal static class NavigationTests
{
    private sealed class Host : Application { protected override void OnStartup(StartupEventArgs e) { } }
    private sealed class BusyTool : ToolWindow
    {
        public readonly TaskCompletionSource Completion=new();
        public BusyTool():base("Busy navigation test") { Action("Start pending action",()=>Completion.Task); }
    }
    public static void Run()
    {
        var previous=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var folder=Path.Combine(Path.GetTempPath(),"BrainiacDeck-Navigation-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",folder);
        var app=new Host { Resources=new ResourceDictionary { Source=new Uri("pack://application:,,,/BrainiacDesk.App;component/Design/Theme.xaml") },ShutdownMode=ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        int count=0; void Check(bool valid,string name) { if(!valid) throw new Exception("FAIL: "+name); count++; Console.WriteLine("PASS: "+name); }
        void Pump() { for(int i=0;i<15;i++) { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(10); } }
        IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
        Button Button(string name)=>Walk(app.MainWindow).OfType<Button>().First(x=>x.IsVisible && Equals(x.Content,name));
        void Click(string name) { Button(name).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Pump(); }
        var model=new ShellViewModel(new SettingsStore(Path.Combine(folder,"settings.db")),NullLogger<ShellViewModel>.Instance); MainWindow? main=null; ActionRouter? router=null;
        try
        {
            var init=model.InitializeAsync(); while(!init.IsCompleted) Pump(); init.GetAwaiter().GetResult(); model.ConfirmExit=false; model.NavigateCommand.Execute("Dashboard"); main=new MainWindow(model); app.MainWindow=main; main.Show(); Pump(); router=new ActionRouter(main);
            foreach(var id in new[] { "convert","shelf","files","workflow","capture","workspaces","privacy","prompt" })
            {
                router.Open(id); Pump();
                Check(app.Windows.Cast<Window>().Count(x=>x.IsVisible && x.Title!="Brainiac Edge activation")==1,id+" opens in the existing shell only");
                Check(((ContentControl)main.FindName("ToolPageHost")).Content is FrameworkElement { IsVisible:true },id+" exposes its real controls inside shell");
                model.NavigateCommand.Execute("Dashboard"); Pump(); Check(((ScrollViewer)main.FindName("PageScroll")).IsVisible,"Dashboard restores even when already the selected page");
            }
            router.Open("shelf"); Pump(); var shelf=router.Shelf; shelf.AddSnippet("Retained embedded clipboard item"); router.Open("files"); router.Open("shelf"); Pump();
            Check(Walk(main).OfType<ListBox>().Any(x=>x.Items.OfType<ShelfItem>().Any(i=>i.Text=="Retained embedded clipboard item")),"Switching tools preserves clipboard session data");
            Check(Walk(main).OfType<CheckBox>().Single(x=>Equals(x.Content,"Enable clipboard monitoring for this session (explicit consent)")).IsChecked==false,"Embedding keeps clipboard monitoring opt-in");
            bool shelfClosed=false; shelf.Closed+=(_,_)=>shelfClosed=true; Click("Close tool"); Check(shelfClosed,"Close tool releases the hidden native clipboard window");
            Click("Commands"); Check(app.Windows.Cast<Window>().Count(x=>x.IsVisible && x.Title!="Brainiac Edge activation")==1,"Commands opens inside shell");
            var actions=Walk(main).OfType<ListBox>().Single(x=>x.IsVisible); actions.SelectedItem=actions.Items.OfType<DeskAction>().Single(x=>x.Id=="files"); Click("Run selected action");
            Check(Walk(main).OfType<TextBlock>().Any(x=>x.IsVisible && x.Text=="File Management Toolkit"),"Executing a command navigates to its tool without closing the shell page");
            router.Open("prompt"); Pump(); var editor=Walk(main).OfType<TextBox>().Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Script text"); editor.Text="Unsaved navigation script"; router.Open("convert"); router.Open("prompt"); Pump(); Check(editor.Text=="Unsaved navigation script" && editor.IsVisible,"Unsaved script remains intact across tool navigation");
            Walk(main).OfType<TextBox>().Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Script name").Text="Navigation script"; Click("Save to script library"); Click("Close tool");
            var busy=new BusyTool(); main.ShowToolPage(busy); Pump(); Click("Start pending action"); bool closed=false; busy.Closed+=(_,_)=>closed=true; busy.Close(); Check(!closed && busy.IsBusy,"Running operations prevent tool disposal"); model.NavigateCommand.Execute("Dashboard"); Check(busy.IsBusy,"Navigation does not cancel active work"); busy.Completion.SetResult(); Pump(); busy.Close(); Check(closed,"Completed tool releases resources normally");
            Console.WriteLine($"{count} single-window navigation checks passed.");
        }
        finally { router?.Dispose(); main?.Close(); app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",previous); }
    }
}
