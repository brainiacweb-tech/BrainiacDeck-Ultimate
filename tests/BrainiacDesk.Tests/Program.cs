using BrainiacDesk.Core;
using BrainiacDesk.Infrastructure;
var folder = Path.Combine(Path.GetTempPath(), "BrainiacDeskTests", Guid.NewGuid().ToString("N"));
var path = Path.Combine(folder, "settings.db");
var passed = 0;
void Check(bool result, string name) { if (!result) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); passed++; }
try
{
    var store = new SettingsStore(path);
    Check(await store.LoadAsync() == new UserSettings(), "Safe first-run defaults");
    var settings = new UserSettings { Theme = "Light", MinimizeToTray = false, ConfirmExit = false, OnboardingComplete = true };
    await store.SaveAsync(settings);
    Check(await new SettingsStore(path).LoadAsync() == settings, "Preferences survive service restart");
    await store.SaveAsync(settings with { Theme = "Dark" });
    Check((await store.LoadAsync()).Theme == "Dark", "Existing settings update");
    await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => store.SaveAsync(settings)));
    Check(await store.LoadAsync() == settings, "Concurrent saves remain readable");
    var invalidPath = Path.Combine(folder, "not-a-folder");
    await File.WriteAllTextAsync(invalidPath, "block");
    var failed = false;
    try { await new SettingsStore(Path.Combine(invalidPath, "settings.db")).LoadAsync(); } catch (IOException) { failed = true; }
    Check(failed, "Storage errors propagate without false success");
    var a = Path.Combine(folder, "a.txt"); var b = Path.Combine(folder, "b.txt"); var c = Path.Combine(folder, "c.txt");
    await File.WriteAllTextAsync(a, "abc"); await File.WriteAllTextAsync(b, "abc"); await File.WriteAllTextAsync(c, "xyz");
    Check(await FileOperations.HashAsync(a) == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", "SHA-256 known vector");
    Check((await FileOperations.HashAsync(a, true)).StartsWith("DDAF35A193617ABA"), "SHA-512 known vector");
    var scan = Path.Combine(folder, "scan"); Directory.CreateDirectory(scan); File.Copy(a, Path.Combine(scan, "a.txt")); File.Copy(b, Path.Combine(scan, "b.txt")); File.Copy(c, Path.Combine(scan, "c.txt"));
    var groups = await FileOperations.DuplicatesAsync(scan, CancellationToken.None);
    Check(groups.Count == 1 && groups[0].Length == 2, "Duplicates verified by size, hash and bytes");
    var plan = FileOperations.PlanRename([a, b], "renamed-", "", 1); FileOperations.Rename(plan);
    Check(plan.All(x => File.Exists(x.Destination) && !File.Exists(x.Source)), "Batch rename executes reviewed plan");
    FileOperations.Rename(plan.Select(x => new RenameEntry(x.Destination, x.Source)).ToArray()); Check(File.Exists(a) && File.Exists(b), "Batch rename undo");
    var collision = false; try { FileOperations.PlanRename([a], "", "", 1); } catch { collision = true; }
    // Explicit existing output collision.
    await File.WriteAllTextAsync(Path.Combine(folder, "taken001.txt"), "keep");
    try { FileOperations.PlanRename([a], "taken", "", 1); } catch (IOException) { collision = true; }
    Check(collision, "Rename output collision rejected");
    var zip = Path.Combine(folder, "bundle.zip"); await FileOperations.CreateZipAsync([a, b], zip, CancellationToken.None);
    var extracted = Path.Combine(folder, "extracted"); await FileOperations.ExtractZipAsync(zip, extracted, CancellationToken.None);
    Check(await File.ReadAllTextAsync(Path.Combine(extracted, "a.txt")) == "abc", "ZIP round trip");
    bool traversal = false; try { FileOperations.SafeArchivePath(extracted, "../escaped.txt"); } catch (InvalidDataException) { traversal = true; } Check(traversal, "Archive traversal rejected");
    var malicious = Path.Combine(folder, "malicious.zip"); using (var archive = System.IO.Compression.ZipFile.Open(malicious, System.IO.Compression.ZipArchiveMode.Create)) { var entry = archive.CreateEntry("../escape.txt"); using var writer = new StreamWriter(entry.Open()); writer.Write("bad"); }
    bool rejected = false; try { await FileOperations.ExtractZipAsync(malicious, Path.Combine(folder, "unsafe"), CancellationToken.None); } catch (InvalidDataException) { rejected = true; }
    Check(rejected && !File.Exists(Path.Combine(folder, "escape.txt")) && !Directory.Exists(Path.Combine(folder, "unsafe")), "Malicious ZIP leaves no extraction output");
    using var cancel = new CancellationTokenSource(); cancel.Cancel(); var cancelled = false;
    try { await FileOperations.CreateZipAsync([a], Path.Combine(folder, "cancel.zip"), cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled && !File.Exists(Path.Combine(folder, "cancel.zip")), "ZIP cancellation cleans output");
    var outputFolder = Path.Combine(folder, "output"); Directory.CreateDirectory(outputFolder);
    var workflow = new WorkflowDefinition("Test", scan, outputFolder, ".txt", [new("Copy"), new("SHA-256"), new("ZIP")]);
    Check(WorkflowEngine.Preview(workflow, Directory.GetFiles(scan)).Length == 3, "Workflow dry run selects authorized files");
    var denied = false; try { WorkflowEngine.Preview(workflow, [a]); } catch (UnauthorizedAccessException) { denied = true; } Check(denied, "Workflow rejects unapproved files");
    var loop = false; try { WorkflowEngine.Validate(workflow with { OutputFolder = scan }); } catch (InvalidOperationException) { loop = true; } Check(loop, "Recursive workflow folder rejected");
    await WorkflowEngine.RunAsync(workflow, Directory.GetFiles(scan), new Progress<string>(), CancellationToken.None);
    var run = Directory.GetDirectories(outputFolder).Single(); Check(File.Exists(Path.Combine(run, "bundle.zip")) && File.Exists(Path.Combine(run, "checksums.txt")) && File.Exists(Path.Combine(run, "a.txt")), "Workflow copy / hash / ZIP executes end-to-end");
    Console.WriteLine($"{passed} checks passed.");
}
finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }