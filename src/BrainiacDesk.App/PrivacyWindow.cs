using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BrainiacDesk.Infrastructure;
namespace BrainiacDesk.App;
public sealed class PrivacyWindow : ToolWindow
{
    public PrivacyWindow() : base("Privacy, Activity and Diagnostics")
    {
        Note("All processing is local. Clipboard content is memory-only and disappears when its shelf closes. Monitoring consent lasts only for that shelf session. Scripts are saved only to files you explicitly choose; BrainiacDesk does not maintain a private script database.");
        Note("Data location: " + AppData);
        var history = new TextBox { IsReadOnly = true, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap }; Body.Children.Add(history);
        Action("View recent activity", () => { history.Text = string.Join(Environment.NewLine, LocalConfiguration.Load("activity.json", Array.Empty<string>())); return Task.CompletedTask; });
        Action("Clear activity history", () => { LocalConfiguration.Save("activity.json", Array.Empty<string>()); history.Clear(); Status.Text = "Activity history cleared."; return Task.CompletedTask; });
        Action("Delete saved workflows and workspaces", () => { if (MessageBox.Show(this, "Delete saved workflow/workspace configurations? Open windows retain their current values until closed.", "Confirm deletion", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { foreach (var name in new[] { "workflow.json", "workspaces.json", "favorites.json", "conversion-preset.json" }) { var path = Path.Combine(AppData, name); if (File.Exists(path)) File.Delete(path); } Status.Text = "Saved configurations deleted."; } return Task.CompletedTask; });
        Action("Export local preferences", async () => { var path = SaveFile("JSON|*.json", ".json"); if (path is not null) { var settings = await new SettingsStore(Path.Combine(AppData, "settings.db")).LoadAsync(); await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(settings)); } });
        Action("Create support diagnostics", async () => { var path = SaveFile("Text|*.txt", ".txt"); if (path is not null) await File.WriteAllTextAsync(path, $"BrainiacDesk development build\nOS: {Environment.OSVersion}\nRuntime: {Environment.Version}\n64-bit: {Environment.Is64BitProcess}\nNo clipboard contents, scripts, file paths or workflow data included.\n"); Status.Text = "Diagnostics contain only OS/runtime details."; });
        Note("No thumbnail cache or conversion history database is maintained. Activity retains at most 200 module-opening/completion events and excludes input filenames. User-exported files remain in the locations you selected.");
    }
}