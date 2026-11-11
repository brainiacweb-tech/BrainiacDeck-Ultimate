using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
public sealed class FileToolsWindow : ToolWindow
{
    private string[] files = [];
    private RenameEntry[]? plan;
    private RenameEntry[]? undo;
    private CancellationTokenSource? cancellation;
    public FileToolsWindow() : base("File Management Toolkit")
    {
        Note("Choose files for checksums, inspection, ZIP, or safe rename. Duplicate scans never delete files. ZIP extraction uses a new isolated folder with a 2 GB / 5000-entry limit.");
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }; Body.Children.Add(output);
        Action("Select files", () => { files = PickFiles(); plan = null; output.Text = string.Join(Environment.NewLine, files); return Task.CompletedTask; });
        var expected = Input(); expected.ToolTip = "Optional expected checksum";
        var hashType = new ComboBox { ItemsSource = new[] { "SHA-256", "SHA-512" }, SelectedIndex = 0 }; Body.Children.Add(hashType);
        Action("Calculate / compare hashes", async () => { if(files.Length == 0) throw new InvalidOperationException("Select files first."); var result = ""; var sha512 = hashType.SelectedIndex == 1; foreach (var file in files) { var hash = await FileOperations.HashAsync(file, sha512); result += file + Environment.NewLine + hash + (string.IsNullOrWhiteSpace(expected.Text) ? "" : hash.Equals(expected.Text.Trim(), StringComparison.OrdinalIgnoreCase) ? " MATCH" : " MISMATCH") + Environment.NewLine; } output.Text = result; });
        Action("Copy results", () => { if (output.Text.Length > 0) System.Windows.Clipboard.SetText(output.Text); return Task.CompletedTask; });
        Action("Export results", async () => { var path = SaveFile("Text|*.txt", ".txt"); if (path is not null) await File.WriteAllTextAsync(path, output.Text); });
        Action("Inspect files", () => { if(files.Length == 0) throw new InvalidOperationException("Select files first."); output.Text = string.Join(Environment.NewLine + Environment.NewLine, files.Select(path => { var f = new FileInfo(path); return $"{f.FullName}\nType: {f.Extension}\nBytes: {f.Length:N0}\nCreated UTC: {f.CreationTimeUtc:O}\nModified UTC: {f.LastWriteTimeUtc:O}"; })); return Task.CompletedTask; });
        Note("Numbered rename: prefix + 001 + suffix + original extension. Preview is required. Originals are never overwritten.");
        var prefix = Input("File-"); var suffix = Input();
        Accessible(output,"File operation results"); Accessible(expected,"Expected checksum"); Accessible(hashType,"Checksum algorithm"); Accessible(prefix,"Rename prefix"); Accessible(suffix,"Rename suffix");
        prefix.TextChanged += (_, _) => plan = null; suffix.TextChanged += (_, _) => plan = null;
        Action("Preview rename", () => { if(files.Length == 0) throw new InvalidOperationException("Select files first."); plan = FileOperations.PlanRename(files, prefix.Text, suffix.Text, 1); output.Text = string.Join(Environment.NewLine, plan.Select(x => x.Source + " → " + x.Destination)); return Task.CompletedTask; });
        Action("Apply reviewed rename", () => { if (plan is null || plan.Length == 0) throw new InvalidOperationException("Preview the rename first."); if (MessageBox.Show(DialogOwner, "Apply the displayed rename plan?", "Confirm rename", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { FileOperations.Rename(plan); undo = plan.Select(x => new RenameEntry(x.Destination, x.Source)).ToArray(); files = plan.Select(x => x.Destination).ToArray(); plan = null; Status.Text = "Renamed. Undo remains available until the next rename or window close."; } return Task.CompletedTask; });
        Action("Undo last rename", () => { if (undo is null) throw new InvalidOperationException("No rename is available to undo in this session."); if (undo is not null) { FileOperations.Rename(undo); files = undo.Select(x => x.Destination).ToArray(); undo = null; Status.Text = "Rename undone."; } return Task.CompletedTask; });
        Action("Create ZIP", async () => { if (files.Length == 0) throw new InvalidOperationException("Select files first."); var path = SaveFile("ZIP|*.zip", ".zip"); if (path is not null) { await FileOperations.CreateZipAsync(files, path, CancellationToken.None); Status.Text = "ZIP created: " + path; } });
        Action("Preview ZIP", () => { var paths = PickFiles("ZIP|*.zip"); if (paths.Length > 0) { using var archive = ZipFile.OpenRead(paths[0]); output.Text = string.Join(Environment.NewLine, archive.Entries.Take(5000).Select(x => $"{x.FullName} · {x.Length:N0} bytes")); } return Task.CompletedTask; });
        Action("Extract ZIP safely", async () => { var paths = PickFiles("ZIP|*.zip"); if (paths.Length == 0) return; var folder = PickFolder(); if (folder is null) return; var destination = ConversionEngine.UniquePath(folder, "Extracted archive", ""); await FileOperations.ExtractZipAsync(paths[0], destination, CancellationToken.None); Status.Text = "Extracted: " + destination; });
        Action("Find verified duplicates in folder", async () => { var folder = PickFolder(); if (folder is null || cancellation is not null) return; cancellation = new(); try { var progress = new Progress<string>(path => Status.Text = "Hashing: " + path); var result = await Task.Run(() => FileOperations.DuplicatesAsync(folder, cancellation.Token, progress)); output.Text = result.Count == 0 ? "No verified duplicates found." : string.Join(Environment.NewLine + Environment.NewLine, result.Select(group => string.Join(Environment.NewLine, group))); Status.Text = $"{result.Count} verified duplicate groups. No files deleted."; } catch (OperationCanceledException) { Status.Text = "Scan cancelled."; } finally { cancellation.Dispose(); cancellation = null; } });
        Action("Cancel duplicate scan", () => { cancellation?.Cancel(); return Task.CompletedTask; });
        var buttons=Body.Children.OfType<Button>().ToDictionary(x=>(string)x.Content);
        Body.Children.Clear();
        Note("Select your source files, then choose a focused tool. Verified operations preserve originals and require review before renaming.");
        var selection=new WrapPanel(); selection.Children.Add(buttons["Select files"]); selection.Children.Add(buttons["Inspect files"]); Body.Children.Add(selection); Body.Children.Add(output);
        var resultsActions=new WrapPanel(); resultsActions.Children.Add(buttons["Copy results"]); resultsActions.Children.Add(buttons["Export results"]); Body.Children.Add(resultsActions);
        var tabs=new TabControl { Margin=new Thickness(0,20,0,0) }; Body.Children.Add(tabs);
        StackPanel Tab(string title) { var panel=new StackPanel { Margin=new Thickness(8,16,8,8) }; tabs.Items.Add(new TabItem { Header=title,Content=panel }); return panel; }
        void Caption(Panel parent,string label) { var text=new TextBlock { Text=label,Margin=new Thickness(0,8,0,8) }; text.SetResourceReference(TextBlock.ForegroundProperty,"Muted"); parent.Children.Add(text); }
        void Buttons(Panel parent,params string[] labels) { var row=new WrapPanel(); foreach(var label in labels) row.Children.Add(buttons[label]); parent.Children.Add(row); }
        var hashes=Tab("Checksums"); Caption(hashes,"Expected checksum (optional)"); hashes.Children.Add(expected); Caption(hashes,"Algorithm"); hashes.Children.Add(hashType); Buttons(hashes,"Calculate / compare hashes");
        var rename=Tab("Rename"); Caption(rename,"Prefix"); rename.Children.Add(prefix); Caption(rename,"Suffix"); rename.Children.Add(suffix); Caption(rename,"Names use prefix + 001 + suffix + original extension. Preview before applying."); Buttons(rename,"Preview rename","Apply reviewed rename","Undo last rename");
        var archive=Tab("Archives"); Caption(archive,"ZIP tools use the selected files; extraction creates a new isolated folder."); Buttons(archive,"Create ZIP","Preview ZIP","Extract ZIP safely");
        var duplicates=Tab("Duplicates"); Caption(duplicates,"Compare file size, hash and bytes. Scanning never deletes files."); Buttons(duplicates,"Find verified duplicates in folder","Cancel duplicate scan");
        Closed += (_, _) => cancellation?.Cancel();
    }
}