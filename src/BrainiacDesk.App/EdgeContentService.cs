using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using BrainiacDesk.Core;
namespace BrainiacDesk.App;
/// <summary>Explicit content handoffs. This service never registers a clipboard listener.</summary>
public sealed class EdgeContentService(IEdgeStore store)
{
    public string WorkspaceId { get; set; } = "";
    public event Action? Changed;
    public event Action<string>? Error;
    private readonly SemaphoreSlim imageGate = new(1,1);
    public async Task<EdgeItem> PinTextAsync(string text, string kind = "Text")
    {
        if(string.IsNullOrWhiteSpace(text) || text.Length>65536) throw new InvalidDataException("Pin text must contain 1–65536 characters.");
        if(kind=="Text" && Uri.TryCreate(text,UriKind.Absolute,out var uri) && uri.Scheme is "https" or "http") kind="URL";
        return await Add(new(Guid.NewGuid().ToString(),kind,text.Replace('\n',' ')[..Math.Min(100,text.Length)],text,null,WorkspaceId,EdgeRules.Fingerprint(kind,text),DateTimeOffset.UtcNow));
    }
    public async Task<EdgeItem> PinFileAsync(string path)
    {
        var workspace=WorkspaceId; path=Path.GetFullPath(path); if(!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("The original file is missing.",path);
        var kind=Directory.Exists(path) ? "Folder" : Path.GetExtension(path).ToLowerInvariant() switch { ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tif" or ".tiff" or ".gif" => "Image", ".pdf"=>"PDF", ".doc" or ".docx"=>"Word", ".xls" or ".xlsx" or ".csv"=>"Excel", _=>"File" };
        var fingerprint=EdgeRules.Fingerprint("Path",path.ToUpperInvariant());
        if(kind=="Image") fingerprint=await ImageFingerprintAsync(path);
        return await Add(new(Guid.NewGuid().ToString(),kind,Path.GetFileName(path),"",path,workspace,fingerprint,DateTimeOffset.UtcNow));
    }
    private async Task<EdgeItem> Add(EdgeItem item) { var saved=await store.AddAsync(item); Changed?.Invoke(); ActivityLog.Record("Pinned content to Brainiac Edge"); return saved; }
    public async Task<EdgeItem> PinImageAsync(BitmapSource image, string kind="Screenshot")
    {
        if((long)image.PixelWidth*image.PixelHeight>32_000_000) throw new InvalidDataException("Edge images must be under 32 megapixels.");
        var frozen=image.Clone(); frozen.Freeze(); var workspace=WorkspaceId;
        await imageGate.WaitAsync(); string? created=null;
        try
        {
            var fingerprint=await Task.Run(()=>PixelFingerprint(frozen));
            var existing=(await store.SearchAsync("",workspace)).FirstOrDefault(x=>x.Fingerprint==fingerprint);
            if(existing is not null) return existing;
            var folder=Path.Combine(LocalPaths.Data,"Edge","Images"); Directory.CreateDirectory(folder); created=Path.Combine(folder,Guid.NewGuid().ToString("N")+".png");
            await Task.Run(()=> { var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(frozen)); using var output=new FileStream(created,FileMode.CreateNew); encoder.Save(output); });
            var saved=await Add(new(Guid.NewGuid().ToString(),kind,"Capture " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),"",created,workspace,fingerprint,DateTimeOffset.UtcNow));
            if(saved.FilePath!=created) File.Delete(created);
            return saved;
        }
        catch { if(created is not null && File.Exists(created)) File.Delete(created); throw; }
        finally { imageGate.Release(); }
    }
    public static BitmapSource LoadImage(string path, int width=0)
    {
        using var stream=File.OpenRead(path); var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=stream; if(width>0) image.DecodePixelWidth=width; image.EndInit();
        if((long)image.PixelWidth*image.PixelHeight>32_000_000) throw new InvalidDataException("Image exceeds 32 megapixels."); image.Freeze(); return image;
    }
    private static string PixelFingerprint(BitmapSource source)
    {
        var converted=new System.Windows.Media.Imaging.FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Bgra32,null,0); var stride=checked(converted.PixelWidth*4); var pixels=new byte[checked(stride*converted.PixelHeight)]; converted.CopyPixels(pixels,stride,0);
        using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256); hash.AppendData(BitConverter.GetBytes(converted.PixelWidth)); hash.AppendData(BitConverter.GetBytes(converted.PixelHeight)); hash.AppendData(pixels); return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static Task<string> ImageFingerprintAsync(string path) => Task.Run(()=>PixelFingerprint(LoadImage(path)));
    public async Task PinShelfAsync(ShelfItem item)
    {
        if(item.Payload is BitmapSource image) await PinImageAsync(image,"Image");
        else if(item.Payload is string[] paths) foreach(var path in paths) await PinFileAsync(path);
        else await PinTextAsync(item.Text);
    }
    public void Report(Exception ex) => Error?.Invoke(ex.Message);
    public static void Copy(EdgeItem item)
    {
        if(item.Kind is "Image" or "Screenshot") Clipboard.SetImage(LoadImage(item.FilePath ?? throw new FileNotFoundException()));
        else if(item.FilePath is not null) { if(!Path.Exists(item.FilePath)) throw new FileNotFoundException("The original file is missing."); var paths=new System.Collections.Specialized.StringCollection(); paths.Add(item.FilePath); Clipboard.SetFileDropList(paths); }
        else Clipboard.SetText(item.Content);
    }
}
