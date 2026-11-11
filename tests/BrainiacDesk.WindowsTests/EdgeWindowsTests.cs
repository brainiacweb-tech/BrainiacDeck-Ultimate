using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BrainiacDesk.App;
using BrainiacDesk.Core;
using BrainiacDesk.Infrastructure;
internal static class EdgeWindowsTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out RectNative rect);
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left,Top,Right,Bottom; }
    private sealed class TestApplication : Application { protected override void OnStartup(StartupEventArgs e) { } }
    public static void Run()
    {
        var previous=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var folder=Path.Combine(Path.GetTempPath(),"BrainiacDesk-Edge-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",folder);
        var app=new TestApplication(); app.Resources=new ResourceDictionary { Source=new Uri("pack://application:,,,/BrainiacDesk.App;component/Design/Theme.xaml") }; app.ShutdownMode=ShutdownMode.OnExplicitShutdown; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        int count=0; void Check(bool valid,string name) { if(!valid) throw new Exception("FAIL: "+name); count++; Console.WriteLine("PASS: "+name); }
        void Pump(int milliseconds=100) { var clock=System.Diagnostics.Stopwatch.StartNew(); do { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(5); } while(clock.ElapsedMilliseconds<milliseconds); }
        void Await(Task task) { var deadline=System.Diagnostics.Stopwatch.StartNew(); while(!task.IsCompleted && deadline.Elapsed.TotalSeconds<30) Pump(10); if(!task.IsCompleted) throw new TimeoutException("Edge operation timed out."); task.GetAwaiter().GetResult(); }
        T Result<T>(Task<T> task) { Await(task); return task.Result; }
        IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
        ActionRouter? router=null; Window? owner=null;
        try
        {
            owner=new Window { Title="Edge isolated owner",Width=420,Height=250 }; owner.Show(); Pump();
            var store=new EdgeStore(Path.Combine(folder,"settings.db")); Await(store.SaveSettingsAsync(new EdgeSettings { Enabled=false,ReducedMotion=true,AutoHide=false }));
            router=new ActionRouter(owner); var edge=router.Edge; Await(edge.InitializeAsync());
            Check(!app.Windows.OfType<Window>().Any(w=>w.Title=="Brainiac Edge activation"),"Disabled Edge creates no activation sensors");
            var text=Result(edge.Content.PinTextAsync("Real integration fixture")); Check(Result(store.SearchAsync("integration")).Count==1,"Edge text service persists explicit content");
            var path=Path.Combine(folder,"reference.txt"); File.WriteAllText(path,"Keep original content"); var file=Result(edge.Content.PinFileAsync(path)); Await(store.RemoveAsync([file.Id])); Check(File.ReadAllText(path)=="Keep original content","Removing pin preserves original file bytes");
            var bitmap=BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)255,64*32*4).ToArray(),64*4); bitmap.Freeze();
            var screenshot=Result(edge.Content.PinImageAsync(bitmap)); var duplicate=Result(edge.Content.PinImageAsync(bitmap));
            Check(screenshot.Id==duplicate.Id && Directory.GetFiles(Path.Combine(folder,"Edge","Images")).Length==1,"Duplicate capture pixels produce one pin and one managed file");
            var fileDuplicate=Result(edge.Content.PinFileAsync(screenshot.FilePath!)); Check(fileDuplicate.Id==screenshot.Id,"Dropped image and screenshot share pixel duplicate detection");
            Await(edge.ReceiveCaptureAsync(bitmap)); Check(Result(store.SearchAsync("")).Count==2,"Capture automatic pinning remains off by default");
            Await(edge.SaveSettingsAsync(edge.Settings with { Enabled=true,AutoCapture=true,ShortcutKey="F22",ShortcutModifiers=7 })); Pump();
            Check(app.Windows.OfType<Window>().Count(w=>w.Title=="Brainiac Edge activation")==System.Windows.Forms.Screen.AllScreens.Length,"Edge creates one native activation sensor per monitor");
            Await(edge.ReceiveCaptureAsync(bitmap)); Check(Result(store.SearchAsync("")).Count==2,"Enabled capture handoff deduplicates existing screenshot");
            var sensor=app.Windows.OfType<Window>().First(w=>w.Title=="Brainiac Edge activation");
            sensor.RaiseEvent(new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount) { RoutedEvent=System.Windows.Input.Mouse.MouseEnterEvent }); Pump(550);
            Check(app.Windows.OfType<EdgeWindow>().Any(x=>x.IsVisible),"Activation sensor event completes configured reveal dwell"); edge.Hide(); Pump();
            edge.Show(false); Pump(250); var shelf=app.Windows.OfType<EdgeWindow>().Single(); Check(shelf.IsVisible && shelf.Owner is null && shelf.Topmost,"Edge is an independent native topmost WPF shelf");
            var active=GetForegroundWindow(); edge.Hide(); Pump(); edge.Show(false); Pump(); Check(GetForegroundWindow()==active,"Pointer reveal does not steal foreground focus");
            foreach(var monitor in System.Windows.Forms.Screen.AllScreens)
            {
                edge.Show(false,monitor); Pump(); GetWindowRect(new WindowInteropHelper(shelf).Handle,out var bounds); var work=monitor.WorkingArea;
                Check(bounds.Left>=work.Left-2 && bounds.Top>=work.Top-2 && bounds.Right<=work.Right+2 && bounds.Bottom<=work.Bottom+2,"Edge physical shelf bounds fit monitor "+monitor.DeviceName);
            }
            var data=new DataObject(); data.SetData(DataFormats.FileDrop,new[] { path }); var drop=(DragEventArgs)Activator.CreateInstance(typeof(DragEventArgs),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[] { data,DragDropKeyStates.None,DragDropEffects.Copy,shelf,new Point(20,20) },null)!; drop.RoutedEvent=UIElement.DropEvent; shelf.RaiseEvent(drop); Pump(400);
            Check(Result(store.SearchAsync("")).Any(x=>x.FilePath==path),"WPF file-drop event pins a reference");
            var shelfWindow=router.Shelf; shelfWindow.AddSnippet("Selected shelf content"); var list=Walk(shelfWindow).OfType<ListBox>().First(); list.SelectedIndex=0; Await(edge.PinShelfSelectionAsync());
            Check(Result(store.SearchAsync("Selected shelf content")).Count==1,"Existing Clipboard Shelf selected item pins directly into Edge");
            Check(Walk(shelfWindow).OfType<CheckBox>().First(x=>(string)x.Content=="Enable clipboard monitoring for this session (explicit consent)").IsChecked==false,"Edge handoff does not enable clipboard monitoring"); shelfWindow.Close();
            edge.Detach(text); Pump(); var card=app.Windows.OfType<EdgeCardWindow>().Single(); Check(!card.Topmost && card.ResizeMode==ResizeMode.CanResize,"Detached card defaults to ordinary resizable window"); card.WindowState=WindowState.Minimized; edge.Detach(text); Pump(); Check(card.WindowState==WindowState.Normal,"Detached card restores from minimized state"); card.Close();
            edge.Detach(text); Pump(); var restoredCard=app.Windows.OfType<EdgeCardWindow>().Single(); Check(File.Exists(Path.Combine(folder,"edge-card-positions.json")),"Floating card saves finite geometry before close"); restoredCard.Close();
            var backup=Path.Combine(folder,"edge-backup.json"); Await(store.ExportAsync(backup));
            var restoredStore=new EdgeStore(Path.Combine(folder,"restored-profile","settings.db")); Await(restoredStore.RestoreAsync(backup)); var restoredImage=Result(restoredStore.SearchAsync("")).Single(x=>x.Id==screenshot.Id);
            Check(restoredImage.FilePath!=screenshot.FilePath && File.Exists(restoredImage.FilePath) && EdgeContentService.LoadImage(restoredImage.FilePath!).PixelWidth==64,"Edge backup restores managed screenshots under the destination profile");
            Await(restoredStore.RestoreAsync(backup)); Check(Directory.GetFiles(Path.Combine(folder,"restored-profile","Edge","Images")).Length==1,"Repeating backup restore does not duplicate managed images");
            var legacy="[{\"Name\":\"Coding\",\"Color\":\"Orange\",\"Actions\":[\"edge\"]}]"; File.WriteAllText(Path.Combine(folder,"workspaces.json"),legacy); var workspace=WorkspaceWindow.LoadWorkspaces()[0]; Check(WorkspaceWindow.LoadWorkspaces()[0].Id==workspace.Id,"Legacy workspace gains stable persisted ID"); edge.SelectWorkspace(workspace.Id); Await(edge.Content.PinTextAsync("Workspace isolated pin")); Check(Result(store.SearchAsync("",workspace.Id)).Count==1,"Edge uses migrated existing workspace IDs");
            int hotkeys=0; var hwnd=new WindowInteropHelper(owner).Handle; using(var first=new ShortcutService(hwnd,()=>hotkeys++,991)) using(var second=new ShortcutService(hwnd,()=>{},992))
            {
                Check(first.Set(7,0x87) && second.Set(7,0x86),"Independent global shortcut IDs register separate chords"); Check(!first.Set(7,0x86) && first.Registered,"Shortcut conflict preserves previous registration"); SendMessage(hwnd,0x312,new IntPtr(991),IntPtr.Zero); Check(hotkeys==1,"Native WM_HOTKEY dispatch reaches registered action");
            }
            var visual=new DrawingVisual(); using(var context=visual.RenderOpen()) { context.DrawRectangle(Brushes.White,null,new Rect(0,0,800,180)); context.DrawText(new FormattedText("BRAINIAC EDGE 2026",System.Globalization.CultureInfo.GetCultureInfo("en-US"),FlowDirection.LeftToRight,new Typeface("Arial"),48,Brushes.Black,1),new Point(20,30)); }
            var rendered=new RenderTargetBitmap(800,180,96,96,PixelFormats.Pbgra32); rendered.Render(visual); var ocrPath=Path.Combine(folder,"ocr.png"); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(rendered)); using(var stream=File.Create(ocrPath)) png.Save(stream);
            if(Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages() is null) Console.WriteLine("SKIP: OCR recognition, no Windows OCR language installed.");
            else { var recognized=Result(LocalOcr.RecognizeAsync(ocrPath)); Check(recognized.Contains("BRAINIAC",StringComparison.OrdinalIgnoreCase),"Real Windows offline OCR recognizes rendered text"); }
            edge.Show(false); Pump(200); Directory.CreateDirectory("artifacts/ui-review"); var shot=new RenderTargetBitmap((int)shelf.ActualWidth,(int)shelf.ActualHeight,96,96,PixelFormats.Pbgra32); shot.Render(shelf); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(shot)); using(var output=File.Create("artifacts/ui-review/brainiac-edge.png")) encoder.Save(output);
            router.Dispose(); router=null; Pump(); Check(!app.Windows.OfType<EdgeWindow>().Any() && !app.Windows.OfType<Window>().Any(w=>w.Title=="Brainiac Edge activation"),"Edge shutdown closes shelf, sensors, cards and native subscriptions");
            Console.WriteLine($"{count} Edge Windows checks passed.");
        }
        finally { router?.Dispose(); owner?.Close(); app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",previous); Console.WriteLine("Isolated Edge profile: "+folder); }
    }
}
