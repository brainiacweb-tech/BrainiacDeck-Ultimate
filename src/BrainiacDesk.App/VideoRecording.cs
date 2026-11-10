using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Drawing = System.Drawing;
namespace BrainiacDesk.App;
public sealed class VideoRecording : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private volatile bool paused;
    private long ticks;
    private int frames;
    public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref ticks));
    public int Frames => Volatile.Read(ref frames);
    public bool Paused => paused;
    public bool Stopping => stop.IsCancellationRequested;
    public void TogglePause() => paused = !paused;
    public void Stop() => stop.Cancel();
    public Task RunAsync(Drawing.Rectangle area, string output, CapturePreset preset, bool fill, bool cursor, int fps, Func<Drawing.Bitmap>? frameProvider = null)
    {
        return Task.Factory.StartNew(() =>
        {
            var width = preset.Width == 0 ? area.Width & ~1 : preset.Width;
            var height = preset.Height == 0 ? area.Height & ~1 : preset.Height;
            using var writer = new NativeVideoWriter(output, width, height, fps, width * height >= 1920 * 1080 ? 12_000_000 : 6_000_000);
            using var scaled = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppRgb);
            var data = new byte[width * height * 4]; var clock = Stopwatch.StartNew(); long last = -1;
            while (!stop.IsCancellationRequested)
            {
                if (paused) { clock.Stop(); stop.Token.WaitHandle.WaitOne(40); continue; }
                if (!clock.IsRunning) clock.Start();
                var time = Math.Max(last + 1, clock.Elapsed.Ticks); var duration = TimeSpan.TicksPerSecond / fps;
                using var source = frameProvider is null ? CaptureImaging.Grab(area, cursor) : frameProvider();
                CaptureImaging.DrawVideoFrame(source, scaled, null, fill); CopyPixels(scaled, data);
                writer.Append(data, time, duration); last = time; Interlocked.Increment(ref frames); Interlocked.Exchange(ref ticks, clock.Elapsed.Ticks);
                if (clock.Elapsed > TimeSpan.FromHours(2)) break;
                var wait = duration - (clock.Elapsed.Ticks - time);
                if (wait > 0) stop.Token.WaitHandle.WaitOne(TimeSpan.FromTicks(wait));
            }
            if (frames == 0) throw new InvalidOperationException("Recording stopped before any frames were captured.");
            writer.Complete();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
    public static void CopyPixels(Drawing.Bitmap bitmap, byte[] destination)
    {
        var rectangle = new Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bits = bitmap.LockBits(rectangle, Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppRgb);
        try { for (int row = 0; row < bitmap.Height; row++) Marshal.Copy(IntPtr.Add(bits.Scan0, row * bits.Stride), destination, row * bitmap.Width * 4, bitmap.Width * 4); }
        finally { bitmap.UnlockBits(bits); }
    }
    public void Dispose() => stop.Dispose();
}