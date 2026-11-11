using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
namespace BrainiacDesk.App;
/// <summary>Windows language-pack OCR. No network, cloud credentials or external process.</summary>
public static class LocalOcr
{
    public static async Task<string> RecognizeAsync(string path)
    {
        var engine=OcrEngine.TryCreateFromUserProfileLanguages() ?? throw new InvalidOperationException("Install an OCR-capable Windows language pack in Windows Settings first.");
        var file=await StorageFile.GetFileFromPathAsync(Path.GetFullPath(path));
        using var stream=await file.OpenAsync(FileAccessMode.Read);
        var decoder=await BitmapDecoder.CreateAsync(stream);
        var scale=Math.Min(1,Math.Min((double)OcrEngine.MaxImageDimension/decoder.PixelWidth,(double)OcrEngine.MaxImageDimension/decoder.PixelHeight));
        var transform=new BitmapTransform { ScaledWidth=(uint)Math.Max(1,decoder.PixelWidth*scale),ScaledHeight=(uint)Math.Max(1,decoder.PixelHeight*scale) };
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,transform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage);
        var result=await engine.RecognizeAsync(bitmap); return result.Text;
    }
}
