using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using BrainiacDesk.Core;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
namespace BrainiacDesk.App;
public partial class EdgeWindow : Window
{
    private readonly EdgeCoordinator edge;
    private readonly EdgeViewModel model;
    private readonly DispatcherTimer searchDelay=new() { Interval=TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer autoHide=new() { Interval=TimeSpan.FromMilliseconds(750) };
    private readonly Dictionary<string,BitmapSource> thumbnails=[];
    private readonly System.Threading.SemaphoreSlim thumbnailGate=new(2,2);
    private bool allowClose, hiding;
    private int generation, operations;
    private Forms.Screen? screen;
    public static readonly DependencyProperty ThumbnailSizeProperty=DependencyProperty.Register(nameof(ThumbnailSize),typeof(double),typeof(EdgeWindow),new PropertyMetadata(100.0));
    public double ThumbnailSize { get=>(double)GetValue(ThumbnailSizeProperty); set=>SetValue(ThumbnailSizeProperty,value); }
    public EdgeWindow(EdgeCoordinator edge,EdgeViewModel model)
    {
        this.edge=edge; this.model=model; InitializeComponent(); DataContext=model;
        KindBox.ItemsSource=new[] { "All","Recent","Text","Code","Note","URL","Image","Screenshot","PDF","Word","Excel","Folder","File" }; KindBox.SelectedIndex=0;
        KindBox.SelectionChanged+=(_,_)=> { model.Kind=(string)KindBox.SelectedItem; QueueSearch(); };
        WorkspaceBox.SelectionChanged+=(_,_)=> { if(WorkspaceBox.SelectedValue is string id) edge.SelectWorkspace(id); };
        model.PropertyChanged+=ModelChanged;
        searchDelay.Tick+=async (_,_)=> { searchDelay.Stop(); await model.RefreshAsync(); };
        autoHide.Tick+=(_,_)=>
        {
            if(!IsVisible || hiding || operations>0) return;
            if(screen is not null && edge.Settings.RespectFullscreen && EdgeNative.Fullscreen(screen,new WindowInteropHelper(this).Handle) && !IsActive) { Tuck(); return; }
            if(edge.Settings.AutoHide && StayOpen.IsChecked!=true && !IsMouseOver && !(IsActive && IsKeyboardFocusWithin) && Pins.ContextMenu?.IsOpen!=true) Tuck();
        };
        DpiChanged+=(_,_)=> { if(screen is not null) Dispatcher.BeginInvoke(()=> { screen=Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle); EdgeNative.Place(this,screen,edge.Settings); }); };
        PreviewKeyDown+=async (_,e)=> { if(e.Key==Key.Escape) { Tuck(); e.Handled=true; } else if(e.Key==Key.Enter && Pins.IsKeyboardFocusWithin) { await Run(OpenSelectedAsync); e.Handled=true; } else if(e.Key==Key.C && Keyboard.Modifiers==ModifierKeys.Control && Pins.IsKeyboardFocusWithin) { await Run(CopyAsync); e.Handled=true; } else if(e.Key==Key.F && Keyboard.Modifiers==ModifierKeys.Control) { SearchBox.Focus(); e.Handled=true; } else if(e.Key==Key.Delete && Pins.IsKeyboardFocusWithin) { await Run(RemoveSelectedAsync); e.Handled=true; } };
        Pins.MouseDoubleClick+=async (_,_)=>await Run(OpenSelectedAsync);
        var menu=new ContextMenu(); Pins.ContextMenu=menu;
        void Entry(string title,Func<Task> action) { var item=new MenuItem { Header=title }; item.Click+=async (_,_)=>await Run(action); menu.Items.Add(item); }
        Entry("Open / preview",OpenSelectedAsync); Entry("Copy",CopyAsync); Entry("Detach floating card",()=> { edge.Detach(Selected()); return Task.CompletedTask; });
        Entry("Edit title, tags, content and expiry",()=> { new EdgeItemEditor(edge,Selected()).Show(); return Task.CompletedTask; });
        Entry("Favourite / unfavourite",()=>UpdateSelectedAsync(x=>x with { Favourite=!x.Favourite })); Entry("Pin / unpin (expiry protection)",()=>UpdateSelectedAsync(x=>x with { Pinned=!x.Pinned }));
        Entry("Recognize text locally",()=>edge.RecognizeAsync(Selected())); Entry("OCR to Clipboard Shelf",()=>edge.RecognizeAndShelfAsync(Selected(),true));
        Entry("Edit / annotate image",()=>edge.EditImageAsync(Selected())); Entry("Send text to Teleprompter",()=> { var item=Selected(); if(item.FilePath is not null) throw new InvalidOperationException("Select a text pin."); new TeleprompterWindow(item.Content).Show(); return Task.CompletedTask; });
        Entry("Send to Clipboard Shelf",()=>edge.SendToShelfAsync(Selected())); Entry("Export selected images / PDFs as PDF",ExportPdfAsync);
        Entry("Open Converter",()=> { edge.Router.Open("convert"); return Task.CompletedTask; }); Entry("Open File Toolkit",()=> { edge.Router.Open("files"); return Task.CompletedTask; });
        Entry("Relink missing file",RelinkAsync); Entry("Remove selected pins",RemoveSelectedAsync);
        Point? drag=null; Pins.PreviewMouseLeftButtonDown+=(_,e)=>drag=e.GetPosition(Pins);
        Pins.PreviewMouseMove+=async (_,e)=> { if(drag is not Point start || e.LeftButton!=MouseButtonState.Pressed || Pins.SelectedItem is not EdgeItem item || (e.GetPosition(Pins)-start).Length<8) return; drag=null; await Run(()=> { var data=new DataObject(); if(item.FilePath is not null) { if(!Path.Exists(item.FilePath)) throw new FileNotFoundException("Original file missing."); data.SetData(DataFormats.FileDrop,new[] { item.FilePath }); } else data.SetText(item.Content); DragDrop.DoDragDrop(Pins,data,DragDropEffects.Copy); return Task.CompletedTask; }); };
        DragOver+=(_,e)=> { e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Bitmap) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled=true; };
        Drop+=async (_,e)=> { e.Handled=true; await Run(async ()=> { if(e.Data.GetData(DataFormats.FileDrop) is string[] paths) foreach(var path in paths.Take(100)) await edge.Content.PinFileAsync(path); else if(e.Data.GetData(DataFormats.Bitmap) is BitmapSource bitmap) await edge.Content.PinImageAsync(bitmap,"Image"); else if(e.Data.GetData(DataFormats.UnicodeText) is string text) await edge.Content.PinTextAsync(text); }); };
        Closing+=(_,e)=> { if(!allowClose) { e.Cancel=true; Tuck(); } };
        Closed+=(_,_)=> { model.PropertyChanged-=ModelChanged; searchDelay.Stop(); autoHide.Stop(); thumbnails.Clear(); };
    }
    private void ModelChanged(object? sender,PropertyChangedEventArgs e) { if(e.PropertyName is nameof(EdgeViewModel.Query) or nameof(EdgeViewModel.FavouritesOnly)) QueueSearch(); }
    private void QueueSearch() { searchDelay.Stop(); searchDelay.Start(); }
    public void RefreshWorkspaces()
    {
        var all=WorkspaceWindow.LoadWorkspaces(); all.Insert(0,new Workspace("General","Orange",[]) { Id="" }); all.Insert(0,new Workspace("All collections","Orange",[]) { Id="*" }); WorkspaceBox.ItemsSource=all; WorkspaceBox.SelectedValue=model.WorkspaceId;
    }
    public void Reveal(Forms.Screen target,bool keyboard)
    {
        generation++; hiding=false; screen=target; Motion.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,null); Motion.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,null); BeginAnimation(OpacityProperty,null); Opacity=1;
        ThumbnailSize=edge.Settings.CardSize; Frame.Opacity=edge.Settings.Glass ? .92 : 1; EdgeNative.Place(this,target,edge.Settings); RefreshWorkspaces(); Show(); Animate(true,null); autoHide.Start(); _=model.RefreshAsync();
        if(keyboard) { Activate(); SearchBox.Focus(); }
    }
    public void Tuck()
    {
        if(!IsVisible || hiding) return; edge.DisarmActivation(); hiding=true; StayOpen.IsChecked=false; autoHide.Stop(); var current=++generation;
        Animate(false,()=> { if(current!=generation) return; Hide(); hiding=false; });
    }
    private void Animate(bool reveal,Action? completed)
    {
        var duration=TimeSpan.FromMilliseconds(edge.Settings.ReducedMotion || !SystemParameters.ClientAreaAnimation ? 0 : 160);
        var property=edge.Settings.Dock is "Left" or "Right" ? System.Windows.Media.TranslateTransform.XProperty : System.Windows.Media.TranslateTransform.YProperty;
        double offset=edge.Settings.Dock is "Bottom" or "Right" ? 28 : -28;
        var animation=new DoubleAnimation(reveal ? offset : 0,reveal ? 0 : offset,duration) { EasingFunction=new CubicEase { EasingMode=EasingMode.EaseOut } };
        if(completed is not null) animation.Completed+=(_,_)=>completed(); Motion.BeginAnimation(property,animation); BeginAnimation(OpacityProperty,new DoubleAnimation(reveal ? 0 : 1,reveal ? 1 : 0,duration));
    }
    public void Shutdown() { allowClose=true; Close(); }
    public async Task Run(Func<Task> action) { operations++; try { await action(); } catch(Exception ex) { model.Status=ex.Message; } finally { operations--; } }
    private EdgeItem Selected()=>Pins.SelectedItem as EdgeItem ?? throw new InvalidOperationException("Select a pin first.");
    private Task CopyAsync() { EdgeContentService.Copy(Selected()); model.Status="Copied selected content."; return Task.CompletedTask; }
    private async Task UpdateSelectedAsync(Func<EdgeItem,EdgeItem> update) { foreach(var item in Pins.SelectedItems.Cast<EdgeItem>().ToArray()) await edge.Store.UpdateAsync(update(item)); await model.RefreshAsync(); }
    private async Task RemoveSelectedAsync()
    {
        var selected=Pins.SelectedItems.Cast<EdgeItem>().ToArray(); if(selected.Length==0) return;
        if(MessageBox.Show(this,$"Remove {selected.Length} pins? Original files are retained.","Remove pins",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        await edge.Store.RemoveAsync(selected.Select(x=>x.Id)); await model.RefreshAsync();
    }
    private async Task OpenSelectedAsync()
    {
        var item=Selected(); if(item.Kind is "Image" or "Screenshot") { var bitmap=await Task.Run(()=>EdgeContentService.LoadImage(item.FilePath!)); new ImagePreviewWindow(bitmap).Show(); }
        else if(item.FilePath is not null) { if(!Path.Exists(item.FilePath)) throw new FileNotFoundException("Original file missing. Use Relink to choose its new location."); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute=true }); }
        else if(item.Kind=="URL") { EdgeRules.Validate(item); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.Content) { UseShellExecute=true }); }
        else new EdgeItemEditor(edge,item).Show();
    }
    private async Task RelinkAsync()
    {
        var item=Selected(); if(item.FilePath is null || Path.Exists(item.FilePath)) throw new InvalidOperationException("Select a missing file pin.");
        string? path; if(item.Kind=="Folder") { var folder=new OpenFolderDialog(); path=folder.ShowDialog()==true ? folder.FolderName : null; } else { var file=new OpenFileDialog(); path=file.ShowDialog()==true ? file.FileName : null; }
        if(path is null) return; await edge.Store.UpdateAsync(item with { FilePath=path,Fingerprint=EdgeRules.Fingerprint("Path",path.ToUpperInvariant()) }); await model.RefreshAsync();
    }
    private async Task ExportPdfAsync()
    {
        var items=Pins.SelectedItems.Cast<EdgeItem>().ToArray(); if(items.Length==0 || items.Any(x=>x.Kind is not ("Image" or "Screenshot" or "PDF") || !File.Exists(x.FilePath))) throw new InvalidOperationException("Select existing images or PDF files.");
        var dialog=new SaveFileDialog { Filter="PDF|*.pdf",DefaultExt=".pdf" }; if(dialog.ShowDialog()!=true) return; if(File.Exists(dialog.FileName)) throw new IOException("Choose a new output filename.");
        await Task.Run(()=>ConversionEngine.Pdf(items.Select(x=>x.FilePath!).ToArray(),dialog.FileName,"Combined PDF",System.Threading.CancellationToken.None)); OperationJournal.Record("Edge","Pinned images / PDF",dialog.FileName,"Completed"); model.Status="PDF exported.";
    }
    private void AddClick(object sender,RoutedEventArgs args)=>new EdgeAddWindow(edge).Show();
    private void SettingsClick(object sender,RoutedEventArgs args)=>new EdgeSettingsWindow(edge).Show();
    private void HideClick(object sender,RoutedEventArgs args)=>Tuck();
    private async void ThumbnailLoaded(object sender,RoutedEventArgs args)
    {
        if(sender is not Image image || image.DataContext is not EdgeItem item || item.Kind is not ("Image" or "Screenshot") || item.FilePath is null) return;
        try
        {
            var key=item.FilePath; if(!thumbnails.TryGetValue(key,out var bitmap))
            {
                await thumbnailGate.WaitAsync(); try { if(!image.IsLoaded || image.DataContext is not EdgeItem current || current.Id!=item.Id) return; bitmap=await Task.Run(()=>EdgeContentService.LoadImage(key,240)); } finally { thumbnailGate.Release(); }
                if(thumbnails.Count>=64) thumbnails.Clear(); thumbnails[key]=bitmap;
            }
            if(image.IsLoaded && image.DataContext is EdgeItem selected && selected.Id==item.Id) image.Source=bitmap;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException or ArgumentException) { image.ToolTip="Preview unavailable: " + ex.Message; }
    }
    private void ThumbnailDataChanged(object sender,DependencyPropertyChangedEventArgs args) { if(sender is Image image) { image.Source=null; if(image.IsLoaded) ThumbnailLoaded(image,new RoutedEventArgs()); } }
    private void ThumbnailUnloaded(object sender,RoutedEventArgs args) { if(sender is Image image) image.Source=null; }
}
