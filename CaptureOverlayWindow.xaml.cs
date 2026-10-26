using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Controls;
using EdgeShelf.Services;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;

namespace EdgeShelf;

public partial class CaptureOverlayWindow : Window
{
    private WpfPoint _startPoint;
    private bool _selecting;

    public Rect? SelectedScreenRect { get; private set; }

    public CaptureOverlayWindow()
    {
        InitializeComponent();
        HintText.Text = AppLanguage.Text("captureHint");
        var bounds = Forms.SystemInformation.VirtualScreen;
        Left = bounds.Left;
        Top = bounds.Top;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(OverlayCanvas);
        _selecting = true;
        SelectionRectangle.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_startPoint, _startPoint);
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_selecting)
        {
            UpdateSelection(_startPoint, e.GetPosition(OverlayCanvas));
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        ReleaseMouseCapture();
        var endPoint = e.GetPosition(OverlayCanvas);
        var local = MakeRect(_startPoint, endPoint);
        if (local.Width < 4 || local.Height < 4)
        {
            return;
        }

        SelectedScreenRect = new Rect(Left + local.X, Top + local.Y, local.Width, local.Height);
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }

    private void UpdateSelection(WpfPoint first, WpfPoint second)
    {
        var rect = MakeRect(first, second);
        Canvas.SetLeft(SelectionRectangle, rect.Left);
        Canvas.SetTop(SelectionRectangle, rect.Top);
        SelectionRectangle.Width = rect.Width;
        SelectionRectangle.Height = rect.Height;
    }

    private static Rect MakeRect(WpfPoint first, WpfPoint second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(first.X - second.X),
        Math.Abs(first.Y - second.Y));
}
