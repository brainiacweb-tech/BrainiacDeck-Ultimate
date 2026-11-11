using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BrainiacDesk.App;
public sealed record Workspace(string Name, string Color, string[] Actions) { public DateTimeOffset? Updated { get; init; } public string Id { get; init; } = Guid.NewGuid().ToString(); public string Display => Name + " · " + Color; public override string ToString() => Name; }
public sealed class WorkspaceWindow : ToolWindow
{
    public static List<Workspace> LoadWorkspaces()
    {
        var workspaces=LocalConfiguration.Load("workspaces.json",new List<Workspace>());
        var path=Path.Combine(BrainiacDesk.Core.LocalPaths.Data,"workspaces.json");
        if(File.Exists(path))
        {
            using var document=JsonDocument.Parse(File.ReadAllText(path)); var rows=document.RootElement.EnumerateArray().ToArray(); bool changed=false; var seen=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<workspaces.Count;i++)
            {
                var hasId=rows[i].TryGetProperty("Id",out var id) && id.ValueKind==JsonValueKind.String && Guid.TryParse(id.GetString(),out _);
                if(!hasId || !seen.Add(workspaces[i].Id)) { workspaces[i]=workspaces[i] with { Id=Guid.NewGuid().ToString() }; seen.Add(workspaces[i].Id); changed=true; }
            }
            if(changed) LocalConfiguration.Save("workspaces.json",workspaces);
        }
        return workspaces;
    }
    public WorkspaceWindow(ActionRouter router) : base("Workspaces")
    {
        Note("Save named collections of BrainiacDesk tools. Importing a configuration never launches tools. Third-party application state is not restored.");
        var workspaces = LoadWorkspaces();
        var list = new ListBox { ItemsSource = workspaces, DisplayMemberPath = "Display", Height = 130 }; Body.Children.Add(list);
        Note("Project workspace name"); var name = Input(); name.ToolTip="Workspace name"; System.Windows.Automation.AutomationProperties.SetName(name,"Workspace name"); Note("Collection colour"); var color = new ComboBox { ItemsSource = new[] { "Orange", "Blue", "Green", "Purple" }, SelectedIndex = 0 }; Body.Children.Add(color); Accessible(color,"Workspace collection colour");
        Note("Choose the tools to open with this workspace");
        var actions = new ListBox { ItemsSource = ActionRouter.Catalog.Where(x => x.Id != "workspaces" && !x.Id.StartsWith("edge-",StringComparison.Ordinal)).ToArray(), DisplayMemberPath = "Title", SelectionMode = SelectionMode.Multiple, Height = 160 }; Body.Children.Add(actions);
        Workspace Selected() => list.SelectedItem as Workspace ?? throw new InvalidOperationException("Select a saved workspace first.");
        void Save() { LocalConfiguration.Save("workspaces.json", workspaces); list.Items.Refresh(); }
        Workspace Definition() { var title = name.Text.Trim(); if (title.Length is 0 or > 80) throw new InvalidOperationException("Workspace name must be 1–80 characters."); if(actions.SelectedItems.Count == 0) throw new InvalidOperationException("Choose at least one tool."); return new(title, (string)color.SelectedItem, actions.SelectedItems.Cast<DeskAction>().Select(x => x.Id).ToArray()) { Updated=DateTimeOffset.UtcNow }; }
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is Workspace workspace) { name.Text = workspace.Name; color.SelectedItem = workspace.Color; actions.SelectedItems.Clear(); foreach (var action in actions.Items.Cast<DeskAction>().Where(x => workspace.Actions.Contains(x.Id))) actions.SelectedItems.Add(action); } };
        Action("Create workspace", () => { workspaces.Add(Definition()); Save(); return Task.CompletedTask; });
        Action("Update selected workspace", () => { if (Selected() is Workspace selected) { workspaces[workspaces.IndexOf(selected)] = Definition() with { Id=selected.Id }; Save(); } return Task.CompletedTask; });
        Action("Duplicate selected", () => { if (Selected() is Workspace selected) { workspaces.Add(selected with { Name = selected.Name + " copy",Id=Guid.NewGuid().ToString(),Updated=DateTimeOffset.UtcNow }); Save(); } return Task.CompletedTask; });
        Action("Open selected workspace", () => { if (Selected() is Workspace selected) { router.Edge.SelectWorkspace(selected.Id); foreach (var id in selected.Actions) router.Open(id); } return Task.CompletedTask; });
        Action("Delete selected workspace", () => { if (Selected() is Workspace selected && MessageBox.Show(DialogOwner, "Delete workspace configuration?", "Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { workspaces.Remove(selected); Save(); } return Task.CompletedTask; });
        Action("Export selected", async () => { if (Selected() is Workspace selected) { var path = SaveFile("JSON|*.json", ".json"); if (path is not null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(selected)); } });
        Action("Import configuration", async () => { var paths = PickFiles("JSON|*.json"); if (paths.Length == 0) return; if (new FileInfo(paths[0]).Length > 65536) throw new InvalidDataException("Configuration exceeds 64 KB."); var workspace = JsonSerializer.Deserialize<Workspace>(await File.ReadAllTextAsync(paths[0])) ?? throw new InvalidDataException("Invalid workspace."); if (string.IsNullOrWhiteSpace(workspace.Name) || workspace.Name.Length > 80 || workspace.Actions is null || (workspace.Actions.Length == 0 || workspace.Actions.Length > ActionRouter.Catalog.Length) || workspace.Actions.Any(id => id == "workspaces" || !ActionRouter.Catalog.Any(x => x.Id == id)) || !new[] { "Orange", "Blue", "Green", "Purple" }.Contains(workspace.Color)) throw new InvalidDataException("Unsupported workspace configuration."); workspaces.Add(workspace with { Id=Guid.NewGuid().ToString(),Updated=DateTimeOffset.UtcNow }); Save(); Status.Text = "Imported safely. Nothing was launched."; });
    }
}