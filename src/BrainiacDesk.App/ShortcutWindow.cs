using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace BrainiacDesk.App;
public sealed class ShortcutWindow : ToolWindow
{
    public ShortcutWindow(ShortcutService service) : base("Shortcut Studio")
    {
        Note("Command Center global shortcut: Ctrl + Shift + selected key. Registration can fail when another app owns the combination. Changes apply immediately and persist only after successful registration.");
        var saved = LocalConfiguration.Load("shortcut.json", new ShortcutPreference());
        var keys = new ComboBox { ItemsSource = new[] { "Space", "B", "D", "F8", "F9", "F10" }, SelectedItem = saved.Key }; Body.Children.Add(keys);
        var enabled = new CheckBox { Content = "Enable command shortcut", IsChecked = saved.Enabled }; Body.Children.Add(enabled);
        Action("Apply shortcut", () => { var key = (string?)keys.SelectedItem ?? "Space"; var active = enabled.IsChecked == true; if (active && !service.Set(6, (uint)KeyInterop.VirtualKeyFromKey(Enum.Parse<Key>(key)))) { Status.Text = "Shortcut unavailable. Choose another combination. Global shortcut is currently disabled."; return Task.CompletedTask; } if (!active) service.Set(0, 0); LocalConfiguration.Save("shortcut.json", new ShortcutPreference(key, active)); Status.Text = active ? "Registered Ctrl + Shift + " + key : "Global shortcut disabled."; return Task.CompletedTask; });
        Action("Reset default selection", () => { keys.SelectedItem = "Space"; enabled.IsChecked = true; Status.Text = "Default selected. Apply to register."; return Task.CompletedTask; });
        Action("Export configuration", async () => { var path = SaveFile("JSON|*.json", ".json"); if (path is not null) await System.IO.File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new ShortcutPreference((string?)keys.SelectedItem ?? "Space", enabled.IsChecked == true))); });
    }
}