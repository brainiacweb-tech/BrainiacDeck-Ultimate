using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BrainiacDesk.App;
using BrainiacDesk.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
internal static class RedesignTests
{
    private sealed class Host : Application { protected override void OnStartup(StartupEventArgs e) { } }
    public static void Run()
    {
        var previous=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var profile=Path.Combine(Path.GetTempPath(),"BrainiacDeck-Design-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",profile);
        var app=new Host { Resources=new ResourceDictionary { Source=new Uri("pack://application:,,,/BrainiacDesk.App;component/Design/Theme.xaml") },ShutdownMode=ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        int count=0; void Check(bool valid,string name) { if(!valid) throw new Exception("FAIL: "+name); Console.WriteLine("PASS: "+name); count++; }
        void Pump(int milliseconds=120) { var watch=System.Diagnostics.Stopwatch.StartNew(); do { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(5); } while(watch.ElapsedMilliseconds<milliseconds); }
        void Await(Task task) { var watch=System.Diagnostics.Stopwatch.StartNew(); while(!task.IsCompleted && watch.Elapsed.TotalSeconds<30) Pump(10); if(!task.IsCompleted) throw new TimeoutException(); task.GetAwaiter().GetResult(); }
        IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
        void Capture(Window window,string name)
        {
            Pump(220); window.UpdateLayout(); var content=(FrameworkElement)window.Content;
            var bitmap=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(content); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory("artifacts/ui-review"); using var output=File.Create("artifacts/ui-review/"+name+".png"); encoder.Save(output);
        }
        MainWindow? main=null;
        try
        {
            var model=new ShellViewModel(new SettingsStore(Path.Combine(profile,"settings.db")),NullLogger<ShellViewModel>.Instance); Await(model.InitializeAsync()); model.NavigateCommand.Execute("Dashboard"); model.ConfirmExit=false;
            Check(!model.IsDark,"New profiles default to requested light appearance");
            Check(model.ContinueItems.Count==0 && model.ProjectItems.Count==0 && model.OutputItems.Count==0,"Fresh dashboard has genuine empty collections");
            foreach(var name in new[] { "AppFont","BodyFont" }) { var face=new Typeface((FontFamily)app.FindResource(name),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal); Check(face.TryGetGlyphTypeface(out var glyph) && glyph.FontUri.ToString().Contains("BrainiacDesk.App",StringComparison.OrdinalIgnoreCase),"Bundled "+name+" resolves without system font install"); }
            main=new MainWindow(model); app.MainWindow=main; main.Show(); main.Width=1460; main.Height=930; Console.WriteLine($"Dashboard requested width {main.Width}, actual {main.ActualWidth}"); Capture(main,"redesign-dashboard-light");
            Check(Walk(main).OfType<Button>().Any(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Open teleprompter"),"Dashboard toolkit retains actual Teleprompter launch route");
            main.Width=860; main.Height=680; Capture(main,"redesign-dashboard-compact");
            Check(Walk(main).OfType<StackPanel>().Any(x=>x.Name=="ContextPanel" && x.Visibility==Visibility.Collapsed),"Narrow dashboard collapses contextual column");
            main.Width=1460; main.Height=930; model.IsDark=true; Await(model.SaveCommand.ExecuteAsync(null)); Capture(main,"redesign-dashboard-dark");
            Check(((SolidColorBrush)app.FindResource("Background")).Color==Color.FromRgb(16,27,56),"Dark theme applies deep navy token");
            var heading=Walk(main).OfType<TextBlock>().First(x=>x.Text=="Your creative workspace"); Check(((SolidColorBrush)heading.Foreground).Color==((SolidColorBrush)app.FindResource("Ink")).Color,"Dark dashboard headings use the contrast-safe ink token");
            Check(((SolidColorBrush)main.Background).Color==((SolidColorBrush)app.FindResource("Background")).Color,"Dark theme covers the outer window margins");
            var reopened=new ShellViewModel(new SettingsStore(Path.Combine(profile,"settings.db")),NullLogger<ShellViewModel>.Instance); Await(reopened.InitializeAsync()); Check(reopened.IsDark,"Theme preference survives settings-store restart");
            model.IsDark=false; Await(model.SaveCommand.ExecuteAsync(null));
            foreach(var page in new[] { "Help","About" }) { model.NavigateCommand.Execute(page); Pump(); Capture(main,"refresh-"+page.ToLowerInvariant()); Check(Walk(main).OfType<Border>().Count(x=>x.IsVisible && x.CornerRadius.TopLeft>=20)>=4,page+" renders themed card surfaces"); }
            Check(Walk(main).OfType<Button>().Any(x=>x.IsVisible && Equals(x.Content,"Commands")) && Walk(main).OfType<Button>().Any(x=>x.IsVisible && Equals(x.Content,"Files")),"Commands and Files remain visible in top navigation");
            var globalSearch=(TextBox)main.FindName("GlobalSearch"); Check(globalSearch.TransformToAncestor(main).Transform(new Point(0,globalSearch.ActualHeight)).Y < ((ScrollViewer)main.FindName("PageScroll")).TransformToAncestor(main).Transform(new Point(0,0)).Y,"Search row stays above page content without overlap");
            model.NavigateCommand.Execute("Dashboard");
            Check(((SolidColorBrush)main.Background).Color==((SolidColorBrush)app.FindResource("Background")).Color,"Window background switches back to light without restart");
            var converter=new ConverterWindow(); converter.Show(); converter.Width=820; Capture(converter,"redesign-converter-compact");
            foreach(var input in Walk(converter).OfType<TextBox>().Where(x=>x.IsVisible && !x.AcceptsReturn && x.Text.Length>0)) { input.ApplyTemplate(); var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input); Check(host.ActualHeight>=input.FontSize*1.15,"Single-line input reserves readable text height"); }
            converter.Close();
            var capture=new CaptureWindow(new ActionRouter(main)); capture.Show(); Capture(capture,"redesign-capture-light"); capture.Close();
            var library=new ScriptLibrary(); var script=library.Save("Design integration script","A real script saved by the integration test.\n"+string.Join(' ',Enumerable.Repeat("Present clearly and confidently.",100)));
            var prompt=new TeleprompterWindow(script.Text,script.Id); prompt.Show(); Pump(); prompt.ToggleFullscreen(); Pump(); Check(prompt.IsFullscreen,"Teleprompter enters actual fullscreen mode"); prompt.ToggleFullscreen(); Check(!prompt.IsFullscreen,"Teleprompter restores from fullscreen");
            var settingsButton=Walk(prompt).OfType<Button>().Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Settings"); settingsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); var settings=app.Windows.OfType<PromptSettingsWindow>().Single(); Capture(settings,"redesign-prompter-settings-light"); Check(Walk(settings).OfType<CheckBox>().Any(x=>Equals(x.Content,"Show reading guide")),"Reading guide is a real persisted preference control"); var guide=Walk(settings).OfType<CheckBox>().Single(x=>Equals(x.Content,"Show reading guide")); guide.IsChecked=true; guide.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            var align=Walk(settings).OfType<ComboBox>().Single(x=>x.Items.Contains("Justify")); align.SelectedItem="Center"; Check(Walk(prompt).OfType<TextBox>().Single().TextAlignment==TextAlignment.Center,"Alignment control updates the actual reading surface");
            settings.Close(); Capture(prompt,"redesign-prompter-overlay"); prompt.Close(); var persisted=LocalConfiguration.Load("teleprompter.json",new PromptPreferences()); Check(persisted.ReadingGuide && persisted.Alignment=="Center","Reading guide and alignment survive preference persistence");
            model.RefreshLocalData(); Check(model.ScriptCount==1 && model.ContinueItems.Any(x=>x.Title==script.Name),"Continue working reflects actual protected script library");
            var edge=new EdgeStore(Path.Combine(profile,"settings.db")); var search=BrainiacDesk.App.DashboardData.SearchAsync("Design integration",edge); Await(search); Check(search.Result.Any(x=>x.Payload is PromptScript),"Global search finds actual saved scripts");
            var tools=BrainiacDesk.App.DashboardData.SearchAsync("teleprompter",edge); Await(tools); Check(tools.Result.Any(x=>x.Payload is DeskAction { Id:"prompt" }),"Global search returns actual action catalog routes");
            Check(new ScriptLibrary().Scripts.Single().Text==script.Text,"Redesign and search preserve saved script content");
            Console.WriteLine($"{count} redesign checks passed.");
        }
        finally { main?.Close(); app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",previous); Console.WriteLine("Isolated redesign profile: "+profile); }
    }
}
