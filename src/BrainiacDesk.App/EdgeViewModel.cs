using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BrainiacDesk.Core;
using CommunityToolkit.Mvvm.ComponentModel;
namespace BrainiacDesk.App;
public partial class EdgeViewModel(IEdgeStore store) : ObservableObject
{
    public ObservableCollection<EdgeItem> Items { get; } = [];
    [ObservableProperty] private string query = "";
    [ObservableProperty] private string workspaceId = "";
    [ObservableProperty] private bool favouritesOnly;
    [ObservableProperty] private string kind = "All";
    [ObservableProperty] private string status = "Drop files, images or text to pin them here.";
    [ObservableProperty] private bool isBusy;
    public bool IsEmpty => Items.Count == 0;
    private int generation;
    public async Task RefreshAsync()
    {
        var request=++generation; IsBusy=true;
        try
        {
            var query=Query; var workspace=WorkspaceId; var favourites=FavouritesOnly; var filter=Kind;
            var result=await Task.Run(()=>store.SearchAsync(query, workspace=="*" ? null : workspace, favourites, filter is "All" or "Recent" ? null : filter));
            if(request!=generation) return;
            Items.Clear(); foreach(var item in (filter=="Recent" ? result.OrderByDescending(x=>x.Created).Take(50) : result)) Items.Add(item); OnPropertyChanged(nameof(IsEmpty));
            Status=Items.Count==0 ? "No pins yet. Add content or change your search." : $"{Items.Count} pins · stored on this device";
        }
        catch(Exception ex) { Status="Could not load pins: " + ex.Message; }
        finally { if(request==generation) IsBusy=false; }
    }
}
