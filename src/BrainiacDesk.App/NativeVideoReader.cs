using System;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public sealed record VideoFrame(Drawing.Bitmap Bitmap, long Time, long Duration) : IDisposable { public void Dispose() => Bitmap.Dispose(); }
public sealed class NativeVideoReader : IDisposable
{
    private IMFSourceReader? reader;
    private bool startup, uninitialize;
    private int stride, bufferHeight, offsetX, offsetY;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int FrameRate { get; private set; }
    public double DurationSeconds { get; private set; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref SharpGen.Runtime.Win32.Variant value);
    public NativeVideoReader(string path)
    {
        try
        {
            var initialized = CoInitializeEx(IntPtr.Zero, 0); uninitialize = initialized >= 0;
            if (initialized < 0 && initialized != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(initialized);
            MediaFactory.MFStartup().CheckError(); startup = true;
            using var attributes = MediaFactory.MFCreateAttributes(1); attributes.Set(SourceReaderAttributeKeys.EnableVideoProcessing, 1u).CheckError();
            reader = MediaFactory.MFCreateSourceReaderFromURL(path, attributes);
            var duration = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource,new Guid("6C990D33-BB8E-477A-8598-0D5D96FCD88A"));
            try { DurationSeconds = Convert.ToDouble(duration.Value,System.Globalization.CultureInfo.InvariantCulture) / TimeSpan.TicksPerSecond; } finally { PropVariantClear(ref duration); }
            reader.SetStreamSelection(SourceReaderIndex.AllStreams, false); reader.SetStreamSelection(SourceReaderIndex.FirstVideoStream, true);
            using var requested = MediaFactory.MFCreateMediaType(); requested.Set(MediaTypeAttributeKeys.MajorType, new Guid("73646976-0000-0010-8000-00AA00389B71")).CheckError(); requested.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32).CheckError();
            reader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, requested);
            ReadFormat();
        }
        catch { Dispose(); throw; }
    }
    private void ReadFormat()
    {
            using var type = reader!.GetCurrentMediaType(SourceReaderIndex.FirstVideoStream);
            MediaFactory.MFGetAttributeSize(type, MediaTypeAttributeKeys.FrameSize, out var width, out var height).CheckError();
            int displayWidth=checked((int)width), displayHeight=checked((int)height), x=0, y=0;
            if(width < 1 || height < 1 || width > 3840 || height > 3840) throw new InvalidDataException("Video editor supports frames up to 3840 pixels per side.");
            bool aperture=false;
            foreach(var key in new[] { MediaTypeAttributeKeys.MinimumDisplayAperture, MediaTypeAttributeKeys.GeometricAperture })
            {
                if(type.GetBlobSize(key,out var length).Failure || length < 16 || length > 1024) continue;
                var bytes=type.GetBlob(key); int ax=BitConverter.ToInt16(bytes,2), ay=BitConverter.ToInt16(bytes,6), aw=BitConverter.ToInt32(bytes,8), ah=BitConverter.ToInt32(bytes,12);
                if(ax < 0 || ay < 0 || aw < 1 || ah < 1 || (long)ax+aw > width || (long)ay+ah > height) continue;
                x=ax; y=ay; displayWidth=aw; displayHeight=ah; aperture=true; break;
            }
            // H.264 decoders may expose macroblock padding at the first sample (1080 -> 1088).
            // Keep the presentation aperture; never stretch padded rows into the user's image.
            if(!aperture && Width > 0 && width >= Width && width-Width < 16 && height >= Height && height-Height < 16) { displayWidth=Width; displayHeight=Height; }
            if(Width > 0 && (Width != displayWidth || Height != displayHeight)) throw new InvalidDataException("Video changes presentation dimensions mid-stream.");
            Width=displayWidth; Height=displayHeight; offsetX=x; offsetY=y; bufferHeight=(int)height;
            var rawStride=MediaFactory.MFGetAttributeUInt32(type,MediaTypeAttributeKeys.DefaultStride,width*4); stride=unchecked((int)rawStride);
            if(Math.Abs((long)stride) < width*4 || Math.Abs((long)stride) > width*4+65536) throw new InvalidDataException("Unsupported decoded video stride.");
            MediaFactory.MFGetAttributeRatio(type, MediaTypeAttributeKeys.FrameRate, out var numerator, out var denominator);
            FrameRate = denominator == 0 ? 30 : Math.Clamp((int)Math.Round((double)numerator / denominator), 1, 60);
    }
    public void Seek(long time) => reader!.SetCurrentPosition(time);
    public VideoFrame? Read()
    {
        while (true)
        {
            using var sample = reader!.ReadSample(SourceReaderIndex.FirstVideoStream, (SourceReaderControlFlag)0, out _, out var flags, out var time);
            if ((flags & SourceReaderFlag.Error) != 0) throw new InvalidDataException("Windows could not decode the video.");
            if ((flags & SourceReaderFlag.EndOfStream) != 0) return null;
            if ((flags & SourceReaderFlag.CurrentMediaTypeChanged) != 0) ReadFormat();
            if (sample is null) continue;
            using var buffer = sample.ConvertToContiguousBuffer(); buffer.Lock(out var pointer, out _, out var length);
            var bitmap = new Drawing.Bitmap(Width, Height, Drawing.Imaging.PixelFormat.Format32bppRgb);
            try
            {
                if (length < Math.Abs((long)stride) * bufferHeight) throw new InvalidDataException("Decoded frame buffer is incomplete.");
                var pixels = new byte[Width * 4]; var bits = bitmap.LockBits(new Drawing.Rectangle(0, 0, Width, Height), Drawing.Imaging.ImageLockMode.WriteOnly, Drawing.Imaging.PixelFormat.Format32bppRgb);
                try { for (int row = 0; row < Height; row++) { var offset = (stride < 0 ? (bufferHeight - 1 - offsetY - row) * -stride : (offsetY + row) * stride) + offsetX * 4; Marshal.Copy(IntPtr.Add(pointer, offset), pixels, 0, pixels.Length); Marshal.Copy(pixels, 0, IntPtr.Add(bits.Scan0, row * bits.Stride), pixels.Length); } }
                finally { bitmap.UnlockBits(bits); }
            }
            catch { bitmap.Dispose(); throw; }
            finally { buffer.Unlock(); }
            long duration; try { duration = sample.SampleDuration; } catch { duration = TimeSpan.TicksPerSecond / FrameRate; }
            return new(bitmap, time, Math.Max(1, duration));
        }
    }
    public void Dispose() { try { reader?.Dispose(); reader = null; } finally { if (startup) { MediaFactory.MFShutdown(); startup = false; } if (uninitialize) { CoUninitialize(); uninitialize = false; } } }
}