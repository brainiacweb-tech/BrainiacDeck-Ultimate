using System.Security.Cryptography;
using System.Text;
namespace BrainiacDesk.Core;
public sealed record EdgeItem(string Id, string Kind, string Title, string Content, string? FilePath, string WorkspaceId, string Fingerprint, DateTimeOffset Created, string Tags = "", string OcrText = "", bool Favourite = false, bool Pinned = true, DateTimeOffset? Expires = null);
public sealed record EdgeSettings
{
    public bool Enabled { get; init; } = true;
    public bool AutoCapture { get; init; }
    public bool CaptureOcr { get; init; }
    public bool CaptureOcrToShelf { get; init; }
    public bool AutoHide { get; init; } = true;
    public bool ReducedMotion { get; init; }
    public bool Glass { get; init; }
    public bool RespectFullscreen { get; init; } = true;
    public double Width { get; init; } = 900;
    public double Height { get; init; } = 500;
    public string Alignment { get; init; } = "Center";
    public string Dock { get; init; } = "Top";
    public int Sensitivity { get; init; } = 2;
    public int DwellMilliseconds { get; init; } = 300;
    public string ShortcutKey { get; init; } = "E";
    public uint ShortcutModifiers { get; init; } = 6;
    public string Monitor { get; init; } = "Pointer";
    public string WorkspaceId { get; init; } = "";
    public int CardSize { get; init; } = 100;
    public void Validate()
    {
        if (!double.IsFinite(Width) || !double.IsFinite(Height) || Width is < 360 or > 2400 || Height is < 240 or > 1600 || Sensitivity is < 1 or > 20 || DwellMilliseconds is < 100 or > 2000 || CardSize is < 64 or > 220 || Alignment is not ("Left" or "Center" or "Right") || Dock is not ("Top" or "Bottom" or "Left" or "Right") || ShortcutModifiers is < 1 or > 15 || string.IsNullOrWhiteSpace(ShortcutKey)) throw new ArgumentException("Invalid Edge settings.");
    }
}
public interface IEdgeStore
{
    Task<IReadOnlyList<EdgeItem>> SearchAsync(string query, string? workspaceId = null, bool favourites = false, string? kind = null);
    Task<EdgeItem> AddAsync(EdgeItem item);
    Task UpdateAsync(EdgeItem item);
    Task RemoveAsync(IEnumerable<string> ids);
    Task<int> ClearExpiredAsync(DateTimeOffset now);
    Task<EdgeSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(EdgeSettings settings);
    Task ExportAsync(string path);
    Task RestoreAsync(string path);
}
public static class EdgeRules
{
    public static string Fingerprint(string kind, string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\n" + content)));
    public static void Validate(EdgeItem item)
    {
        if (item is null || item.Title is null || item.Content is null || item.OcrText is null || item.Tags is null || item.WorkspaceId is null || item.Fingerprint is null || !Guid.TryParse(item.Id, out _) || item.Kind is not ("Text" or "Code" or "Note" or "URL" or "Image" or "Screenshot" or "PDF" or "Word" or "Excel" or "Folder" or "File") || string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > 200 || item.Content.Length > 65536 || item.OcrText.Length > 200000 || item.Tags.Length > 2000 || item.WorkspaceId.Length > 100 || item.Fingerprint.Length != 64 || (item.FilePath is not null && (!Path.IsPathFullyQualified(item.FilePath) || item.FilePath.Length > 32000))) throw new InvalidDataException("Invalid pinned content.");
        if (item.Kind == "URL" && (!Uri.TryCreate(item.Content, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))) throw new InvalidDataException("Only HTTP and HTTPS links are supported.");
    }
    public static bool AtEdge(int x, int y, int left, int top, int right, int bottom, string dock, int sensitivity) => dock switch { "Bottom" => y >= bottom-sensitivity && y < bottom, "Left" => x >= left && x < left+sensitivity, "Right" => x >= right-sensitivity && x < right, _ => y >= top && y < top+sensitivity };
}
public sealed class EdgeDwell
{
    private long? since;
    public bool Tick(bool atEdge, long milliseconds, int delay)
    {
        if (!atEdge) { since = null; return false; }
        since ??= milliseconds;
        if (milliseconds - since < delay) return false;
        since = null; return true;
    }
}
