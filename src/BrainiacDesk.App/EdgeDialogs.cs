using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
public sealed class EdgeAddWindow : ToolWindow
{
    public EdgeAddWindow(EdgeCoordinator edge) : base("Add to Brainiac Edge")
    {
        Note("Pins are saved locally across restarts. Files remain in their original locations. Clipboard content is shared only when you explicitly pin an existing Shelf item.");
        var type=new ComboBox { ItemsSource=new[] { "Note","Text","Code","URL" },SelectedIndex=0 }; Body.Children.Add(type); var text=Input("",160);
        Action("Pin text / code / URL",async ()=> { await edge.Content.PinTextAsync(text.Text,(string)type.SelectedItem); Status.Text="Pinned."; });
        Action("Pin files / images / documents",async ()=> { foreach(var file in PickFiles()) await edge.Content.PinFileAsync(file); Status.Text="File references pinned."; });
        Action("Pin folder",async ()=> { var folder=PickFolder(); if(folder is not null) await edge.Content.PinFileAsync(folder); });
        Action("Choose existing Clipboard Shelf item",()=> { edge.Router.Open("shelf"); return Task.CompletedTask; });
        Action("Pin selected Clipboard Shelf item",edge.PinShelfSelectionAsync);
        Action("Pin latest screenshot",edge.PinLatestAsync);
        Action("Restore minimized cards",()=> { foreach(var card in Application.Current.Windows.OfType<EdgeCardWindow>()) { card.WindowState=WindowState.Normal; card.Show(); } return Task.CompletedTask; });
    }
}
public sealed class EdgeItemEditor : ToolWindow
{
    public EdgeItemEditor(EdgeCoordinator edge,EdgeItem item) : base("Edit Edge pin")
    {
        Note("Changes affect the pin only. Original documents and images remain unchanged. Use the Capture editor to annotate an image into a separate export.");
        Note("Title"); var title=Input(item.Title); Note("Tags (comma-separated)"); var tags=Input(item.Tags);
        Note("Content"); var content=Input(item.Content,150); content.IsReadOnly=item.FilePath is not null;
        var favourite=new CheckBox { Content="Favourite",IsChecked=item.Favourite }; var pinned=new CheckBox { Content="Pinned · protected from expiry",IsChecked=item.Pinned }; Body.Children.Add(favourite); Body.Children.Add(pinned);
        Note("Expiry UTC (ISO 8601); blank keeps indefinitely"); var expiry=Input(item.Expires?.ToString("O") ?? "");
        Note("Workspace"); var workspaces=WorkspaceWindow.LoadWorkspaces(); workspaces.Insert(0,new("General","Orange",[]) { Id="" }); var workspace=new ComboBox { ItemsSource=workspaces,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=item.WorkspaceId }; Body.Children.Add(workspace);
        var ocr=Input(item.OcrText,90); ocr.IsReadOnly=true; ocr.ToolTip="Recognized text";
        Action("Copy recognized text",()=> { if(!string.IsNullOrEmpty(item.OcrText)) System.Windows.Clipboard.SetText(item.OcrText); return Task.CompletedTask; });
        Action("Save pin",async ()=>
        {
            DateTimeOffset? expires=string.IsNullOrWhiteSpace(expiry.Text) ? null : DateTimeOffset.Parse(expiry.Text,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal);
            var updated=item with { Title=title.Text.Trim(),Tags=tags.Text,Content=content.Text,Favourite=favourite.IsChecked==true,Pinned=pinned.IsChecked==true,Expires=expires,WorkspaceId=(string?)workspace.SelectedValue ?? item.WorkspaceId };
            if(item.FilePath is null) updated=updated with { Fingerprint=EdgeRules.Fingerprint(item.Kind,updated.Content) };
            await edge.Store.UpdateAsync(updated); await edge.Model.RefreshAsync(); Status.Text="Pin saved.";
        });
    }
}
public sealed class EdgeSettingsWindow : ToolWindow
{
    public EdgeSettingsWindow(EdgeCoordinator edge) : base("Brainiac Edge Settings")
    {
        var settings=edge.Settings;
        CheckBox Check(string label,bool initial) { var c=new CheckBox { Content=label,IsChecked=initial,Margin=new Thickness(0,8,0,8) }; Body.Children.Add(c); return c; }
        ComboBox Combo(string label,string[] options,string initial) { Note(label); var c=new ComboBox { ItemsSource=options,SelectedItem=initial }; Body.Children.Add(c); return c; }
        var enabled=Check("Enable edge activation and global shortcut",settings.Enabled); var capture=Check("Automatically pin completed BrainiacDesk screenshots",settings.AutoCapture);
        var captureOcr=Check("Run offline OCR on completed captures and index the text",settings.CaptureOcr);
        var captureShelf=Check("Send recognized capture text to Clipboard Shelf (its privacy filters still apply)",settings.CaptureOcrToShelf);
        var autoHide=Check("Auto-hide when pointer and keyboard leave",settings.AutoHide); var fullscreen=Check("Respect fullscreen applications and presentations",settings.RespectFullscreen); var reduced=Check("Reduce motion",settings.ReducedMotion); var glass=Check("Translucent glass-style surface",settings.Glass);
        Note("Width / height in device-independent pixels"); var width=Input(settings.Width.ToString()); var height=Input(settings.Height.ToString());
        var align=Combo("Alignment",["Left","Center","Right"],settings.Alignment); var dock=Combo("Dock edge",["Top","Bottom","Left","Right"],settings.Dock);
        var monitors=new[] { "Pointer" }.Concat(System.Windows.Forms.Screen.AllScreens.Select(x=>x.DeviceName)).ToArray(); var monitor=Combo("Monitor (Pointer follows the active edge)",monitors,settings.Monitor);
        Note("Activation strip sensitivity: 1–20 physical pixels"); var sensitivity=Input(settings.Sensitivity.ToString()); Note("Hover dwell: 100–2000 ms"); var dwell=Input(settings.DwellMilliseconds.ToString());
        Note("Thumbnail size: 64–220 (64 compact, 160 expanded)"); var size=Input(settings.CardSize.ToString());
        Note("Shortcut key (for example E or F8)"); var key=Input(settings.ShortcutKey); var ctrl=Check("Ctrl",(settings.ShortcutModifiers&2)!=0); var shift=Check("Shift",(settings.ShortcutModifiers&4)!=0); var alt=Check("Alt",(settings.ShortcutModifiers&1)!=0); var win=Check("Windows key",(settings.ShortcutModifiers&8)!=0);
        Action("Save Edge settings",async ()=> { var changed=settings with { Enabled=enabled.IsChecked==true,AutoCapture=capture.IsChecked==true,CaptureOcr=captureOcr.IsChecked==true,CaptureOcrToShelf=captureShelf.IsChecked==true,AutoHide=autoHide.IsChecked==true,RespectFullscreen=fullscreen.IsChecked==true,ReducedMotion=reduced.IsChecked==true,Glass=glass.IsChecked==true,Width=double.Parse(width.Text),Height=double.Parse(height.Text),Alignment=(string)align.SelectedItem,Dock=(string)dock.SelectedItem,Monitor=(string)monitor.SelectedItem,Sensitivity=int.Parse(sensitivity.Text),DwellMilliseconds=int.Parse(dwell.Text),CardSize=int.Parse(size.Text),ShortcutKey=key.Text.Trim(),ShortcutModifiers=(uint)((ctrl.IsChecked==true ? 2 : 0)|(shift.IsChecked==true ? 4 : 0)|(alt.IsChecked==true ? 1 : 0)|(win.IsChecked==true ? 8 : 0)),WorkspaceId=edge.Model.WorkspaceId }; await edge.SaveSettingsAsync(changed); Status.Text="Edge settings saved."; });
        Action("Clear expired unpinned items",edge.ClearExpiredAsync);
        Action("Backup pin collections",async ()=> { var path=SaveFile("Edge JSON backup|*.json",".json"); if(path is not null) { await edge.Store.ExportAsync(path); Status.Text="Backup saved with managed screenshots. External file references remain references; include originals separately."; } });
        Action("Restore / merge pin collections",async ()=> { var files=PickFiles("Edge JSON backup|*.json"); if(files.Length>0) { await edge.Store.RestoreAsync(files[0]); await edge.Model.RefreshAsync(); Status.Text="Pins merged. Existing pins retained."; } });
        Note("Backups contain pin text and paths in plain JSON. Managed screenshot files live under the BrainiacDesk profile's Edge/Images folder. Removing pins retains originals. Clear orphaned images only after backing up your profile.");
        Action("Remove unused managed images",async ()=> { if(MessageBox.Show(this,"Delete unreferenced managed Edge images? User-owned originals are never deleted.","Clean Edge images",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return; var paths=(await edge.Store.SearchAsync("")).Where(x=>x.FilePath is not null).Select(x=>Path.GetFullPath(x.FilePath!)).ToHashSet(StringComparer.OrdinalIgnoreCase); var folder=Path.GetFullPath(Path.Combine(LocalPaths.Data,"Edge","Images")); int removed=0; if(Directory.Exists(folder) && (File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0) throw new IOException("Managed image cleanup does not follow linked directories."); if(Directory.Exists(folder)) foreach(var file in Directory.GetFiles(folder,"*.png",SearchOption.TopDirectoryOnly)) { var full=Path.GetFullPath(file); if(Path.GetDirectoryName(full)==folder && !paths.Contains(full) && (File.GetAttributes(full)&FileAttributes.ReparsePoint)==0 && File.GetLastWriteTimeUtc(full)<DateTime.UtcNow.AddDays(-1)) { File.Delete(full); removed++; } } Status.Text=$"Removed {removed} unused managed images older than one day."; });
    }
}
