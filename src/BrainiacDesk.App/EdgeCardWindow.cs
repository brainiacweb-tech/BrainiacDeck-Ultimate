using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
public sealed record EdgeCardPlacement(double Left,double Top,double Width,double Height,double Opacity,bool Topmost);
public sealed class EdgeCardWindow : Window
{
    public EdgeCardWindow(EdgeCoordinator edge,EdgeItem item)
    {
        Title=item.Title+" · Brainiac Edge"; Width=420; Height=360; MinWidth=280; MinHeight=220; ShowInTaskbar=true; ShowActivated=false;
        var root=new DockPanel { Margin=new Thickness(14) }; Content=root;
        var controls=new WrapPanel(); DockPanel.SetDock(controls,Dock.Top); root.Children.Add(controls);
        var status=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0) }; DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
        void Button(string label,Func<Task> action) { var button=new Button { Content=label,Padding=new Thickness(9,5,9,5),FontSize=12 }; button.Click+=async (_,_)=> { try { await action(); } catch(Exception ex) { status.Text=ex.Message; } }; controls.Children.Add(button); }
        var top=new CheckBox { Content="Always on top",Margin=new Thickness(6),IsChecked=false }; top.Click+=(_,_)=>Topmost=top.IsChecked==true; controls.Children.Add(top);
        var opacity=new Slider { Minimum=.35,Maximum=1,Value=1,Width=100,ToolTip="Card opacity" }; opacity.ValueChanged+=(_,_)=>Opacity=opacity.Value; controls.Children.Add(opacity);
        Button("Copy",async ()=> { item=(await edge.Store.SearchAsync("")).FirstOrDefault(x=>x.Id==item.Id) ?? throw new InvalidOperationException("This pin was removed."); EdgeContentService.Copy(item); status.Text="Copied."; });
        Button("Edit",async ()=> { item=(await edge.Store.SearchAsync("")).FirstOrDefault(x=>x.Id==item.Id) ?? throw new InvalidOperationException("This pin was removed."); new EdgeItemEditor(edge,item).Show(); });
        Button("Minimize",()=> { WindowState=WindowState.Minimized; return Task.CompletedTask; });
        Button("Pin / unpin",async ()=> { item=(await edge.Store.SearchAsync("")).FirstOrDefault(x=>x.Id==item.Id) ?? throw new InvalidOperationException("This pin was removed."); item=item with { Pinned=!item.Pinned }; await edge.Store.UpdateAsync(item); await edge.Model.RefreshAsync(); status.Text=item.Pinned ? "Pinned against expiry." : "Unpinned."; });
        Button("Close",()=> { Close(); return Task.CompletedTask; });
        if(item.Kind is "Image" or "Screenshot") { var image=new Image { Stretch=System.Windows.Media.Stretch.Uniform }; root.Children.Add(image); Loaded+=async (_,_)=> { try { image.Source=await Task.Run(()=>EdgeContentService.LoadImage(item.FilePath!,1000)); } catch(Exception ex) { status.Text=ex.Message; } }; }
        else { var text=new TextBox { Text=item.FilePath ?? item.Content,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto }; root.Children.Add(text); }
        var placements=LocalConfiguration.Load("edge-card-positions.json",new Dictionary<string,EdgeCardPlacement>());
        if(placements.TryGetValue(item.Id,out var saved) && double.IsFinite(saved.Left) && double.IsFinite(saved.Top) && double.IsFinite(saved.Width) && double.IsFinite(saved.Height) && double.IsFinite(saved.Opacity))
        {
            Width=Math.Clamp(saved.Width,280,1600); Height=Math.Clamp(saved.Height,220,1200); Opacity=opacity.Value=Math.Clamp(saved.Opacity,.35,1); Topmost=saved.Topmost; top.IsChecked=saved.Topmost;
            var visible=System.Windows.Forms.Screen.AllScreens; foreach(var screen in visible) { var scale=EdgeNative.Scale(screen); var b=screen.WorkingArea; if(saved.Left*scale>=b.Left && saved.Top*scale>=b.Top && (saved.Left+100)*scale<b.Right && (saved.Top+80)*scale<b.Bottom) { WindowStartupLocation=WindowStartupLocation.Manual; Left=saved.Left; Top=saved.Top; break; } }
        }
        Closing+=(_,_)=> { try { var current=LocalConfiguration.Load("edge-card-positions.json",new Dictionary<string,EdgeCardPlacement>()); var bounds=RestoreBounds; if(bounds.IsEmpty || !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top) || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)) return; current[item.Id]=new(bounds.Left,bounds.Top,bounds.Width,bounds.Height,Opacity,Topmost); LocalConfiguration.Save("edge-card-positions.json",current); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { edge.Model.Status="Card position was not saved: "+ex.Message; } };
    }
}
