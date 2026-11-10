using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BrainiacDesk.App;
using Drawing = System.Drawing;
internal static class CaptureStudioTests
{
    private sealed class InlineProgress(Action<double> report) : IProgress<double> { public void Report(double value) => report(value); }
    public static void Run(string folder, Action<bool,string> check, Action<Action,string> fails)
    {
        var bitmap = BitmapSource.Create(320, 180, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 320 * 180 * 4).ToArray(), 320 * 4);
        foreach (var preset in CapturePreset.All.Skip(1))
        {
            var resized = CaptureImaging.Resize(bitmap, preset, false); check(resized.PixelWidth == preset.Width && resized.PixelHeight == preset.Height, "HD/social image output: " + preset.Name);
        }
        var portrait = CaptureImaging.Resize(bitmap, CapturePreset.All[4], true); check(portrait.PixelWidth == 1080 && portrait.PixelHeight == 1920, "Fill-and-crop portrait export");
        check(CaptureGeometry.MapSelection(new System.Windows.Rect(100,50,400,200),new System.Windows.Size(1920,540),new Drawing.Rectangle(-1920,0,3840,1080)) == new Drawing.Rectangle(-1720,100,800,400),"Region selection maps scaling and negative monitor coordinates");
        check(CaptureGeometry.MapSelection(new System.Windows.Rect(-20,-10,40,30),new System.Windows.Size(100,100),new Drawing.Rectangle(0,0,100,100)) == new Drawing.Rectangle(0,0,20,20),"Region selection clips out-of-bounds drag accurately");
        check(VideoEditing.ZoomCrop(new Drawing.Rectangle(100,50,400,200),2) == new Drawing.Rectangle(200,100,200,100),"Video zoom crops around selected center");
        var path = Path.Combine(folder, "native-recording.mp4"); const int width = 160, height = 96; var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) { var offset = (y * width + x) * 4; pixels[offset] = y < height / 2 ? (byte)0 : (byte)255; pixels[offset + 2] = y < height / 2 ? (byte)255 : (byte)0; pixels[offset + 3] = 255; }
        using (var writer = new NativeVideoWriter(path, width, height, 30)) { for(int i=0;i<30;i++) writer.Append(pixels, i * TimeSpan.TicksPerSecond / 30, TimeSpan.TicksPerSecond / 30); writer.Complete(); }
        check(new FileInfo(path).Length > 1000, "Native H.264 MP4 output created");
        int frames = 0; long last = 0;
        using (var reader = new NativeVideoReader(path))
        {
            check(reader.DurationSeconds > .9 && reader.DurationSeconds < 1.1, "Video duration metadata");
            check(reader.Width == width && reader.Height == height, "MP4 decoded dimensions match encoder");
            while (true) { using var frame = reader.Read(); if (frame is null) break; frames++; last = frame.Time + frame.Duration; if(frames == 1) { var top = frame.Bitmap.GetPixel(20, 10); var bottom = frame.Bitmap.GetPixel(20,height-10); check(top.R > top.B + 100 && bottom.B > bottom.R + 100, "Video colors and vertical orientation preserved"); } }
        }
        check(frames == 30 && last >= TimeSpan.TicksPerSecond * 9 / 10, "MP4 frame count and duration");
        fails(() => new NativeVideoWriter(path, width, height), "Recording preserves existing output");
        fails(() => new NativeVideoWriter(Path.Combine(folder,"odd.mp4"), 161,96), "Invalid video dimensions rejected");
        var clip = Path.Combine(folder,"edited.mp4");
        VideoEditing.Export(path, clip, .2, .7, new Drawing.Rectangle(0,0,80,96), new("Test crop",80,96), false, null, CancellationToken.None);
        using (var reader = new NativeVideoReader(clip)) { check(reader.Width == 80 && reader.Height == 96, "Video crop/zoom export dimensions"); using var first = reader.Read(); check(first is not null && first.Time == 0, "Trimmed video timeline starts at zero"); }
        foreach(var preset in CapturePreset.All.Skip(1))
        {
            var hd=Path.Combine(folder,"hd-"+preset.Width+"-"+preset.Height+".mp4"); VideoEditing.Export(path,hd,0,.12,null,preset,false,null,CancellationToken.None);
            using var reader=new NativeVideoReader(hd); using var frame=reader.Read(); check(frame is not null && reader.Width == preset.Width && reader.Height == preset.Height,"Native MP4 preset: "+preset.Name);
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var cancelled = Path.Combine(folder,"cancel-video.mp4");
        fails(() => VideoEditing.Export(path,cancelled,0,1,null,CapturePreset.All[1],false,null,cancellation.Token), "Video export cancellation reported"); check(!File.Exists(cancelled), "Cancelled video export leaves no output");
        using var middleCancellation=new CancellationTokenSource(); var midOutput=Path.Combine(folder,"mid-cancel.mp4");
        fails(() => VideoEditing.Export(path,midOutput,0,1,null,new("Small",160,96),false,new InlineProgress(_ => middleCancellation.Cancel()),middleCancellation.Token),"Cancellation during active video encoding reported");
        check(!File.Exists(midOutput),"Mid-export cancellation removes partial output");
        fails(() => VideoEditing.Export(path,Path.Combine(folder,"bad-range.mp4"),1,0,null,CapturePreset.All[1],false,null,CancellationToken.None), "Invalid trim rejected");
        using var recording = new VideoRecording();
        var recorded = Path.Combine(folder,"timed.mp4"); var job = recording.RunAsync(new(0,0,160,96),recorded,new("Test",160,96),false,false,15,() => { var frame = new Drawing.Bitmap(160,96); using var g = Drawing.Graphics.FromImage(frame); g.Clear(Drawing.Color.Teal); return frame; });
        var deadline = DateTime.UtcNow.AddSeconds(15); while(recording.Frames < 3 && !job.IsCompleted && DateTime.UtcNow < deadline) Thread.Sleep(20);
        recording.TogglePause(); Thread.Sleep(200); var pausedFrames=recording.Frames; Thread.Sleep(200);
        check(recording.Paused && recording.Frames == pausedFrames,"Paused recording stops frame acquisition");
        recording.TogglePause(); deadline=DateTime.UtcNow.AddSeconds(15); while(recording.Frames == pausedFrames && !job.IsCompleted && DateTime.UtcNow < deadline) Thread.Sleep(20);
        check(!recording.Paused && recording.Frames > pausedFrames,"Recording resumes after pause");
        recording.Stop(); job.GetAwaiter().GetResult(); check(recording.Frames >= 3 && File.Exists(recorded), "Timed recording start/stop finalizes MP4");
        check(!Directory.EnumerateFiles(folder,"*.partial.mp4").Any(), "Video temporary files cleaned");
    }
}
