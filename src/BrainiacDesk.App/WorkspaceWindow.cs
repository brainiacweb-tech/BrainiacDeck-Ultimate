using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BrainiacDesk.App;
public sealed record Workspace(string Name, string Color, string[] Actions);
public sealed class WorkspaceWindow : ToolWindow
{
    public WorkspaceWindow(ActionRouter router) : base("Workspaces")
    {
        Note("Save named collections of BrainiacDesk tools. Importing a configuration never launches tools. Third-party application state is not restored.");
        var workspaces = LocalConfiguration.Load("workspaces.json", new List<Workspace>());
        var list = new ListBox { ItemsSource = workspaces, DisplayMemberPath = "Name", Height = 130 }; Body.Children.Add(list);
        var name = Input("My workspace"); var color = new ComboBox { ItemsSource = new[] { "Orange", "Blue", "Green", "Purple" }, SelectedIndex = 0 }; Body.Children.Add(color);
        var actions = new ListBox { ItemsSource = ActionRouter.Catalog.Where(x => x.Id != "workspaces").ToArray(), DisplayMemberPath = "Title", SelectionMode = SelectionMode.Multiple, Height = 160 }; Body.Children.Add(actions);
        void Save() { LocalConfiguration.Save("workspaces.json", workspaces); list.Items.Refresh(); }
        Workspace Definition() { var title = name.Text.Trim(); if (title.Length is 0 or > 80) throw new InvalidOperationException("Workspace name must be 1–80 characters."); return new(title, (string)color.SelectedItem, actions.SelectedItems.Cast<DeskAction>().Select(x => x.Id).ToArray()); }
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is Workspace workspace) { name.Text = workspace.Name; color.SelectedItem = workspace.Color; actions.SelectedItems.Clear(); foreach (var action in actions.Items.Cast<DeskAction>().Where(x => workspace.Actions.Contains(x.Id))) actions.SelectedItems.Add(action); } };
        Action("Create workspace", () => { workspaces.Add(Definition()); Save(); return Task.CompletedTask; });
        Action("Update selected workspace", () => { if (list.SelectedItem is Workspace selected) { workspaces[workspaces.IndexOf(selected)] = Definition(); Save(); } return Task.CompletedTask; });
        Action("Duplicate selected", () => { if (list.SelectedItem is Workspace selected) { workspaces.Add(selected with { Name = selected.Name + " copy" }); Save(); } return Task.CompletedTask; });
        Action("Open selected workspace", () => { if (list.SelectedItem is Workspace selected) foreach (var id in selected.Actions) router.Open(id); return Task.CompletedTask; });
        Action("Delete selected workspace", () => { if (list.SelectedItem is Workspace selected && MessageBox.Show(this, "Delete workspace configuration?", "Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { workspaces.Remove(selected); Save(); } return Task.CompletedTask; });
        Action("Export selected", async () => { if (list.SelectedItem is Workspace selected) { var path = SaveFile("JSON|*.json", ".json"); if (path is not null) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(selected)); } });
        Action("Import configuration", async () => { var paths = PickFiles("JSON|*.json"); if (paths.Length == 0) return; if (new FileInfo(paths[0]).Length > 65536) throw new InvalidDataException("Configuration exceeds 64 KB."); var workspace = JsonSerializer.Deserialize<Workspace>(await File.ReadAllTextAsync(paths[0])) ?? throw new InvalidDataException("Invalid workspace."); if (string.IsNullOrWhiteSpace(workspace.Name) || workspace.Name.Length > 80 || workspace.Actions is null || workspace.Actions.Length > 10 || workspace.Actions.Any(id => id == "workspaces" || !ActionRouter.Catalog.Any(x => x.Id == id)) || !new[] { "Orange", "Blue", "Green", "Purple" }.Contains(workspace.Color)) throw new InvalidDataException("Unsupported workspace configuration."); workspaces.Add(workspace); Save(); Status.Text = "Imported safely. Nothing was launched."; });
    }
}