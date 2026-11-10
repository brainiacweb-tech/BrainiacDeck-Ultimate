using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace BrainiacDesk.App;
public sealed class ImagePreviewWindow : ToolWindow
{
    public ImagePreviewWindow(BitmapSource image) : base("Export Preview")
    {
        Note($"{image.PixelWidth} × {image.PixelHeight} export pixels. This preview uses your selected framing and output size.");
        Body.Children.Add(new Border { Background = Brushes.Black, Padding = new Thickness(12), Child = new Image { Source = image, Height = 420, Stretch = Stretch.Uniform } });
    }
}
