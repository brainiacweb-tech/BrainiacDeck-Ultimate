using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BrainiacDesk.App;
internal static class TeleprompterTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr w,IntPtr l);
    public static void Run()
    {
        var previous=Environment.GetEnvironmentVariable("BRAINIACDESK_DATA"); var folder=Path.Combine(Path.GetTempPath(),"BrainiacDesk-Overlay-"+Guid.NewGuid().ToString("N")); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",folder);
        var app=new App(); app.InitializeComponent(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown; SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        int count=0; void Check(bool valid,string name) { if(!valid) throw new Exception("FAIL: "+name); count++; Console.WriteLine("PASS: "+name); }
        void Pump(int milliseconds=100) { var clock=System.Diagnostics.Stopwatch.StartNew(); do { app.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle); Thread.Sleep(5); } while(clock.ElapsedMilliseconds<milliseconds); }
        try
        {
            var library=new ScriptLibrary(); Check(library.Scripts.Count==0 && !Directory.Exists(folder),"First use creates no sample script or data file");
            library.Save("Saved user script","A real local script for this isolated test."); var restored=new ScriptLibrary(); Check(restored.Scripts.Count==1 && restored.Scripts[0].Text.Contains("real local"),"Protected script library survives service restart");
            Check(!File.ReadAllText(Path.Combine(folder,"scripts.protected.json")).Contains("real local"),"Script text is not stored as plaintext");
            restored.Select(null); restored.Save("Second script","Second saved text"); restored.Delete(restored.Scripts[0].Id); Check(new ScriptLibrary().Scripts.Count==1,"Script deletion persists");
            var shelf=new[] { new ShelfItem("Text","Persistent text","Persistent text",DateTime.UtcNow,true),new ShelfItem("Files","file.txt",new[] { "C:\\file.txt" },DateTime.UtcNow,false) }; ShelfPersistence.Save(shelf); var saved=ShelfPersistence.Load(); Check(saved.Length==2 && (string)saved[0].Payload=="Persistent text" && saved[1].Payload is string[],"Protected shelf text and file references survive restart");
            OperationJournal.Record("Converter","input.png","output.jpg","Completed"); Check(OperationJournal.Load().Length==1 && OperationJournal.Summary().Contains("1 completed conversions"),"Dashboard conversion count derives from saved operation records"); OperationJournal.Clear(); Check(OperationJournal.Load().Length==0,"Operation history clear persists");
            Check(PromptEngine.Advance(0,.05,40,100)==2 && PromptEngine.Advance(99,.1,40,100)==100,"Time-based scrolling advances and clamps at end");
            var script="one two three four five six seven eight"; Check(PromptEngine.MatchSpeech(script,"three four",0)==18,"Voice alignment follows a matching word sequence"); Check(PromptEngine.MatchSpeech(script,"unrelated audio",0)==0,"Unmatched speech does not fabricate progress"); Check(PromptEngine.MatchSpeech(script,"one two",20)==20,"Voice alignment never moves backwards");
            var other=new Window { Title="Isolated focus test",Width=300,Height=160 }; other.Show(); other.Activate(); Pump(); var foreground=GetForegroundWindow();
            var overlay=new TeleprompterWindow(string.Join('\n',Enumerable.Range(1,80).Select(i=>"Line "+i+" · scrolling test text with enough content to fill the viewport."))); overlay.Show(); Pump(250);
            var hwnd=new WindowInteropHelper(overlay).Handle; var flags=GetWindowLongPtr(hwnd,-20).ToInt64();
            Check(overlay.Owner is null && (flags&8)!=0,"Overlay is independent and native always-on-top");
            Check((flags&0x08000000)!=0 && GetForegroundWindow()==foreground,"Showing overlay does not steal foreground focus");
            other.WindowState=WindowState.Minimized; Pump(); Check(overlay.IsVisible && overlay.WindowState!=WindowState.Minimized,"Overlay remains visible when another app window minimizes");
            overlay.TogglePlayback(); Pump(500); Check(overlay.Playing && overlay.ScrollOffset>0,"Real WPF text viewport scrolls automatically");
            overlay.TogglePlayback(); var offset=overlay.ScrollOffset; Pump(200); Check(Math.Abs(overlay.ScrollOffset-offset)<.1,"Paused scrolling remains still");
            overlay.Restart(); Pump(); Check(overlay.ScrollOffset<.1,"Restart resets real scroll position");
            SendMessage(hwnd,0x312,new IntPtr(8101),IntPtr.Zero); Check(overlay.Playing,"Global-hotkey message toggles playback"); overlay.TogglePlayback();
            overlay.Width=700; overlay.Height=260; overlay.Snap(); Pump(200);
            var content=(FrameworkElement)overlay.Content; var preview=new RenderTargetBitmap((int)content.ActualWidth,(int)content.ActualHeight,96,96,PixelFormats.Pbgra32); preview.Render(content); Directory.CreateDirectory("artifacts/ui-review"); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview)); using(var output=File.Create("artifacts/ui-review/teleprompter-overlay.png")) encoder.Save(output);
            overlay.Close(); Check(LocalConfiguration.Load("teleprompter.json",new PromptPreferences()).Width==700,"Overlay geometry is persisted before native handle closes");
            var reopened=new TeleprompterWindow(); reopened.Show(); Pump(); Check(reopened.Width==700 && reopened.Height==260 && reopened.ScriptText=="Second saved text","Overlay restores dimensions and selected saved script"); reopened.Close(); other.Close();
            var conversionFolder=Path.Combine(folder,"exports"); Directory.CreateDirectory(conversionFolder); var imagePath=Path.Combine(folder,"fixture.png");
            var pixels=Enumerable.Repeat((byte)255,32*16*4).ToArray(); var bitmap=BitmapSource.Create(32,16,96,96,PixelFormats.Bgra32,null,pixels,32*4); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using(var file=File.Create(imagePath)) png.Save(file);
            System.Collections.Generic.IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Walk(VisualTreeHelper.GetChild(root,i))) yield return child; }
            var converter=new ConverterWindow(); converter.Show(); Pump(); converter.AddInputs([imagePath]);
            Walk(converter).OfType<System.Windows.Controls.TextBox>().First(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Output folder").Text=conversionFolder;
            var format=Walk(converter).OfType<System.Windows.Controls.ComboBox>().First(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Output format"); format.SelectedItem="JPG";
            void ConvertQueue() { Walk(converter).OfType<System.Windows.Controls.Button>().First(x=>x.Content as string=="Convert queue").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); var deadline=DateTime.UtcNow.AddSeconds(20); while(converter.IsBusy && DateTime.UtcNow<deadline) Pump(20); Check(!converter.IsBusy,"Converter UI operation returns without hanging"); }
            ConvertQueue(); var jpg=Path.Combine(conversionFolder,"fixture.jpg"); Check(File.Exists(jpg) && OperationJournal.Load().Any(x=>x.Module=="Converter" && x.Output==jpg && x.Outcome=="Completed"),"Converter button writes actual JPEG and persistent result");
            using(var input=File.OpenRead(jpg)) Check(BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0].PixelWidth==32,"Converted UI output decodes with expected dimensions");
            var textPath=Path.Combine(folder,"document.txt"); File.WriteAllText(textPath,"A real PDF page from the UI queue."); converter.AddInputs([textPath]); format.SelectedItem="Combined PDF"; ConvertQueue(); var pdf=Path.Combine(conversionFolder,"BrainiacDesk document.pdf"); using(var document=PdfSharp.Pdf.IO.PdfReader.Open(pdf,PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Check(document.PageCount==2,"Converter UI combines real image and text into two-page PDF"); converter.Close();
            Check(OperationJournal.Load().Count(x=>x.Module=="Converter" && x.Outcome=="Completed")==2,"Conversion history survives window closure");
            void Click(Window window,string label) { Walk(window).OfType<System.Windows.Controls.Button>().First(x=>x.Content as string==label).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Pump(50); }
            var shelfWindow=new ClipboardShelfWindow(); shelfWindow.Show(); Pump(); shelfWindow.AddSnippet("A retained test snippet.");
            var persistence=Walk(shelfWindow).OfType<System.Windows.Controls.CheckBox>().First(x=>x.Content as string=="Save shelf across restarts using Windows account encryption"); persistence.IsChecked=true; persistence.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); shelfWindow.Close();
            shelfWindow=new ClipboardShelfWindow(); shelfWindow.Show(); Pump(); Check(Walk(shelfWindow).OfType<System.Windows.Controls.ListBox>().First().Items.Cast<ShelfItem>().Any(x=>x.Text=="A retained test snippet."),"Shelf window restores user content after close and reopen");
            Check(Walk(shelfWindow).OfType<System.Windows.Controls.CheckBox>().First(x=>(x.Content as string)?.StartsWith("Enable clipboard monitoring")==true).IsChecked==false,"Persistent shelf still requires fresh monitoring consent"); Click(shelfWindow,"Collapse / expand"); Check(shelfWindow.Height==180,"Shelf collapse control changes real native window height"); shelfWindow.Close();
            var workspaceOwner=new Window { Width=300,Height=160 }; workspaceOwner.Show(); var router=new ActionRouter(workspaceOwner); var workspace=new WorkspaceWindow(router); workspace.Show(); Pump();
            Walk(workspace).OfType<System.Windows.Controls.TextBox>().First().Text="User workspace"; var actions=Walk(workspace).OfType<System.Windows.Controls.ListBox>().Last(); actions.SelectedItem=actions.Items.Cast<DeskAction>().First(x=>x.Id=="prompt"); Click(workspace,"Create workspace"); workspace.Close();
            workspace=new WorkspaceWindow(router); workspace.Show(); Pump(); var workspaceList=Walk(workspace).OfType<System.Windows.Controls.ListBox>().First(); Check(workspaceList.Items.Cast<Workspace>().Single().Name=="User workspace","Workspace creation persists through window restart"); workspaceList.SelectedIndex=0; Click(workspace,"Open selected workspace"); var routedOverlay=app.Windows.OfType<TeleprompterWindow>().Single(); Check(routedOverlay.Owner is null && routedOverlay.IsVisible,"Saved workspace opens the independent real teleprompter"); workspaceOwner.WindowState=WindowState.Minimized; Pump(); Check(routedOverlay.IsVisible,"Router-created overlay survives its launcher minimizing"); routedOverlay.Close(); workspace.Close();
            workspaceOwner.WindowState=WindowState.Normal; var command=new CommandWindow(router); command.Show(); Pump(); Walk(command).OfType<System.Windows.Controls.TextBox>().First().Text="checksum"; Click(command,"Run selected action"); Check(app.Windows.OfType<FileToolsWindow>().Any(x=>x.IsVisible),"Command Center dispatches search to the actual File Toolkit"); foreach(var tool in app.Windows.OfType<FileToolsWindow>().ToArray()) tool.Close(); workspaceOwner.Close();
            var workflowInput=Path.Combine(folder,"workflow-input"); var workflowOutput=Path.Combine(folder,"workflow-output"); Directory.CreateDirectory(workflowInput); Directory.CreateDirectory(workflowOutput);
            var workflow=new WorkflowWindow(); workflow.Show(); Pump(); var fields=Walk(workflow).OfType<System.Windows.Controls.TextBox>().Where(x=>!x.IsReadOnly).ToArray(); fields[0].Text="User workflow"; fields[1].Text=workflowInput; fields[2].Text=workflowOutput; fields[3].Text=".txt"; Click(workflow,"Add step ↓"); Click(workflow,"Save workflow"); workflow.Close();
            workflow=new WorkflowWindow(); workflow.Show(); Pump(); Click(workflow,"Load saved workflow"); Check(Walk(workflow).OfType<System.Windows.Controls.TextBox>().Any(x=>x.Text=="User workflow") && Walk(workflow).OfType<System.Windows.Controls.ListBox>().First().Items.Count==1,"Workflow UI saves and restores real configuration and steps"); workflow.Close();
            var languages=OfflineVoiceFollower.Languages(); Console.WriteLine("Installed offline speech languages: "+(languages.Length==0 ? "none; voice control must be disabled" : string.Join(", ",languages)));
            File.WriteAllText(Path.Combine(folder,"scripts.protected.json"),"invalid protected data"); bool failed=false; try { _=new ScriptLibrary(); } catch { failed=true; } Check(failed && File.ReadAllText(Path.Combine(folder,"scripts.protected.json"))=="invalid protected data","Corrupt saved scripts report failure without resetting data");
            Console.WriteLine($"{count} teleprompter and persistence checks passed.");
        }
        finally { app.Shutdown(); Environment.SetEnvironmentVariable("BRAINIACDESK_DATA",previous); Console.WriteLine("Isolated test profile: "+folder); }
    }
}