using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
namespace BrainiacDesk.App;
public sealed record DeskAction(string Id, string Title, string Keywords);
public sealed class ActionRouter(Window owner) : IDisposable
{
    public static readonly DeskAction[] Catalog = [new("convert", "Convert images or create PDF", "converter image png jpg resize compress"), new("shelf", "Open Clipboard Shelf", "snippets copy paste text"), new("prompt", "Start Teleprompter", "script presentation speech"), new("files", "Open File Toolkit", "rename zip archive checksum sha256 sha512 duplicates inspect"), new("workflow", "Open Workflow Studio", "automation batch sequence"), new("capture", "Capture screen", "screenshot annotate redact"), new("workspaces", "Open Workspaces", "workspace configuration"), new("privacy", "Open Privacy Center", "clear history data retention diagnostics"), new("edge", "Open Brainiac Edge", "pinboard floating workspace pins"), new("edge-hide", "Hide Brainiac Edge", "tuck shelf"), new("edge-clipboard", "Pin clipboard item to Edge", "selected clipboard shelf"), new("edge-capture", "Pin latest screenshot to Edge", "capture image"), new("edge-search", "Search pinned content", "edge search OCR tags"), new("edge-expire", "Clear expired Edge items", "cleanup retention"), new("edge-settings", "Brainiac Edge settings", "shortcut docking monitor appearance backup")];
    private readonly Dictionary<string, Window> windows = [];
    private EdgeCoordinator? edge;
    public EdgeCoordinator Edge => edge ??=new EdgeCoordinator(owner,this);
    public CaptureWindow Capture { get { Open("capture"); return (CaptureWindow)windows["capture"]; } }
    public void Dispose() { edge?.Dispose(); CloseAll(); }
    public ClipboardShelfWindow Shelf { get { Open("shelf"); return (ClipboardShelfWindow)windows["shelf"]; } }
    public void Open(string id)
    {
        try { OpenCore(id); }
        catch (Exception ex) { MessageBox.Show(owner, "Could not open the tool. Your saved files were preserved.\n\n" + ex.Message, "BrainiacDesk", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void OpenCore(string id)
    {
        if(id=="prompt" && owner is MainWindow promptShell) { promptShell.OpenPromptWorkspace(); ActivityLog.Record("Opened prompt"); return; }
        if(id.StartsWith("edge",StringComparison.Ordinal)) { _=RunEdgeAsync(id); return; }
        if (!Catalog.Any(x => x.Id == id)) throw new ArgumentException("Unknown action.");
        if (!windows.TryGetValue(id, out var window))
        {
            window = id switch { "convert" => new ConverterWindow(), "shelf" => new ClipboardShelfWindow(item=>Edge.Content.PinShelfAsync(item)), "prompt" => new TeleprompterWindow(), "files" => new FileToolsWindow(), "workflow" => new WorkflowWindow(paths=>PinWorkflowAsync(paths),Edge), "capture" => new CaptureWindow(this), "workspaces" => new WorkspaceWindow(this), "privacy" => new PrivacyWindow(), _ => throw new ArgumentException("Unknown action.") };
            windows[id] = window; if (id != "prompt") window.Owner = owner; window.Closed += (_, _) => windows.Remove(id);
        }
        if(window is ToolWindow tool && owner is MainWindow shell) { shell.ShowToolPage(tool); ActivityLog.Record("Opened " + id); return; }
        window.Show(); if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; if (id != "prompt") window.Activate();
        ActivityLog.Record("Opened " + id);
    }
    private async System.Threading.Tasks.Task PinWorkflowAsync(string[] paths) { foreach(var path in paths) await Edge.Content.PinFileAsync(path); }
    private async System.Threading.Tasks.Task RunEdgeAsync(string id)
    {
        try { switch(id) { case "edge": Edge.Show(); break; case "edge-settings": new EdgeSettingsWindow(Edge).OpenInWorkspace(); break; case "edge-hide": Edge.Hide(); break; case "edge-search": Edge.Search(); break; case "edge-clipboard": await Edge.PinShelfSelectionAsync(); break; case "edge-capture": await Edge.PinLatestAsync(); break; case "edge-expire": await Edge.ClearExpiredAsync(); break; default: throw new ArgumentException("Unknown Edge command."); } }
        catch(Exception ex) { Edge.Model.Status=ex.Message; MessageBox.Show(owner,ex.Message,"Brainiac Edge"); }
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
        Directory.CreateDirectory(ActivityLog.Folder); var path = Path.Combine(ActivityLog.Folder, name); var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static T Load<T>(string name, T fallback) { var path = Path.Combine(ActivityLog.Folder, name); return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback : fallback; }
}