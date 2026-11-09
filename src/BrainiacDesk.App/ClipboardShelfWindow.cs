using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace BrainiacDesk.App;
public sealed record ShelfItem(string Kind, string Text, object Payload, DateTime Created, bool Pinned = false)
{
    public override string ToString() => (Pinned ? "[Pinned] " : "") + Kind + " · " + Text.Replace('\n', ' ')[..Math.Min(Text.Length, 120)];
}
public sealed class ClipboardShelfWindow : ToolWindow
{
    private readonly ObservableCollection<ShelfItem> items = [];
    private readonly ListBox list;
    private readonly CheckBox monitor = new() { Content = "Enable clipboard monitoring for this session (explicit consent)", IsChecked = false };
    private readonly DispatcherTimer expiry = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly TextBox exclusions;
    private HwndSource? source;
    private bool registered;
    private bool ownWrite;
    private const int Limit = 50;
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    public ClipboardShelfWindow() : base("Clipboard Shelf")
    {
        Topmost = true; Width = 760; Height = 620; Top = 12;
        Note("Memory-only shelf. Maximum 50 items; unpinned items expire after one hour. Closing clears everything and stops monitoring. Clipboard passwords cannot always be detected: pause monitoring before copying private content.");
        Body.Children.Add(monitor);
        monitor.Checked += (_, _) => SetMonitoring(true); monitor.Unchecked += (_, _) => SetMonitoring(false);
        Note("Exclude text containing these words (comma-separated)"); exclusions = Input("password,secret,token,api_key");
        var search = Input(); search.ToolTip = "Search shelf";
        list = new ListBox { ItemsSource = items, Height = 180 }; Body.Children.Add(list);
        var view = CollectionViewSource.GetDefaultView(items); view.Filter = o => o is ShelfItem item && item.Text.Contains(search.Text, StringComparison.OrdinalIgnoreCase);
        search.TextChanged += (_, _) => view.Refresh();
        Action("Add clipboard once", () => { ReadClipboard(); return Task.CompletedTask; });
        var paste = new ComboBox { ItemsSource = new[] { "Original", "Uppercase", "Lowercase", "Title case", "Normalize whitespace" }, SelectedIndex = 0 }; Body.Children.Add(paste);
        Action("Copy selected", () => { if (list.SelectedItem is not ShelfItem item) return Task.CompletedTask; ownWrite = true; try { if (item.Payload is BitmapSource bitmap) System.Windows.Clipboard.SetImage(bitmap); else if (item.Payload is string[] paths) { var collection = new StringCollection(); collection.AddRange(paths); System.Windows.Clipboard.SetFileDropList(collection); } else System.Windows.Clipboard.SetText(Transform(item.Text, (string)paste.SelectedItem)); } finally { Dispatcher.BeginInvoke(() => ownWrite = false, DispatcherPriority.ContextIdle); } return Task.CompletedTask; });
        Action("Pin / unpin", () => { if (list.SelectedItem is ShelfItem item) { var i = items.IndexOf(item); items[i] = item with { Pinned = !item.Pinned }; } return Task.CompletedTask; });
        Action("Move selected up", () => { if (list.SelectedItem is ShelfItem item && items.IndexOf(item) > 0) items.Move(items.IndexOf(item), items.IndexOf(item) - 1); return Task.CompletedTask; });
        Action("Open text in teleprompter", () => { if (list.SelectedItem is ShelfItem item && item.Kind == "Text") new TeleprompterWindow(item.Text).Show(); return Task.CompletedTask; });
        Action("Save text as TXT", async () => { if (list.SelectedItem is ShelfItem item && item.Kind == "Text") { var path = SaveFile("Text|*.txt", ".txt"); if (path is not null) await System.IO.File.WriteAllTextAsync(path, item.Text); } });
        Action("Delete selected", () => { if (list.SelectedItem is ShelfItem item) items.Remove(item); return Task.CompletedTask; });
        Action("Clear shelf", () => { items.Clear(); Status.Text = "Shelf cleared from application memory."; return Task.CompletedTask; });
        Action("Collapse / expand", () => { Height = Height > 200 ? 180 : 620; return Task.CompletedTask; });
        AllowDrop = true; Drop += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths) Add(new("Files", string.Join("; ", paths), paths, DateTime.UtcNow)); else if (e.Data.GetData(DataFormats.UnicodeText) is string text) AddSnippet(text); };
        SourceInitialized += (_, _) => { source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source.AddHook(Hook); };
        expiry.Tick += (_, _) => { foreach (var item in items.Where(x => !x.Pinned && DateTime.UtcNow - x.Created > TimeSpan.FromHours(1)).ToArray()) items.Remove(item); }; expiry.Start();
        Closed += (_, _) => { SetMonitoring(false); source?.RemoveHook(Hook); expiry.Stop(); items.Clear(); };
        // Double-click the title bar uses native maximize/restore, so collapsing remains recoverable through the context menu.
        var menu = new ContextMenu(); var expand = new MenuItem { Header = "Expand shelf" }; expand.Click += (_, _) => { Body.Visibility = Visibility.Visible; Height = 620; }; menu.Items.Add(expand); ContextMenu = menu;
    }
    public static string Transform(string text, string mode) => mode switch { "Uppercase" => text.ToUpperInvariant(), "Lowercase" => text.ToLowerInvariant(), "Title case" => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture)), "Normalize whitespace" => Regex.Replace(text, @"\s+", " ").Trim(), _ => text };
    public void AddImage(BitmapSource image) { if ((long)image.PixelWidth * image.PixelHeight > 8_000_000) throw new InvalidOperationException("Shelf images must be under 8 megapixels."); var clone = image.Clone(); clone.Freeze(); Add(new("Image", $"{image.PixelWidth} × {image.PixelHeight}", clone, DateTime.UtcNow)); }
    public void AddSnippet(string text)
    {
        if (text.Length > 65536) { Status.Text = "Text exceeds 64K character limit."; return; }
        if (exclusions.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase))) { Status.Text = "Text excluded by privacy filter."; return; }
        if (!string.IsNullOrWhiteSpace(text)) Add(new("Text", text, text, DateTime.UtcNow));
    }
    private void Add(ShelfItem item)
    {
        if (items.Count >= Limit) { var oldest = items.LastOrDefault(x => !x.Pinned); if (oldest is null) { Status.Text = "Shelf is full of pinned items. Remove one first."; return; } items.Remove(oldest); }
        items.Insert(0, item); Status.Text = $"{items.Count}/{Limit} items · memory only";
    }
    private void ReadClipboard()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing")) { Status.Text = "Clipboard owner requested exclusion."; return; }
            if (System.Windows.Clipboard.ContainsText()) AddSnippet(System.Windows.Clipboard.GetText());
            else if (System.Windows.Clipboard.ContainsFileDropList()) { var paths = System.Windows.Clipboard.GetFileDropList().Cast<string>().ToArray(); Add(new("Files", string.Join("; ", paths), paths, DateTime.UtcNow)); }
            else if (System.Windows.Clipboard.ContainsImage() && System.Windows.Clipboard.GetImage() is BitmapSource bitmap) AddImage(bitmap);
        }
        catch (Exception ex) { Status.Text = "Clipboard unavailable: " + ex.Message; }
    }
    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) { if (message == 0x031D && registered && !ownWrite) ReadClipboard(); return IntPtr.Zero; }
    private void SetMonitoring(bool enabled)
    {
        if (source is null) return;
        if (enabled && !registered) { registered = AddClipboardFormatListener(source.Handle); if (!registered) { monitor.IsChecked = false; Status.Text = "Windows could not register clipboard monitoring."; } else Status.Text = "Monitoring enabled for this session. Pause before copying private data."; }
        else if (!enabled && registered) { RemoveClipboardFormatListener(source.Handle); registered = false; Status.Text = "Clipboard monitoring paused."; }
    }
}