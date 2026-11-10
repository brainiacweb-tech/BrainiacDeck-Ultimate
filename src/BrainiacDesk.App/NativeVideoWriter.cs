using System;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.MediaFoundation;
namespace BrainiacDesk.App;
public sealed class NativeVideoWriter : IDisposable
{
    private IMFSinkWriter? writer;
    private readonly string output;
    private readonly string temporary;
    private int stream;
    private bool startup, uninitialize, completed;
    private int frames;
    private long previous = -1;
    public int Width { get; }
    public int Height { get; }
    public int FrameRate { get; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    public NativeVideoWriter(string path, int width, int height, int frameRate = 30, int bitrate = 8_000_000)
    {
        if (width < 2 || height < 2 || width % 2 != 0 || height % 2 != 0 || width > 3840 || height > 3840 || frameRate is < 1 or > 60) throw new ArgumentException("Video dimensions must be even, 2–3840 pixels, with 1–60 fps.");
        output = Path.GetFullPath(path); temporary = output + "." + Guid.NewGuid().ToString("N") + ".partial.mp4";
        if (File.Exists(output)) throw new IOException("Output already exists. Choose a new filename.");
        Width = width; Height = height; FrameRate = frameRate;
        try
        {
            var initialized = CoInitializeEx(IntPtr.Zero, 0); uninitialize = initialized >= 0;
            if (initialized < 0 && initialized != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(initialized);
            MediaFactory.MFStartup().CheckError(); startup = true;
            writer = MediaFactory.MFCreateSinkWriterFromURL(temporary, null, null);
            using var encoded = MediaFactory.MFCreateMediaType();
            encoded.Set(MediaTypeAttributeKeys.MajorType, new Guid("73646976-0000-0010-8000-00AA00389B71")).CheckError();
            encoded.Set(MediaTypeAttributeKeys.Subtype, new Guid("34363248-0000-0010-8000-00AA00389B71")).CheckError();
            encoded.Set(MediaTypeAttributeKeys.AvgBitrate, (uint)bitrate).CheckError();
            Configure(encoded, width, height, frameRate);
            stream = writer.AddStream(encoded);
            using var input = MediaFactory.MFCreateMediaType();
            input.Set(MediaTypeAttributeKeys.MajorType, new Guid("73646976-0000-0010-8000-00AA00389B71")).CheckError();
            input.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32).CheckError(); Configure(input, width, height, frameRate);
            input.Set(MediaTypeAttributeKeys.DefaultStride, (uint)(width * 4)).CheckError();
            writer.SetInputMediaType(stream, input, null); writer.BeginWriting();
        }
        catch { Dispose(); throw; }
    }
    private static void Configure(IMFMediaType type, int width, int height, int fps)
    {
        type.Set(MediaTypeAttributeKeys.InterlaceMode, 2u).CheckError();
        MediaFactory.MFSetAttributeSize(type, MediaTypeAttributeKeys.FrameSize, (uint)width, (uint)height).CheckError();
        MediaFactory.MFSetAttributeRatio(type, MediaTypeAttributeKeys.FrameRate, (uint)fps, 1).CheckError();
        MediaFactory.MFSetAttributeRatio(type, MediaTypeAttributeKeys.PixelAspectRatio, 1, 1).CheckError();
    }
    public void Append(byte[] pixels, long time, long duration)
    {
        if (writer is null || completed) throw new ObjectDisposedException(nameof(NativeVideoWriter));
        if (pixels.Length != Width * Height * 4 || time <= previous || duration <= 0) throw new ArgumentException("Invalid frame buffer or timestamp.");
        using var buffer = MediaFactory.MFCreateMemoryBuffer(pixels.Length);
        buffer.Lock(out var pointer, out _, out _); try { Marshal.Copy(pixels, 0, pointer, pixels.Length); } finally { buffer.Unlock(); }
        buffer.CurrentLength = pixels.Length;
        using var sample = MediaFactory.MFCreateSample(); sample.AddBuffer(buffer); sample.SampleTime = time; sample.SampleDuration = duration;
        writer.WriteSample(stream, sample); previous = time; frames++;
    }
    public void Complete()
    {
        if (writer is null || completed || frames == 0) throw new InvalidOperationException("No recording frames were written.");
        writer.Finalize(); writer.Dispose(); writer = null; File.Move(temporary, output, false); completed = true;
    }
    public void Dispose()
    {
        try { writer?.Dispose(); writer = null; if (!completed && File.Exists(temporary)) File.Delete(temporary); }
        finally { if (startup) { MediaFactory.MFShutdown(); startup = false; } if (uninitialize) { CoUninitialize(); uninitialize = false; } }
    }
}