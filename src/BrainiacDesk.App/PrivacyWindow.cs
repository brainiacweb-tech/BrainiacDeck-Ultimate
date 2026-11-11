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
        Note("All processing is local. Clipboard persistence is off by default; opt-in saved shelf content uses Windows account encryption. Monitoring consent lasts only for that shelf session. Scripts saved to the local library and operation history use Windows account encryption. Exported text files are ordinary unencrypted files. Voice recognition starts only with session microphone consent; audio is not saved.");
        Note("Data location: " + AppData);
        var history = new TextBox { IsReadOnly = true, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap }; Body.Children.Add(history); Accessible(history,"Retained activity and diagnostics");
        Action("View recent activity", () => { history.Text = string.Join(Environment.NewLine, LocalConfiguration.Load("activity.json", Array.Empty<string>())); return Task.CompletedTask; });
        Action("View operation history", () => { history.Text = string.Join(Environment.NewLine, OperationJournal.Load().Select(x => $"{x.Time:u} · {x.Module} · {x.Outcome} · {x.Input} → {x.Output}")); return Task.CompletedTask; });
        Action("Clear operation history", () => { OperationJournal.Clear(); history.Clear(); Status.Text = "Operation history cleared."; return Task.CompletedTask; });
        Action("Clear activity history", () => { LocalConfiguration.Save("activity.json", Array.Empty<string>()); history.Clear(); Status.Text = "Activity history cleared."; return Task.CompletedTask; });
        Action("Delete saved tool configurations", () => { if (MessageBox.Show(DialogOwner, "Delete saved workflows, workspaces, command favorites and conversion preset? Open windows retain their current values until closed.", "Confirm deletion", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { foreach (var name in new[] { "workflow.json", "workspaces.json", "favorites.json", "conversion-preset.json" }) { var path = Path.Combine(AppData, name); if (File.Exists(path)) File.Delete(path); } Status.Text = "Saved configurations deleted."; } return Task.CompletedTask; });
        Action("Delete saved scripts and shelf", () => { if (MessageBox.Show(DialogOwner,"Delete protected scripts and shelf content? Close their windows first; exported files are retained.","Delete private data",MessageBoxButton.YesNo) == MessageBoxResult.Yes) { if(Application.Current.Windows.Cast<Window>().Any(x => x is TeleprompterWindow or PromptEditorWindow or ClipboardShelfWindow)) throw new InvalidOperationException("Close the teleprompter, script editor and shelf first."); foreach(var name in new[] { "scripts.protected.json", "shelf.protected.json" }) { var path=Path.Combine(AppData,name); if(File.Exists(path)) File.Delete(path); } Status.Text="Saved scripts and shelf deleted."; } return Task.CompletedTask; });
        Action("Export local preferences", async () => { var path = SaveFile("JSON|*.json", ".json"); if (path is not null) { var settings = await new SettingsStore(Path.Combine(AppData, "settings.db")).LoadAsync(); await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(settings)); } });
        Action("Create support diagnostics", async () => { var path = SaveFile("Text|*.txt", ".txt"); if (path is not null) await File.WriteAllTextAsync(path, $"BrainiacDesk development build\nOS: {Environment.OSVersion}\nRuntime: {Environment.Version}\n64-bit: {Environment.Is64BitProcess}\nNo clipboard contents, scripts, file paths or workflow data included.\n"); Status.Text = "Diagnostics contain only OS/runtime details."; });
        Note("Edge keeps a bounded in-memory thumbnail cache. Explicit Edge pins are persisted in local SQLite; managed captures are saved under Edge/Images. Edge backups contain ordinary unencrypted content and paths. Protected operation history retains at most 200 real operation results and includes input/output paths. Activity retains at most 200 module-opening/completion events and excludes input filenames. User-exported files remain in the locations you selected.");
    }
}