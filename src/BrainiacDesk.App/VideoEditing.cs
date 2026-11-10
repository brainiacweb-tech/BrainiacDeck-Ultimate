using System;
using System.Threading;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public static class VideoEditing
{
    public static Drawing.Rectangle ZoomCrop(Drawing.Rectangle area, double zoom)
    {
        if(area.Width < 2 || area.Height < 2 || !double.IsFinite(zoom) || zoom < 1 || zoom > 4) throw new ArgumentException("Choose a valid crop and zoom between 1× and 4×.");
        int width=Math.Max(2,(int)(area.Width/zoom)), height=Math.Max(2,(int)(area.Height/zoom)); return new(area.X+(area.Width-width)/2,area.Y+(area.Height-height)/2,width,height);
    }
    public static void Export(string input, string output, double startSeconds, double endSeconds, Drawing.Rectangle? crop, CapturePreset preset, bool fill, IProgress<double>? progress, CancellationToken token)
    {
        if (!double.IsFinite(startSeconds) || !double.IsFinite(endSeconds) || startSeconds < 0 || endSeconds <= startSeconds || endSeconds > 7200) throw new ArgumentException("Choose a trim range between 0 and 7200 seconds, with end after start.");
        token.ThrowIfCancellationRequested();
        using var reader = new NativeVideoReader(input);
        var width = preset.Width == 0 ? (crop?.Width ?? reader.Width) & ~1 : preset.Width;
        var height = preset.Height == 0 ? (crop?.Height ?? reader.Height) & ~1 : preset.Height;
        using var writer = new NativeVideoWriter(output, width, height, reader.FrameRate, 12_000_000);
        using var resized = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppRgb);
        var data = new byte[width * height * 4]; long first = -1, last = -1;
        var start = (long)(startSeconds * TimeSpan.TicksPerSecond); var end = (long)(endSeconds * TimeSpan.TicksPerSecond);
        if (start > 0) reader.Seek(start);
        while (true)
        {
            token.ThrowIfCancellationRequested(); using var frame = reader.Read(); if (frame is null) break;
            if (frame.Time < start) continue; if (frame.Time >= end) break;
            CaptureImaging.DrawVideoFrame(frame.Bitmap, resized, crop, fill); VideoRecording.CopyPixels(resized, data);
            if (first < 0) first = frame.Time;
            var timestamp = frame.Time - first; if (timestamp <= last) continue;
            writer.Append(data, timestamp, Math.Min(frame.Duration, end - frame.Time)); last = timestamp;
            progress?.Report(Math.Clamp((double)(frame.Time - start) / (end - start), 0, 1));
        }
        token.ThrowIfCancellationRequested(); writer.Complete(); progress?.Report(1);
    }
}