using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BrainiacDesk.App;
public sealed class PromptEditorWindow : ToolWindow
{
    public PromptEditorWindow(TeleprompterWindow overlay,ScriptLibrary library,string text) : base("Teleprompter Scripts")
    {
        Width=840; Note("Write or paste a real script. Save to the protected local library to restore it next time, or import/export a text file. No scripts are created automatically.");
        var search=Input(); search.ToolTip="Search saved scripts";
        var saved=new ListBox { Height=110,DisplayMemberPath="Name" }; Body.Children.Add(saved);
        var name=Input(); name.ToolTip="Script name"; System.Windows.Automation.AutomationProperties.SetName(name,"Script name");
        var editor=Input(text,240); editor.ToolTip="Script text"; System.Windows.Automation.AutomationProperties.SetName(editor,"Script text"); editor.AcceptsTab=true; editor.MaxLength=ScriptLibrary.MaximumCharacters;
        bool dirty=false,loading=false; Guid? editingId=library.Active;
        void Refresh() { saved.ItemsSource=library.Scripts.Where(x=>x.Name.Contains(search.Text,StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.Updated).ToArray(); }
        bool Discard()=>!dirty || MessageBox.Show(this,"Discard unsaved script edits?","Unsaved script",MessageBoxButton.YesNo)==MessageBoxResult.Yes;
        name.Text=library.Scripts.FirstOrDefault(x=>x.Id==editingId)?.Name ?? "";
        editor.TextChanged+=(_,_)=> { if(!loading) dirty=true; }; name.TextChanged+=(_,_)=> { if(!loading) dirty=true; };
        search.TextChanged+=(_,_)=>Refresh();
        Action("Load selected script",()=> { if(saved.SelectedItem is not PromptScript script) throw new InvalidOperationException("Select a saved script first."); if(!Discard()) return Task.CompletedTask; loading=true; editingId=script.Id; name.Text=script.Name; editor.Text=script.Text; loading=false; dirty=false; return Task.CompletedTask; });
        Action("New script",()=> { if(Discard()) { loading=true; editingId=null; editor.Clear(); name.Clear(); loading=false; dirty=false; } return Task.CompletedTask; });
        Action("Import TXT / Markdown",async()=> { if(!Discard()) return; var paths=PickFiles("Scripts|*.txt;*.md"); if(paths.Length==0) return; if(new FileInfo(paths[0]).Length>2_000_000) throw new InvalidDataException("Script exceeds 2 MB."); var imported=await File.ReadAllTextAsync(paths[0]); if(imported.Length>ScriptLibrary.MaximumCharacters) throw new InvalidDataException("Script exceeds 250,000 characters."); editingId=null; name.Text=Path.GetFileNameWithoutExtension(paths[0]); editor.Text=imported; dirty=true; });
        Action("Save to script library",()=> { library.Select(editingId); var script=library.Save(name.Text,editor.Text); editingId=script.Id; dirty=false; Refresh(); Status.Text="Script saved using Windows account data protection."; return Task.CompletedTask; });
        Action("Export script TXT",async()=> { var path=SaveFile("Text|*.txt",".txt"); if(path is not null) { await File.WriteAllTextAsync(path,editor.Text); Status.Text="Text file exported. Save to library separately to restore it next launch."; } });
        Action("Use script in overlay",()=> { overlay.SetScript(editor.Text); Status.Text="Script applied. Save to library to preserve changes after restart."; return Task.CompletedTask; });
        Action("Delete selected script",()=> { if(saved.SelectedItem is not PromptScript script) throw new InvalidOperationException("Select a saved script first."); if(MessageBox.Show(this,"Delete this script from the local library? Exported files are retained.","Delete script",MessageBoxButton.YesNo)==MessageBoxResult.Yes) { library.Delete(script.Id); if(editingId==script.Id) editingId=null; Refresh(); } return Task.CompletedTask; });
        Closing+=(_,e)=> { if(!e.Cancel && !Discard()) e.Cancel=true; }; Refresh();
    }
}
public sealed class PromptSettingsWindow : ToolWindow
{
    public PromptSettingsWindow(TeleprompterWindow overlay,PromptPreferences initial,Action<PromptPreferences> apply,Action snap,Action<string> startVoice,Action stopVoice) : base("Teleprompter Settings")
    {
        Width=760; var preferences=initial;
        Note("The overlay stays above normal applications without activating on ordinary clicks. Drag its label, double-click to snap, and resize from its lower-right corner. Ctrl+Alt+P pauses, Ctrl+Alt+R restarts, Ctrl+Alt+X dismisses when those shortcuts are available.");
        void Option(string label,double min,double max,double value,Action<double> update)
        {
            var caption=new TextBlock { Text=$"{label} · {value:0.##}",Margin=new Thickness(0,8,0,0) }; Body.Children.Add(caption); var slider=new Slider { Minimum=min,Maximum=max,Value=value,Margin=new Thickness(0,6,0,12) }; Body.Children.Add(slider); slider.ValueChanged+=(_,_)=> { caption.Text=$"{label} · {slider.Value:0.##}"; update(slider.Value); apply(preferences); };
        }
        Option("Font size",16,64,initial.FontSize,value=>preferences=preferences with { FontSize=value });
        Option("Opacity",.35,1,initial.Opacity,value=>preferences=preferences with { Opacity=value });
        Option("Reading width",250,1200,initial.ReadingWidth,value=>preferences=preferences with { ReadingWidth=value });
        Option("Scroll speed · pixels/second",5,180,initial.Speed,value=>preferences=preferences with { Speed=value });
        var mirror=new CheckBox { Content="Mirror script text",IsChecked=initial.Mirror }; Body.Children.Add(mirror); mirror.Click+=(_,_)=> { preferences=preferences with { Mirror=mirror.IsChecked==true }; apply(preferences); };
        var capture=new CheckBox { Content="Request Windows capture exclusion",IsChecked=initial.ExcludeCapture }; Body.Children.Add(capture); capture.Click+=(_,_)=> { preferences=preferences with { ExcludeCapture=capture.IsChecked==true }; apply(preferences); };
        Note("Capture exclusion is a Windows request. It cannot be guaranteed in Teams, Zoom, Meet, screen sharing, or recording applications. Test it before sharing sensitive scripts.");
        var monitors=new ComboBox { ItemsSource=System.Windows.Forms.Screen.AllScreens.Select(x=>x.DeviceName).ToArray(),SelectedItem=initial.Monitor }; Body.Children.Add(monitors);
        Action("Move to selected monitor",()=> { if(monitors.SelectedItem is not string device) throw new InvalidOperationException("Choose a monitor."); overlay.ChooseMonitor(device); return Task.CompletedTask; });
        Action("Snap to top centre",()=> { snap(); return Task.CompletedTask; });
        Note("Optional voice follow uses an installed offline Windows speech recognizer and the default microphone. It advances when at least two recognized words match nearby script text. Accent, language and meeting audio can affect recognition; headphones help. Voice recognition never starts automatically.");
        string[] languages; try { languages=OfflineVoiceFollower.Languages(); } catch(Exception ex) { languages=[]; Status.Text="Offline speech unavailable: "+ex.Message; }
        var language=new ComboBox { ItemsSource=languages,SelectedIndex=languages.Length>0 ? 0 : -1 }; Body.Children.Add(language);
        var start=Action("Enable offline voice follow",()=> { startVoice((string)language.SelectedItem); return Task.CompletedTask; }); start.IsEnabled=languages.Length>0;
        if(languages.Length==0) Note("Voice follow unavailable: no compatible offline speech recognizer is installed. Install a Windows speech language, enable desktop microphone access, and reopen settings. Automatic scrolling remains available.");
        Action("Stop voice follow",()=> { stopVoice(); return Task.CompletedTask; });
        Action("Open Windows microphone settings",()=> { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:privacy-microphone") { UseShellExecute=true }); return Task.CompletedTask; });
    }
}