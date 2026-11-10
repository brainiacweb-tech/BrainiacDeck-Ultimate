using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace BrainiacDesk.App;
public sealed class TeleprompterWindow : Window
{
    private readonly TextBox reading=new() { IsReadOnly=true,IsReadOnlyCaretVisible=false,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Hidden,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,BorderThickness=new Thickness(0),Padding=new Thickness(18,8,18,28),Background=Brushes.Transparent,Foreground=Brushes.White,Focusable=false,Style=null };
    private readonly TextBlock status=new() { FontSize=10,Foreground=Brushes.LightGray,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(18,0,18,5) };
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(16) };
    private readonly DispatcherTimer saveTimer=new() { Interval=TimeSpan.FromMilliseconds(600) };
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private readonly Button play;
    private readonly WrapPanel extras=new() { Visibility=Visibility.Collapsed,HorizontalAlignment=HorizontalAlignment.Center };
    private PromptPreferences preferences;
    private readonly ScriptLibrary library=new();
    private readonly OfflineVoiceFollower voice=new();
    private HwndSource? source;
    private bool playing,following,ready;
    private double last;
    private int speechCursor;
    private readonly System.Collections.Generic.List<int> hotkeys=[];
    private Window? editorWindow,settingsWindow;
    public bool Playing=>playing;
    public string ScriptText=>reading.Text;
    public double ScrollOffset=>reading.VerticalOffset;
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowDisplayAffinity(IntPtr h,uint affinity);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    public TeleprompterWindow(string text="")
    {
        preferences=LocalConfiguration.Load("teleprompter.json",new PromptPreferences());
        FontFamily=(FontFamily)Application.Current.FindResource("AppFont");
        Title="Floating Teleprompter · BrainiacDesk"; Width=Valid(preferences.Width,400,1200,640); Height=Valid(preferences.Height,140,650,220); MinWidth=400; MinHeight=140; MaxWidth=1400; MaxHeight=800;
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false; ShowActivated=false;
        var shell=new Border { CornerRadius=new CornerRadius(28),Background=new SolidColorBrush(Color.FromRgb(10,17,29)),BorderBrush=new SolidColorBrush(Color.FromRgb(53,67,86)),BorderThickness=new Thickness(1) };
        var layout=new Grid(); foreach(var height in new[] { GridLength.Auto,new GridLength(1,GridUnitType.Star),GridLength.Auto,GridLength.Auto }) layout.RowDefinitions.Add(new RowDefinition { Height=height }); shell.Child=layout; Content=shell;
        var header=new DockPanel { Margin=new Thickness(18,5,12,0),Background=Brushes.Transparent,LastChildFill=true };
        var controls=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right }; DockPanel.SetDock(controls,Dock.Right); header.Children.Add(controls);
        AddButton(controls,"Edit script","Icon.Nav.Prompt",OpenEditor); play=AddButton(controls,"Play or pause","Icon.Nav.Play",TogglePlayback); AddButton(controls,"Settings","Icon.Nav.Settings",OpenSettings); AddButton(controls,"Close teleprompter","Icon.Nav.Cancel",Close);
        var grip=new TextBlock { Text="BRAINIACDESK  ·  PROMPTER",FontSize=9,Foreground=Brushes.LightGray,VerticalAlignment=VerticalAlignment.Center,Padding=new Thickness(0,8,0,8),Cursor=Cursors.SizeAll };
        grip.MouseLeftButtonDown+=(_,e)=> { if(e.ClickCount==2) Snap(); else { try { DragMove(); SnapNearTop(); } catch(InvalidOperationException) { } } }; header.Children.Add(grip); layout.Children.Add(header);
        Grid.SetRow(reading,1); layout.Children.Add(reading); Grid.SetRow(status,2); layout.Children.Add(status); Grid.SetRow(extras,3); layout.Children.Add(extras);
        AddButton(extras,"Restart script","Icon.Nav.Prompt",Restart); AddButton(extras,"Snap beneath webcam","Icon.Nav.Arrow",Snap); AddButton(extras,"Slower","Icon.Nav.Settings",()=>SetSpeed(preferences.Speed-5)); AddButton(extras,"Faster","Icon.Nav.Settings",()=>SetSpeed(preferences.Speed+5));
        var resize=new Thumb { Width=18,Height=18,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,Opacity=.5 }; resize.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Thumb\"><Border Background=\"Transparent\"><TextBlock Text=\"⋰\" Foreground=\"#AAB6C5\" FontSize=\"16\"/></Border></ControlTemplate>"); Grid.SetRowSpan(resize,4); layout.Children.Add(resize); resize.DragDelta+=(_,e)=> { Width=Math.Clamp(ActualWidth+e.HorizontalChange,MinWidth,MaxWidth); Height=Math.Clamp(ActualHeight+e.VerticalChange,MinHeight,MaxHeight); };
        MouseEnter+=(_,_)=>extras.Visibility=Visibility.Visible; MouseLeave+=(_,_)=>extras.Visibility=Visibility.Collapsed;
        ApplyPreferences(); var active=library.Scripts.FirstOrDefault(x=>x.Id==library.Active); SetScript(text.Length>0 ? text : active?.Text ?? "");
        timer.Tick+=(_,_)=>Tick(); saveTimer.Tick+=(_,_)=> { saveTimer.Stop(); SavePreferences(); };
        SizeChanged+=(_,_)=>QueueSave(); LocationChanged+=(_,_)=>QueueSave();
        SourceInitialized+=(_,_)=>InitializeNative();
        Loaded+=(_,_)=> { ready=true; RestorePosition(); };
        Closing+=(_,_)=>SavePreferences();
        Closed+=(_,_)=> { ready=false; timer.Stop(); saveTimer.Stop(); voice.Dispose(); foreach(var id in hotkeys) UnregisterHotKey(source!.Handle,id); source?.RemoveHook(Hook); editorWindow?.Close(); settingsWindow?.Close(); clock.Stop(); };
    }
    private static double Valid(double value,double min,double max,double fallback)=>double.IsFinite(value) ? Math.Clamp(value,min,max) : fallback;
    private Button AddButton(Panel parent,string label,string icon,Action action)
    {
        var image=new Image { Width=15,Height=15 }; image.Source=PromptIcon(label,icon);
        var button=new Button { Content=image,ToolTip=label,Width=32,Height=28,Padding=new Thickness(4),Margin=new Thickness(2),Focusable=false,Background=Brushes.Transparent,Foreground=Brushes.White };
        button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Button\"><Border x:Name=\"surface\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" CornerRadius=\"8\" Background=\"{TemplateBinding Background}\"><ContentPresenter HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"/></Border><ControlTemplate.Triggers><Trigger Property=\"IsMouseOver\" Value=\"True\"><Setter TargetName=\"surface\" Property=\"Background\" Value=\"#29384D\"/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        System.Windows.Automation.AutomationProperties.SetName(button,label); button.Click+=(_,_)=> { try { action(); } catch(Exception ex) { status.Text=ex.Message; } }; parent.Children.Add(button); return button;
    }
    private static ImageSource PromptIcon(string label,string fallback)
    {
        string? data=label switch { "Close teleprompter"=>"M5,5 L19,19 M19,5 L5,19", "Play or pause"=>"M7,4 L20,12 7,20 Z", "Pause"=>"M8,4 L8,20 M16,4 L16,20", "Restart script"=>"M5,8 L5,3 M5,8 L10,8 M5,8 C10,-1 23,5 21,14 C20,22 8,25 4,16", "Snap beneath webcam"=>"M3,5 L21,5 M12,21 L12,9 M8,13 L12,9 16,13", "Slower"=>"M4,12 L20,12", "Faster"=>"M4,12 L20,12 M12,4 L12,20", "Edit script"=>"M4,20 L5,14 16,3 21,8 10,19 Z M14,5 L19,10", _=>null };
        if(data is null) { var original=(DrawingImage)Application.Current.FindResource(fallback); var clone=original.CloneCurrentValue(); if(clone.Drawing is DrawingGroup group) foreach(var child in group.Children) if(child is GeometryDrawing { Pen:not null } shape) shape.Pen.Brush=Brushes.White; return clone; }
        var image=new DrawingImage(new GeometryDrawing(label=="Play or pause" ? Brushes.White : null,new Pen(Brushes.White,1.7) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round },Geometry.Parse(data))); image.Freeze(); return image;
    }
    private void InitializeNative()
    {
        source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook);
        SetWindowLongPtr(source.Handle,-20,new IntPtr(GetWindowLongPtr(source.Handle,-20).ToInt64()|0x08000000L|0x80L));
        var conflicts=new System.Collections.Generic.List<string>();
        foreach(var pair in new[] { (8101,Key.P,"Ctrl+Alt+P"),(8102,Key.R,"Ctrl+Alt+R"),(8103,Key.X,"Ctrl+Alt+X") })
        { if(RegisterHotKey(source.Handle,pair.Item1,0x4003,(uint)KeyInterop.VirtualKeyFromKey(pair.Item2))) hotkeys.Add(pair.Item1); else conflicts.Add(pair.Item3); }
        status.Text=conflicts.Count>0 ? "Shortcut unavailable: "+string.Join(", ",conflicts)+". Use the controls." : "Ctrl+Alt+P play/pause  ·  R restart  ·  X dismiss";
        RequestExclusion();
    }
    private IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled)
    {
        if(message==0x21) { handled=true; return new IntPtr(3); } // WM_MOUSEACTIVATE / MA_NOACTIVATE
        if(message==0x312) { if(w.ToInt32()==8101) TogglePlayback(); else if(w.ToInt32()==8102) Restart(); else if(w.ToInt32()==8103) Close(); else return IntPtr.Zero; handled=true; }
        return IntPtr.Zero;
    }
    public void SetScript(string text)
    {
        if(text.Length>ScriptLibrary.MaximumCharacters) throw new InvalidDataException("Script exceeds 250,000 characters.");
        StopVoice(); playing=false; timer.Stop(); reading.Text=text; reading.ScrollToHome(); speechCursor=0; UpdatePlay();
        status.Text=string.IsNullOrWhiteSpace(text) ? "No script yet. Use Edit script to write, paste, or load one." : "Ready · "+text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length+" words";
    }
    public void TogglePlayback()
    {
        if(string.IsNullOrWhiteSpace(reading.Text)) { status.Text="Add a script first."; return; }
        if(following) { StopVoice(); return; }
        playing=!playing; last=clock.Elapsed.TotalSeconds; if(playing) timer.Start(); else timer.Stop(); UpdatePlay();
    }
    public void Restart() { reading.ScrollToHome(); speechCursor=0; last=clock.Elapsed.TotalSeconds; }
    private void UpdatePlay() { ((Image)play.Content).Source=PromptIcon(playing ? "Pause" : "Play or pause","Icon.Nav.Play"); play.ToolTip=playing ? "Pause scrolling" : "Start scrolling"; play.Opacity=playing ? 1 : .75; }
    private void Tick()
    {
        var now=clock.Elapsed.TotalSeconds;
        if(playing) { var maximum=Math.Max(0,reading.ExtentHeight-reading.ViewportHeight); reading.ScrollToVerticalOffset(PromptEngine.Advance(reading.VerticalOffset,now-last,preferences.Speed,maximum)); if(reading.VerticalOffset>=maximum) { playing=false; timer.Stop(); UpdatePlay(); status.Text=maximum>0 ? "End of script · Restart to read again" : "Script fits in the panel · expand or adjust font size"; } }
        last=now;
    }
    private void SetSpeed(double value) { preferences=preferences with { Speed=Math.Clamp(value,5,180) }; status.Text=$"Scroll speed · {preferences.Speed:0} px/s"; QueueSave(); }
    private void ApplyPreferences()
    {
        preferences=preferences with { FontSize=Valid(preferences.FontSize,16,64,26),Speed=Valid(preferences.Speed,5,180,30),Opacity=Valid(preferences.Opacity,.35,1,.96),ReadingWidth=Valid(preferences.ReadingWidth,250,1200,580) };
        Opacity=preferences.Opacity; reading.FontSize=preferences.FontSize; reading.MaxWidth=preferences.ReadingWidth; reading.HorizontalAlignment=HorizontalAlignment.Center; reading.LayoutTransform=preferences.Mirror ? new ScaleTransform(-1,1) : Transform.Identity;
    }
    private void QueueSave() { if(!ready) return; saveTimer.Stop(); saveTimer.Start(); }
    private void SavePreferences()
    {
        if(source is null) return;
        try { if(!GetWindowRect(source.Handle,out var rect)) return; preferences=preferences with { Width=Width,Height=Height,X=rect.Left,Y=rect.Top,HasPosition=true,Monitor=Forms.Screen.FromHandle(source.Handle).DeviceName }; LocalConfiguration.Save("teleprompter.json",preferences); }
        catch(Exception ex) { status.Text="Could not save teleprompter settings: "+ex.Message; }
    }
    private void RestorePosition()
    {
        if(source is null) return;
        var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==preferences.Monitor) ?? Forms.Screen.PrimaryScreen!;
        if(!preferences.HasPosition) { Snap(screen); return; }
        var work=screen.WorkingArea; var dpi=VisualTreeHelper.GetDpi(this); var width=(int)(Width*dpi.DpiScaleX); var height=(int)(Height*dpi.DpiScaleY);
        var x=Valid(preferences.X,work.Left,Math.Max(work.Left,work.Right-width),work.Left); var y=Valid(preferences.Y,work.Top,Math.Max(work.Top,work.Bottom-height),work.Top);
        SetWindowPos(source.Handle,new IntPtr(-1),(int)x,(int)y,0,0,0x11);
    }
    public void Snap()=>Snap(source is null ? Forms.Screen.PrimaryScreen! : Forms.Screen.FromHandle(source.Handle));
    private void Snap(Forms.Screen screen)
    {
        if(source is null) return; var work=screen.WorkingArea; SetWindowPos(source.Handle,new IntPtr(-1),work.Left+work.Width/2,work.Top+12,0,0,0x11);
        Dispatcher.BeginInvoke(()=> { var dpi=VisualTreeHelper.GetDpi(this); var width=Math.Min(work.Width,(int)(Width*dpi.DpiScaleX)); var height=Math.Min(work.Height,(int)(Height*dpi.DpiScaleY)); SetWindowPos(source.Handle,new IntPtr(-1),work.Left+(work.Width-width)/2,work.Top+12,width,height,0x10); QueueSave(); });
    }
    private void SnapNearTop() { if(source is null) return; GetWindowRect(source.Handle,out var rect); var screen=Forms.Screen.FromHandle(source.Handle); if(Math.Abs(rect.Top-screen.WorkingArea.Top)<35) Snap(screen); }
    private void RequestExclusion() { if(source is null) return; if(!SetWindowDisplayAffinity(source.Handle,preferences.ExcludeCapture ? 0x11u : 0u)) status.Text="Windows rejected capture exclusion. It is not guaranteed across recorders."; }
    public void OpenEditor()
    {
        if(editorWindow is not null) { editorWindow.Activate(); return; }
        playing=false; timer.Stop(); StopVoice(); UpdatePlay(); editorWindow=new PromptEditorWindow(this,library,reading.Text); editorWindow.Closed+=(_,_)=>editorWindow=null; editorWindow.Show(); editorWindow.Activate();
    }
    private void StopVoice() { following=false; voice.Dispose(); }
    private void StartVoice(string language)
    {
        if(string.IsNullOrWhiteSpace(reading.Text)) throw new InvalidOperationException("Add a script first.");
        if(MessageBox.Show(settingsWindow ?? this,"Allow BrainiacDesk to use the default microphone for this voice-follow session? Recognition runs locally with the installed Windows speech engine. Audio is not saved. Enable microphone access for desktop apps in Windows Settings if blocked.","Microphone consent",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
        playing=false; timer.Stop(); UpdatePlay(); following=true;
        try { voice.Start(language,(text,confidence)=>Dispatcher.BeginInvoke(()=> { if(!following) return; var next=PromptEngine.MatchSpeech(reading.Text,text,speechCursor); if(next>speechCursor) { speechCursor=next; var line=reading.GetLineIndexFromCharacterIndex(Math.Min(next,reading.Text.Length-1)); if(line>=0) reading.ScrollToLine(Math.Max(0,line-1)); status.Text="Voice follow · listening locally"; } }),error=>Dispatcher.BeginInvoke(()=> { StopVoice(); status.Text="Voice recognition stopped: "+error; })); status.Text="Voice follow · microphone active · Ctrl+Alt+P stops listening"; }
        catch { StopVoice(); throw; }
    }
    private void OpenSettings()
    {
        if(settingsWindow is not null) { settingsWindow.Activate(); return; }
        settingsWindow=new PromptSettingsWindow(this,preferences,(next)=> { preferences=next; ApplyPreferences(); RequestExclusion(); QueueSave(); },Snap,StartVoice,()=> { StopVoice(); status.Text="Voice follow off · microphone released"; });
        settingsWindow.Closed+=(_,_)=>settingsWindow=null; settingsWindow.Show(); settingsWindow.Activate();
    }
    public void ChooseMonitor(string device) { var screen=Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==device) ?? throw new InvalidOperationException("Monitor disconnected. Reopen settings."); Snap(screen); }
}