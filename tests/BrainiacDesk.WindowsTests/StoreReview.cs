using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BrainiacDesk.App;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
internal static class StoreReview
{
    private sealed class Host : Application { protected override void OnStartup(StartupEventArgs e) { } }
    public static void Run()
    {
        var previous=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var profile=Path.Combine(Path.GetTempPath(),"BrainiacDeck-StoreReview-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",profile);
        var output=Path.GetFullPath("artifacts/store"); Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"screenshots"));
        var checks=new List<object>(); var findings=new List<object>(); var screenshots=new List<object>();
        var app=new Host { Resources=new ResourceDictionary { Source=new Uri("pack://application:,,,/BrainiacDesk.App;component/Design/Theme.xaml") },ShutdownMode=ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        void Pump() { for(int i=0;i<12;i++) { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(5); } }
        void Check(bool passed,string name) { checks.Add(new { name,passed }); Console.WriteLine((passed ? "PASS: " : "FAIL: ")+name); if(!passed) throw new Exception(name); }
        void Await(Task task) { while(!task.IsCompleted) Pump(); task.GetAwaiter().GetResult(); }
        IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
        var model=new ShellViewModel(new SettingsStore(Path.Combine(profile,"settings.db")),NullLogger<ShellViewModel>.Instance); MainWindow? main=null; ActionRouter? router=null;
        void Capture(string name,double scale)
        {
            Pump(); var content=(FrameworkElement)main!.Content; content.UpdateLayout();
            var image=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth*scale),(int)Math.Ceiling(content.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32); image.Render(content);
            var path=Path.Combine(output,"screenshots",name+".png"); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image)); using(var stream=File.Create(path)) png.Save(stream);
            screenshots.Add(new { path, width=image.PixelWidth,height=image.PixelHeight,scale,source="Actual application controls in an isolated empty profile; no simulated content" });
        }
        try
        {
            Await(model.InitializeAsync()); model.ConfirmExit=false; model.NavigateCommand.Execute("Dashboard"); main=new MainWindow(model); app.MainWindow=main; main.Show(); Pump(); router=new ActionRouter(main);
            var nativeDpi=VisualTreeHelper.GetDpi(main).DpiScaleX;
            foreach(var dark in new[] { false,true }) { model.IsDark=dark; Await(model.SaveCommand.ExecuteAsync(null)); foreach(var page in new[] { "Dashboard","Help","About" }) { model.NavigateCommand.Execute(page); Capture((dark ? "dark-" : "light-")+page.ToLowerInvariant(),nativeDpi); } }
            foreach(var scale in new[] { 1.25,1.5,2.0 }) { model.NavigateCommand.Execute("Dashboard"); Capture("render-density-"+(int)(scale*100),scale); Check(main.ActualWidth>0 && main.ActualHeight>0,"Dashboard renders at "+(int)(scale*100)+"% pixel density (synthetic, not physical DPI transition)"); }
            model.IsDark=false; Await(model.SaveCommand.ExecuteAsync(null));
            foreach(var id in new[] { "convert","shelf","files","workflow","capture","workspaces","privacy","prompt" })
            {
                router.Open(id); Pump(); Capture("tool-"+id,nativeDpi);
                main.Show(); if(main.WindowState==WindowState.Minimized) main.WindowState=WindowState.Normal; main.UpdateLayout(); Pump();
                var controls=Walk(main).OfType<Control>().Where(x=>x.IsVisible && x.IsEnabled && (x is Button || x is TextBox || x is ComboBox || x is CheckBox || x is Slider)).ToArray();
                foreach(var control in controls) { var peer=UIElementAutomationPeer.CreatePeerForElement(control); if(string.IsNullOrWhiteSpace(peer?.GetName())) findings.Add(new { tool=id,control=control.GetType().Name,issue="No accessible name reported by WPF automation peer" }); }
                Check(Walk(main).OfType<Button>().Where(x=>x.IsVisible && x.IsEnabled).All(x=>!string.IsNullOrWhiteSpace(UIElementAutomationPeer.CreatePeerForElement(x)?.GetName())),id+" visible buttons expose accessible names");
                Check(controls.Length>0,id+" interactive controls are available (shell visible="+main.IsVisible+", enabled="+main.IsEnabled+")");
                var first=controls.First(); first.Focus(); Check(main.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)),id+" supports forward keyboard focus navigation");
            }
            model.NavigateCommand.Execute("Dashboard"); main.Width=800; main.Height=600; Pump(); Capture("small-window",nativeDpi); Check(((ScrollViewer)main.FindName("PageScroll")).IsVisible,"Small window retains scrollable main content");
        }
        finally
        {
            router?.Dispose(); main?.Close(); app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",previous);
            File.WriteAllText(Path.Combine(output,"ui-review.json"),JsonSerializer.Serialize(new { utc=DateTimeOffset.UtcNow,checks,accessibilityFindings=findings,screenshots,physicalDpiTransitions="Not tested",narrator="Not tested",highContrast="Not tested",packagedIdentity="Not tested",note="Rendering and automation-peer checks do not establish full accessibility or Store certification." },new JsonSerializerOptions { WriteIndented=true }));
        }
        Console.WriteLine($"{checks.Count} Store UI checks passed; {findings.Count} accessibility findings require review.");
    }
}
