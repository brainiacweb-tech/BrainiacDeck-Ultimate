using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using BrainiacDesk.Core;
using BrainiacDesk.Infrastructure;
using Forms=System.Windows.Forms;
namespace BrainiacDesk.App;
/// <summary>One application-owned Edge lifetime; all UI/native callbacks use the WPF dispatcher.</summary>
public sealed class EdgeCoordinator : IDisposable
{
    public IEdgeStore Store { get; }
    public EdgeContentService Content { get; }
    public EdgeViewModel Model { get; }
    public EdgeSettings Settings { get; private set; }=new();
    public ActionRouter Router { get; }
    private readonly Window owner;
    private EdgeActivation? activation;
    private ShortcutService? shortcut;
    private EdgeWindow? shelf;
    private bool disposed;
    private DateTimeOffset lastCleanup;
    private readonly Dictionary<string,EdgeCardWindow> cards=[];
    private System.Windows.Media.Imaging.BitmapSource? latestCapture;
    public EdgeCoordinator(Window owner,ActionRouter router,IEdgeStore? store=null)
    {
        this.owner=owner; Router=router; Store=store ?? new EdgeStore(Path.Combine(LocalPaths.Data,"settings.db")); Content=new EdgeContentService(Store); Model=new EdgeViewModel(Store);
        Content.Changed+=ContentChanged; Content.Error+=ContentError;
    }
    private void ContentChanged() { if(!disposed) owner.Dispatcher.BeginInvoke(async ()=>await Model.RefreshAsync()); }
    private void ContentError(string message)=>Model.Status=message;
    public async Task InitializeAsync()
    {
        Settings=await Store.LoadSettingsAsync(); await Store.ClearExpiredAsync(DateTimeOffset.UtcNow); lastCleanup=DateTimeOffset.UtcNow; if(disposed) return; Model.WorkspaceId=Settings.WorkspaceId; Content.WorkspaceId=Settings.WorkspaceId=="*" ? "" : Settings.WorkspaceId;
        shortcut=new ShortcutService(new WindowInteropHelper(owner).Handle,()=> { if(shelf?.IsVisible==true) shelf.Tuck(); else Show(true); },702);
        if(Settings.Enabled && !shortcut.Set(Settings.ShortcutModifiers,VirtualKey(Settings.ShortcutKey))) Model.Status="Edge shortcut is occupied. Choose another in Edge settings.";
        activation=new EdgeActivation(screen=>Show(false,screen),()=>CaptureVisibilityScope.IsActive || shelf?.IsVisible==true || Application.Current.Windows.OfType<RecordingController>().Any() || Application.Current.Windows.OfType<RegionSelectionWindow>().Any()); activation.DisplaysChanged+=()=> { if(!disposed && shelf?.IsVisible==true) Show(false); }; activation.Configure(Settings);
    }
    private static uint VirtualKey(string key) { var parsed=Enum.Parse<System.Windows.Input.Key>(key); var value=System.Windows.Input.KeyInterop.VirtualKeyFromKey(parsed); if(value==0 || parsed is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) throw new ArgumentException("Choose a non-modifier shortcut key."); return (uint)value; }
    public async Task SaveSettingsAsync(EdgeSettings settings)
    {
        settings.Validate(); var key=VirtualKey(settings.ShortcutKey);
        if(shortcut is not null && !shortcut.Set(settings.ShortcutModifiers,settings.Enabled ? key : 0)) throw new InvalidOperationException("Shortcut is in use. The previous shortcut remains registered.");
        try { await Store.SaveSettingsAsync(settings); } catch { shortcut?.Set(Settings.ShortcutModifiers,Settings.Enabled ? VirtualKey(Settings.ShortcutKey) : 0); throw; }
        Settings=settings; activation?.Configure(settings);
        if(shelf?.IsVisible==true) Show(false);
    }
    public void Show(bool keyboard=true,Forms.Screen? screen=null)
    {
        if(disposed) return;
        shelf ??=new EdgeWindow(this,Model);
        screen ??=Settings.Monitor=="Pointer" ? Forms.Screen.FromPoint(Forms.Cursor.Position) : Forms.Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==Settings.Monitor) ?? Forms.Screen.PrimaryScreen!;
        shelf.Reveal(screen,keyboard); if(DateTimeOffset.UtcNow-lastCleanup>TimeSpan.FromMinutes(1)) _=MaintainAsync(); ActivityLog.Record("Opened Brainiac Edge");
    }
    private async Task MaintainAsync()
    {
        lastCleanup=DateTimeOffset.UtcNow;
        try { await Store.ClearExpiredAsync(DateTimeOffset.UtcNow); if(!disposed) await Model.RefreshAsync(); } catch(Exception ex) { if(!disposed) Model.Status="Expiry cleanup failed: "+ex.Message; }
    }
    public void DisarmActivation()=>activation?.DisarmUnderPointer();
    public void Hide()=>shelf?.Tuck();
    public void Search() { SelectWorkspace("*"); Show(); }
    public void SelectWorkspace(string id)
    {
        if(Model.WorkspaceId==id) return; Model.WorkspaceId=id; Content.WorkspaceId=id=="*" ? "" : id; Settings=Settings with { WorkspaceId=id }; _=SaveWorkspaceAsync(); _=Model.RefreshAsync();
    }
    private async Task SaveWorkspaceAsync() { try { await Store.SaveSettingsAsync(Settings); } catch(Exception ex) { Model.Status="Workspace selection not saved: "+ex.Message; } }
    public async Task ReceiveCaptureAsync(System.Windows.Media.Imaging.BitmapSource bitmap)
    {
        latestCapture=bitmap;
        if(Settings.AutoCapture || Settings.CaptureOcr || Settings.CaptureOcrToShelf)
        {
            var item=await Content.PinImageAsync(bitmap);
            if(Settings.CaptureOcr || Settings.CaptureOcrToShelf) await RecognizeAndShelfAsync(item,Settings.CaptureOcrToShelf);
        }
    }
    public async Task PinLatestAsync() { if(latestCapture is null) throw new InvalidOperationException("Capture a screenshot in this session first."); await Content.PinImageAsync(latestCapture); Show(); }
    public async Task PinShelfSelectionAsync()
    {
        var item=Router.Shelf.SelectedItem ?? throw new InvalidOperationException("Select an existing Clipboard Shelf item, then pin it to Edge."); await Content.PinShelfAsync(item); Show();
    }
    public async Task EditImageAsync(EdgeItem item) { if(item.Kind is not ("Image" or "Screenshot") || item.FilePath is null) throw new InvalidOperationException("Select an image pin."); Router.Open("capture"); await Router.Capture.OpenPinnedImageAsync(item.FilePath); }
    public async Task SendToShelfAsync(EdgeItem item)
    {
        if(item.Kind is "Image" or "Screenshot") Router.Shelf.AddImage(await Task.Run(()=>EdgeContentService.LoadImage(item.FilePath!)));
        else if(item.FilePath is not null) Router.Shelf.AddFiles([item.FilePath]); else Router.Shelf.AddSnippet(item.Content);
    }
    public async Task RecognizeAndShelfAsync(EdgeItem item,bool sendToShelf)
    {
        await RecognizeAsync(item);
        if(sendToShelf)
        {
            var updated=(await Store.SearchAsync("",item.WorkspaceId)).First(x=>x.Id==item.Id);
            if(!string.IsNullOrWhiteSpace(updated.OcrText)) Router.Shelf.AddSnippet(updated.OcrText);
        }
    }
    public async Task RunImageWorkflowAsync(string path,bool recognize,bool toShelf,string? pdf)
    {
        var item=await Content.PinFileAsync(path);
        if(item.Kind is not ("Image" or "Screenshot")) throw new InvalidOperationException("Choose an image for this workflow.");
        if(recognize || toShelf) await RecognizeAndShelfAsync(item,toShelf);
        if(pdf is not null)
        {
            if(File.Exists(pdf)) throw new IOException("Choose a new PDF filename.");
            await Task.Run(()=>ConversionEngine.Pdf([path],pdf,"Combined PDF",System.Threading.CancellationToken.None));
            OperationJournal.Record("Edge workflow","Pinned image",pdf,"Completed");
        }
        ActivityLog.Record("Edge image workflow completed");
    }
    public async Task RecognizeAsync(EdgeItem item)
    {
        if(item.Kind is not ("Image" or "Screenshot") || item.FilePath is null) throw new InvalidOperationException("Select an image pin.");
        Model.Status="Recognizing text locally…"; var text=await LocalOcr.RecognizeAsync(item.FilePath); await Store.UpdateAsync(item with { OcrText=text }); await Model.RefreshAsync(); Model.Status=string.IsNullOrWhiteSpace(text) ? "No text recognized. Check the installed Windows OCR languages." : "Text recognized and indexed locally.";
    }
    public void Detach(EdgeItem item)
    {
        if(cards.TryGetValue(item.Id,out var existing)) { existing.WindowState=WindowState.Normal; existing.Show(); existing.Activate(); return; }
        var card=new EdgeCardWindow(this,item); cards[item.Id]=card; card.Closed+=(_,_)=>cards.Remove(item.Id); card.Show();
    }
    public async Task ClearExpiredAsync() { var count=await Store.ClearExpiredAsync(DateTimeOffset.UtcNow); await Model.RefreshAsync(); Model.Status=$"Removed {count} expired unpinned entries. Original files retained."; }
    public void Dispose()
    {
        if(disposed) return; disposed=true; Content.Changed-=ContentChanged; Content.Error-=ContentError; activation?.Dispose(); shortcut?.Dispose(); foreach(var card in cards.Values.ToArray()) card.Close(); shelf?.Shutdown(); latestCapture=null;
    }
}
