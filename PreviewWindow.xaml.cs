using System.Windows;
using System.Windows.Media.Imaging;
using EdgeShelf.Services;

namespace EdgeShelf;

public partial class PreviewWindow : Window
{
    public PreviewWindow(string imagePath)
    {
        InitializeComponent();
        Title = AppLanguage.Text("previewTitle");
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(imagePath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        PreviewImage.Source = image;
    }
}
