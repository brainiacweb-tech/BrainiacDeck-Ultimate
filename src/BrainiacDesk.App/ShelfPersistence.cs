using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
namespace BrainiacDesk.App;
public sealed record ShelfPreference(bool Persist=false,string Exclusions="password,secret,token,api_key");
public sealed record SavedShelfItem(string Kind,string Text,string? Image,string[]? Files,DateTime Created,bool Pinned);
public static class ShelfPersistence
{
    public static void Save(ShelfItem[] items)
    {
        var saved=items.Select(item=>
        {
            string? image=null;
            if(item.Payload is BitmapSource bitmap) { using var stream=new MemoryStream(); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream); if(stream.Length>8_000_000) throw new InvalidDataException("Image exceeds the 8 MB protected shelf limit."); image=Convert.ToBase64String(stream.ToArray()); }
            return new SavedShelfItem(item.Kind,item.Text,image,item.Payload as string[],item.Created,item.Pinned);
        }).ToArray();
        if(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(saved).Length>24_000_000) throw new InvalidDataException("Protected shelf limit is 24 MB. Remove large items before saving.");
        ProtectedLocalData.Save("shelf.protected.json",saved);
    }
    public static ShelfItem[] Load()
    {
        return ProtectedLocalData.Load("shelf.protected.json",Array.Empty<SavedShelfItem>()).Where(x=>x.Pinned || DateTime.UtcNow-x.Created<TimeSpan.FromHours(1)).Take(50).Select(item=>
        {
            object payload=item.Text;
            if(item.Kind=="Image") { var bytes=Convert.FromBase64String(item.Image ?? throw new InvalidDataException("Saved shelf image is missing.")); using var stream=new MemoryStream(bytes); var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0]; if((long)frame.PixelWidth*frame.PixelHeight>8_000_000) throw new InvalidDataException("Saved shelf image exceeds pixel limit."); frame.Freeze(); payload=frame; }
            else if(item.Kind=="Files") payload=item.Files ?? throw new InvalidDataException("Saved file references are missing.");
            return new ShelfItem(item.Kind,item.Text,payload,item.Created,item.Pinned);
        }).ToArray();
    }
}