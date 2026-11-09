using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace BrainiacDesk.App;
public sealed class CommandWindow : ToolWindow
{
    public CommandWindow(ActionRouter router) : base("Universal Command Center")
    {
        Width = 720; Height = 480; Topmost = true;
        Note("Type a local action. Up/Down selects, Enter executes, Escape closes. Commands open the relevant tool; file operations require their own explicit choices.");
        var search = Input(); var list = new ListBox { Height = 200, DisplayMemberPath = "Title" }; Body.Children.Add(list);
        var favorites = LocalConfiguration.Load("favorites.json", new List<string>());
        void Refresh() { list.ItemsSource = ActionRouter.Catalog.Select(action => (action, score: ActionRouter.Score(action, search.Text))).Where(x => x.score > 0).OrderByDescending(x => x.score + (favorites.Contains(x.action.Id) ? 5 : 0)).Select(x => x.action).ToArray(); list.SelectedIndex = 0; }
        void Execute() { if (list.SelectedItem is DeskAction action) { router.Open(action.Id); Dispatcher.BeginInvoke(() => Close()); } }
        search.TextChanged += (_, _) => Refresh();
        search.PreviewKeyDown += (_, e) => { if (e.Key == Key.Down) { list.SelectedIndex = Math.Min(list.Items.Count - 1, list.SelectedIndex + 1); e.Handled = true; } else if (e.Key == Key.Up) { list.SelectedIndex = Math.Max(0, list.SelectedIndex - 1); e.Handled = true; } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { Execute(); e.Handled = true; } else if (e.Key == Key.Escape) Close(); };
        list.MouseDoubleClick += (_, _) => Execute();
        Action("Run selected action", () => { Execute(); return Task.CompletedTask; });
        Action("Favorite / unfavorite", () => { if (list.SelectedItem is DeskAction action) { if (!favorites.Remove(action.Id)) favorites.Add(action.Id); LocalConfiguration.Save("favorites.json", favorites); Refresh(); } return Task.CompletedTask; });
        Loaded += (_, _) => { Refresh(); search.Focus(); };
    }
}