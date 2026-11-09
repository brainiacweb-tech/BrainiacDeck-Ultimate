using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
namespace BrainiacDesk.App;
public sealed record DeskAction(string Id, string Title, string Keywords);
public sealed class ActionRouter(Window owner)
{
    public static readonly DeskAction[] Catalog = [new("convert", "Convert images or create PDF", "converter image png jpg resize compress"), new("shelf", "Open Clipboard Shelf", "snippets copy paste text"), new("prompt", "Start Teleprompter", "script presentation speech"), new("files", "Open File Toolkit", "rename zip archive checksum sha256 sha512 duplicates inspect"), new("workflow", "Open Workflow Studio", "automation batch sequence"), new("capture", "Capture screen", "screenshot annotate redact"), new("workspaces", "Open Workspaces", "workspace configuration"), new("privacy", "Open Privacy Center", "clear history data retention diagnostics")];
    private readonly Dictionary<string, Window> windows = [];
    public ClipboardShelfWindow Shelf { get { Open("shelf"); return (ClipboardShelfWindow)windows["shelf"]; } }
    public void Open(string id)
    {
        try { OpenCore(id); }
        catch (Exception ex) { MessageBox.Show(owner, "Could not open the tool. Your saved files were preserved.\n\n" + ex.Message, "BrainiacDesk", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void OpenCore(string id)
    {
        if (!Catalog.Any(x => x.Id == id)) throw new ArgumentException("Unknown action.");
        if (!windows.TryGetValue(id, out var window))
        {
            window = id switch { "convert" => new ConverterWindow(), "shelf" => new ClipboardShelfWindow(), "prompt" => new TeleprompterWindow(), "files" => new FileToolsWindow(), "workflow" => new WorkflowWindow(), "capture" => new CaptureWindow(this), "workspaces" => new WorkspaceWindow(this), "privacy" => new PrivacyWindow(), _ => throw new ArgumentException("Unknown action.") };
            windows[id] = window; window.Owner = owner; window.Closed += (_, _) => windows.Remove(id);
        }
        window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; window.Activate();
        ActivityLog.Record("Opened " + id);
    }
    public void CloseAll() { foreach (var window in windows.Values.ToArray()) window.Close(); }
    public static int Score(DeskAction action, string query)
    {
        query = query.Trim().ToLowerInvariant(); if (query.Length == 0) return 1;
        var haystack = (action.Title + " " + action.Keywords).ToLowerInvariant();
        if (haystack.Contains(query, StringComparison.Ordinal)) return 1000 - haystack.IndexOf(query, StringComparison.Ordinal);
        int position = 0, score = 0;
        foreach (var c in query) { var next = haystack.IndexOf(c, position); if (next < 0) return 0; score += 10 - Math.Min(9, next - position); position = next + 1; }
        return score;
    }
}
public static class ActivityLog
{
    public static string Folder => BrainiacDesk.Core.LocalPaths.Data;
    public static void Record(string message)
    {
        try { Directory.CreateDirectory(Folder); var path = Path.Combine(Folder, "activity.json"); var entries = File.Exists(path) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [] : []; entries.Insert(0, DateTimeOffset.Now.ToString("u") + " " + message); LocalConfiguration.Save("activity.json", entries.Take(200).ToArray()); } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (JsonException) { }
    }
}
public static class LocalConfiguration
{
    public static void Save<T>(string name, T value)
    {
        Directory.CreateDirectory(ActivityLog.Folder); var path = Path.Combine(ActivityLog.Folder, name); var temp = path + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static T Load<T>(string name, T fallback) { var path = Path.Combine(ActivityLog.Folder, name); return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback : fallback; }
}