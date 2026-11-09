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
    Console.WriteLine($"{passed} checks passed.");
}
finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(folder)) Directory.Delete(folder, true); }