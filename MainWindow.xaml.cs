using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using EdgeShelf.Models;
using EdgeShelf.Services;
using Forms = System.Windows.Forms;
using WpfPoint = System.Windows.Point;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDataObject = System.Windows.DataObject;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfContextMenu = System.Windows.Controls.ContextMenu;

namespace EdgeShelf;

public partial class MainWindow : Window
{
    private const int WmClipboardUpdate = 0x031D;
    private const string InternalDragFormat = "EdgeShelf/InternalDrag";
    private readonly ShelfStore _store = new();
    private readonly SettingsService _settingsStore = new();
    private readonly EdgeShelfSettings _settings;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _trayShowItem;
    private readonly Forms.ToolStripMenuItem _trayRetentionItem;
    private readonly Forms.ToolStripMenuItem _trayStartupItem;
    private readonly Forms.ToolStripMenuItem _trayExitItem;
    private readonly Forms.ToolStripMenuItem _trayLanguageItem;
    private readonly Forms.ToolStripMenuItem _trayChineseItem;
    private readonly Forms.ToolStripMenuItem _trayEnglishItem;
    private readonly System.Windows.Threading.DispatcherTimer _stateTimer;
    private readonly System.Windows.Threading.DispatcherTimer _collapseTimer;
    private readonly System.Windows.Threading.DispatcherTimer _captureRevealTimer;
    private const double ExpandedWidth = 360;
    private const double HandleWidth = 24;
    private const double EdgeTriggerWidth = 4;
    private WpfPoint _dragStartPoint;
    private bool _isDragging;
    private bool _captureRevealActive;
    private bool _externalDragActive;
    private bool _dragExpandStarted;
    private bool _marqueeSelecting;
    private bool _marqueePending;
    private bool _marqueeAdditive;
    private bool _fileDragRequested;
    private List<ShelfItem> _dragSelection = [];
    private List<ShelfItem> _lastMultiSelection = [];
    private readonly HashSet<ShelfItem> _marqueePreservedSelection = [];
    private WpfPoint _marqueeStartPoint;
    private HwndSource? _source;
    private uint _lastClipboardSequence;
    private string? _lastClipboardImageHash;
    private DateTimeOffset _lastClipboardImageAt;

    public ObservableCollection<ShelfItem> Items { get; }

    public MainWindow()
    {
        InitializeComponent();
        Items = _store.Load();
        _settings = _settingsStore.Load();
        AppLanguage.Set(_settings.Language ?? "zh-CN");
        DataContext = this;

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "EdgeShelf"
        };
        var menu = new Forms.ContextMenuStrip();
        _trayShowItem = new Forms.ToolStripMenuItem();
        _trayShowItem.Click += (_, _) => ShowShelf();
        menu.Items.Add(_trayShowItem);
        _trayRetentionItem = new Forms.ToolStripMenuItem();
        AddRetentionOption(_trayRetentionItem, "hour1", 1);
        AddRetentionOption(_trayRetentionItem, "hour24", 24);
        AddRetentionOption(_trayRetentionItem, "days7", 24 * 7);
        AddRetentionOption(_trayRetentionItem, "forever", 0);
        menu.Items.Add(_trayRetentionItem);
        _trayStartupItem = new Forms.ToolStripMenuItem { CheckOnClick = true, Checked = _settings.StartWithWindows };
        _trayStartupItem.CheckedChanged += (_, _) => SetStartWithWindows(_trayStartupItem.Checked);
        menu.Items.Add(_trayStartupItem);
        _trayLanguageItem = new Forms.ToolStripMenuItem();
        _trayChineseItem = new Forms.ToolStripMenuItem { Tag = "zh-CN" };
        _trayEnglishItem = new Forms.ToolStripMenuItem { Tag = "en-US" };
        _trayChineseItem.Click += (_, _) => ChangeLanguage("zh-CN");
        _trayEnglishItem.Click += (_, _) => ChangeLanguage("en-US");
        _trayLanguageItem.DropDownItems.AddRange([_trayChineseItem, _trayEnglishItem]);
        menu.Items.Add(_trayLanguageItem);
        _trayExitItem = new Forms.ToolStripMenuItem();
        _trayExitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(_trayExitItem);
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowShelf();

        _stateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _stateTimer.Tick += (_, _) => RefreshItems();
        _stateTimer.Start();
        _collapseTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            SetExpanded(false);
        };
        _captureRevealTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _captureRevealTimer.Tick += (_, _) =>
        {
            _captureRevealTimer.Stop();
            _captureRevealActive = false;
            if (!IsMouseOver && !_isDragging && !_externalDragActive)
            {
                SetExpanded(false);
            }
        };
        Loaded += (_, _) =>
        {
            ConfigureWindow();
            ApplyLanguage();
        };
        SourceInitialized += (_, _) => RegisterClipboardListener();
        ApplyLanguage();
    }

    public void DisposeResources()
    {
        _store.Save(Items);
        UnregisterClipboardListener();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _stateTimer.Stop();
        _collapseTimer.Stop();
        _captureRevealTimer.Stop();
    }

    private void ConfigureWindow()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width;
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2d);
        SetExpanded(false, animate: false);
    }

    private void RegisterClipboardListener()
    {
        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        _source.AddHook(WndProc);
        NativeMethods.AddClipboardFormatListener(_source.Handle);
    }

    private void UnregisterClipboardListener()
    {
        if (_source is not null)
        {
            NativeMethods.RemoveClipboardFormatListener(_source.Handle);
            _source.RemoveHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            QueueClipboardImageImport();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ShowShelf()
    {
        Show();
        SetExpanded(true);
        WindowState = WindowState.Normal;
        Topmost = false;
        Topmost = true;
        Activate();
        _captureRevealTimer.Stop();
        if (!IsMouseOver)
        {
            _captureRevealActive = true;
            _captureRevealTimer.Start();
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _collapseTimer.Stop();
        _captureRevealTimer.Stop();
        _captureRevealActive = false;
        if (ContentPanel.Visibility != Visibility.Visible)
        {
            RevealHandle();
        }
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging && !_captureRevealActive && !_externalDragActive)
        {
            _collapseTimer.Start();
        }
    }

    private void HandleButton_Click(object sender, RoutedEventArgs e) => SetExpanded(true);

    private void HandleSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ContentPanel.Visibility != Visibility.Visible)
        {
            if (Width <= EdgeTriggerWidth + 1)
            {
                RevealHandle();
            }
            else
            {
                SetExpanded(true);
            }
            e.Handled = true;
        }
    }

    private void RevealHandle()
    {
        var area = SystemParameters.WorkArea;
        _collapseTimer.Stop();
        BeginAnimation(WidthProperty, CreateBoundsAnimation(Width, HandleWidth, 180));
        BeginAnimation(LeftProperty, CreateBoundsAnimation(Left, area.Right - HandleWidth, 180));
        EdgeTrigger.Visibility = Visibility.Collapsed;
        HandleButton.Visibility = Visibility.Visible;
    }

    private void SetExpanded(bool expanded, bool animate = true)
    {
        var area = SystemParameters.WorkArea;
        var targetWidth = expanded ? ExpandedWidth : EdgeTriggerWidth;
        var targetLeft = area.Right - targetWidth;
        if (animate)
        {
            BeginAnimation(WidthProperty, CreateBoundsAnimation(Width, targetWidth, 240));
            BeginAnimation(LeftProperty, CreateBoundsAnimation(Left, targetLeft, 240));
        }
        else
        {
            Width = targetWidth;
            Left = targetLeft;
        }

        ContentPanel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        HandleButton.Width = HandleWidth;
        HandleButton.Visibility = Visibility.Collapsed;
        EdgeTrigger.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
    }

    private static System.Windows.Media.Animation.DoubleAnimation CreateBoundsAnimation(double from, double to, int durationMilliseconds) =>
        new(from, to, TimeSpan.FromMilliseconds(durationMilliseconds))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
            }
        };

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void ChangeLanguage(string language)
    {
        _settings.Language = language;
        _settingsStore.Save(_settings);
        AppLanguage.Set(language);
        foreach (var item in Items)
        {
            item.RefreshDerivedState();
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        SubtitleText.Text = AppLanguage.Text("subtitle");
        DropTitleText.Text = AppLanguage.Text("dropTitle");
        DropDescriptionText.Text = AppLanguage.Text("dropDescription");
        DeleteSelectedButton.Content = AppLanguage.Text("deleteSelected");
        HandleButton.ToolTip = AppLanguage.Text("handle");
        SelectionCountText.Text = AppLanguage.SelectionCount(ItemsList.SelectedItems.Count);

        foreach (var item in FindVisualAndLogicalChildren<FrameworkElement>(ItemsList))
        {
            if (item.Tag is string key && key is "pin" or "remove" or "invalid")
            {
                switch (item)
                {
                    case WpfMenuItem menuItem:
                        menuItem.Header = AppLanguage.Text(key);
                        break;
                    case TextBlock textBlock:
                        textBlock.Text = AppLanguage.Text(key);
                        break;
                }
            }
        }

        _trayIcon.Text = "EdgeShelf";
        _trayShowItem.Text = AppLanguage.Text("showShelf");
        _trayRetentionItem.Text = AppLanguage.Text("retention");
        _trayStartupItem.Text = AppLanguage.Text("startup");
        _trayLanguageItem.Text = AppLanguage.Text("language");
        _trayChineseItem.Text = AppLanguage.Text("chinese");
        _trayEnglishItem.Text = AppLanguage.Text("english");
        _trayChineseItem.Checked = !AppLanguage.IsEnglish;
        _trayEnglishItem.Checked = AppLanguage.IsEnglish;
        _trayExitItem.Text = AppLanguage.Text("exit");
        foreach (Forms.ToolStripMenuItem option in _trayRetentionItem.DropDownItems)
        {
            if (option.Tag is string key)
            {
                option.Text = AppLanguage.Text(key);
            }
        }
    }

    private static IEnumerable<T> FindVisualAndLogicalChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindVisualAndLogicalChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private void DropZone_DragOver(object sender, WpfDragEventArgs e)
    {
        var canAccept = TryGetDroppedPaths(e.Data, out _);
        if (canAccept)
        {
            _externalDragActive = true;
            _collapseTimer.Stop();
            _captureRevealTimer.Stop();
            if (!_dragExpandStarted)
            {
                _dragExpandStarted = true;
                SetExpanded(true);
            }
        }

        e.Effects = canAccept ? WpfDragDropEffects.Copy : WpfDragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, WpfDragEventArgs e)
    {
        if (!TryGetDroppedPaths(e.Data, out var paths))
        {
            e.Handled = true;
            return;
        }

        _externalDragActive = false;
        _dragExpandStarted = false;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            Items.Insert(0, new ShelfItem
            {
                Kind = ShelfItemKind.File,
                Path = path,
                DisplayName = System.IO.Path.GetFileName(path),
                CreatedUtc = DateTimeOffset.UtcNow
            });
        }

        _store.Save(Items);
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, WpfDragEventArgs e)
    {
        _externalDragActive = false;
        _dragExpandStarted = false;
        if (!_isDragging && !_captureRevealActive)
        {
            _collapseTimer.Start();
        }
    }

    private static bool TryGetDroppedPaths(System.Windows.IDataObject data, out string[] paths)
    {
        paths = [];
        if (data.GetDataPresent(InternalDragFormat))
        {
            return false;
        }

        if (data.GetDataPresent(WpfDataFormats.FileDrop))
        {
            if (data.GetData(WpfDataFormats.FileDrop) is string[] fileDrop)
            {
                paths = fileDrop;
                return paths.Length > 0;
            }

            if (data.GetData(WpfDataFormats.FileDrop) is System.Collections.IEnumerable values)
            {
                paths = values.OfType<string>().ToArray();
                return paths.Length > 0;
            }
        }

        if (data.GetDataPresent("FileNameW") && data.GetData("FileNameW") is string[] fileNames)
        {
            paths = fileNames;
            return paths.Length > 0;
        }

        if (data.GetDataPresent("FileName") && data.GetData("FileName") is string[] legacyNames)
        {
            paths = legacyNames;
            return paths.Length > 0;
        }

        return false;
    }

    private void Items_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        BeginPointerSelection(e.GetPosition(null), e.GetPosition(ItemsList), source);

        // When starting a drag from an already-selected item, ListBox's default
        // mouse handling would collapse the multi-selection to that one item.
        // Keep the whole selection visually active so all selected files drag together.
        if (_fileDragRequested && _dragSelection.Count > 1)
        {
            var itemContainer = source is null
                ? null
                : ItemsControl.ContainerFromElement(ItemsList, source) as ListBoxItem;
            if (itemContainer?.DataContext is ShelfItem clickedItem
                && ItemsList.SelectedItems.Contains(clickedItem))
            {
                e.Handled = true;
            }
        }
    }

    private void BeginPointerSelection(WpfPoint screenPoint, WpfPoint listPoint, DependencyObject? source)
    {
        _dragStartPoint = screenPoint;
        var itemContainer = source is null ? null : ItemsControl.ContainerFromElement(ItemsList, source) as ListBoxItem;
        _fileDragRequested = itemContainer is not null && IsItemCardHit(source, itemContainer);
        var currentSelection = ItemsList.SelectedItems.OfType<ShelfItem>().Where(item => item.Exists).ToList();
        var clickedItem = itemContainer?.DataContext as ShelfItem;
        if (_fileDragRequested && clickedItem is not null && clickedItem.Exists
            && _lastMultiSelection.Contains(clickedItem))
        {
            _dragSelection = _lastMultiSelection.Where(item => Items.Contains(item) && item.Exists).ToList();
        }
        else
        {
            _dragSelection = currentSelection;
            if (_fileDragRequested && clickedItem is not null && clickedItem.Exists && !_dragSelection.Contains(clickedItem))
            {
                _dragSelection.Add(clickedItem);
            }
        }
        _marqueePending = !_fileDragRequested;
        _marqueeAdditive = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        _marqueePreservedSelection.Clear();
        if (_marqueeAdditive)
        {
            foreach (var item in ItemsList.SelectedItems.OfType<ShelfItem>())
            {
                _marqueePreservedSelection.Add(item);
            }
        }

        _marqueeStartPoint = listPoint;
    }

    private bool TryStartPendingDrag(WpfPoint screenPoint, WpfPoint listPoint)
    {
        if ((!_marqueePending && !_fileDragRequested)
            || _isDragging
            || Math.Abs(screenPoint.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(screenPoint.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return false;
        }

        _marqueePending = false;
        if (_fileDragRequested)
        {
            StartFileDrag();
            return true;
        }

        _marqueeSelecting = true;
        MarqueeRectangle.Visibility = Visibility.Visible;
        MarqueeRectangle.Width = 0;
        MarqueeRectangle.Height = 0;
        ItemsList.CaptureMouse();
        if (!_marqueeAdditive)
        {
            ItemsList.SelectedItems.Clear();
        }

        UpdateMarqueeSelection(listPoint);
        return true;
    }

    private static bool IsItemCardHit(DependencyObject? source, ListBoxItem itemContainer)
    {
        while (source is not null && !ReferenceEquals(source, itemContainer))
        {
            if (source is Border { ContextMenu: not null })
            {
                return true;
            }

            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    private void Items_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_marqueeSelecting)
        {
            UpdateMarqueeSelection(e.GetPosition(ItemsList));
            e.Handled = true;
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && TryStartPendingDrag(e.GetPosition(null), e.GetPosition(ItemsList)))
        {
            e.Handled = true;
        }
    }

    private void DropHint_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        BeginPointerSelection(e.GetPosition(null), e.GetPosition(ItemsList), null);
        e.Handled = true;
    }

    private void DropHint_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && TryStartPendingDrag(e.GetPosition(null), e.GetPosition(ItemsList)))
        {
            e.Handled = true;
        }
    }

    private void DropHint_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _marqueePending = false;
        _fileDragRequested = false;
        _dragSelection.Clear();
        if (_marqueeSelecting)
        {
            _marqueeSelecting = false;
            ItemsList.ReleaseMouseCapture();
            MarqueeRectangle.Visibility = Visibility.Collapsed;
            _marqueePreservedSelection.Clear();
            e.Handled = true;
        }
    }

    private void StartFileDrag()
    {
        var selected = _dragSelection;

        if (selected.Count == 0)
        {
            return;
        }

        _isDragging = true;
        try
        {
            var data = new WpfDataObject();
            var fileDropList = new System.Collections.Specialized.StringCollection();
            fileDropList.AddRange(selected.Select(item => item.Path).ToArray());
            data.SetFileDropList(fileDropList);
            data.SetData(InternalDragFormat, true);
            DragDrop.DoDragDrop(this, data, WpfDragDropEffects.Copy);
        }
        finally
        {
            _isDragging = false;
        }
    }

    private void Items_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _marqueePending = false;
        _fileDragRequested = false;
        _dragSelection.Clear();
        _lastMultiSelection = ItemsList.SelectedItems.OfType<ShelfItem>().ToList();
        if (!_marqueeSelecting)
        {
            return;
        }

        _marqueeSelecting = false;
        ItemsList.ReleaseMouseCapture();
        MarqueeRectangle.Visibility = Visibility.Collapsed;
        _marqueePreservedSelection.Clear();
        e.Handled = true;
    }

    private void UpdateMarqueeSelection(WpfPoint currentPoint)
    {
        var selection = MakeRect(_marqueeStartPoint, currentPoint);
        Canvas.SetLeft(MarqueeRectangle, selection.Left);
        Canvas.SetTop(MarqueeRectangle, selection.Top);
        MarqueeRectangle.Width = selection.Width;
        MarqueeRectangle.Height = selection.Height;

        ItemsList.SelectedItems.Clear();
        if (_marqueeAdditive)
        {
            foreach (var item in _marqueePreservedSelection)
            {
                ItemsList.SelectedItems.Add(item);
            }
        }

        foreach (var item in Items)
        {
            if (ItemsList.ItemContainerGenerator.ContainerFromItem(item) is not ListBoxItem container || !container.IsVisible)
            {
                continue;
            }

            var topLeft = container.TranslatePoint(new WpfPoint(0, 0), ItemsList);
            var itemRect = new Rect(topLeft, new System.Windows.Size(container.ActualWidth, container.ActualHeight));
            if (selection.IntersectsWith(itemRect))
            {
                ItemsList.SelectedItems.Add(item);
            }
        }
    }

    private static Rect MakeRect(WpfPoint first, WpfPoint second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(first.X - second.X),
        Math.Abs(first.Y - second.Y));

    private void ItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = ItemsList.SelectedItems.Count;
        if (count > 1)
        {
            _lastMultiSelection = ItemsList.SelectedItems.OfType<ShelfItem>().ToList();
        }

        SelectionCountText.Text = AppLanguage.SelectionCount(count);
        DeleteSelectedButton.IsEnabled = count > 0;
    }

    private void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = ItemsList.SelectedItems.OfType<ShelfItem>().ToList();
        foreach (var item in selected)
        {
            Items.Remove(item);
            if (item.Kind == ShelfItemKind.Screenshot)
            {
                TryDelete(item.Path);
            }
        }

        _store.Save(Items);
    }

    private void QueueClipboardImageImport()
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await Task.Delay(250);
            var sequence = NativeMethods.GetClipboardSequenceNumber();
            if (sequence == _lastClipboardSequence)
            {
                return;
            }

            _lastClipboardSequence = sequence;
            try
            {
                if (System.Windows.Clipboard.GetImage() is not BitmapSource image)
                {
                    return;
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                byte[] pngBytes;
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    pngBytes = stream.ToArray();
                }

                var imageHash = Convert.ToHexString(SHA256.HashData(pngBytes));
                var now = DateTimeOffset.UtcNow;
                if (imageHash == _lastClipboardImageHash && now - _lastClipboardImageAt < TimeSpan.FromSeconds(2))
                {
                    return;
                }

                _lastClipboardImageHash = imageHash;
                _lastClipboardImageAt = now;
                var path = _store.CreateScreenshotPath();
                File.WriteAllBytes(path, pngBytes);

                Items.Insert(0, new ShelfItem
                {
                    Kind = ShelfItemKind.Screenshot,
                    Path = path,
                    DisplayName = System.IO.Path.GetFileName(path),
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = _settings.RetentionHours == 0 ? null : DateTimeOffset.UtcNow.AddHours(_settings.RetentionHours)
                });
                _store.Save(Items);
                ShowShelf();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
            }
            catch (IOException)
            {
            }
        }));
    }

    private void AddRetentionOption(Forms.ToolStripMenuItem parent, string label, int hours)
    {
        var option = new Forms.ToolStripMenuItem(AppLanguage.Text(label)) { Checked = _settings.RetentionHours == hours, Tag = label };
        option.Click += (_, _) =>
        {
            _settings.RetentionHours = hours;
            _settingsStore.Save(_settings);
            var now = DateTimeOffset.UtcNow;
            foreach (var item in Items.Where(item => item.Kind == ShelfItemKind.Screenshot && !item.IsPinned))
            {
                item.ExpiresUtc = hours == 0 ? null : now.AddHours(hours);
            }

            _store.Save(Items);
            foreach (Forms.ToolStripMenuItem sibling in parent.DropDownItems)
            {
                sibling.Checked = ReferenceEquals(sibling, option);
            }
        };
        parent.DropDownItems.Add(option);
    }

    private void SetStartWithWindows(bool enabled)
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true);
        if (enabled)
        {
            runKey.SetValue("EdgeShelf", $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            runKey.DeleteValue("EdgeShelf", throwOnMissingValue: false);
        }

        _settings.StartWithWindows = enabled;
        _settingsStore.Save(_settings);
    }

    private void Items_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is ShelfItem { Kind: ShelfItemKind.Screenshot, Exists: true } item)
        {
            var preview = new PreviewWindow(item.Path) { Owner = this };
            preview.ShowDialog();
        }
    }

    private void Item_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ShelfItem item)
        {
            ItemsList.SelectedItem = item;
            if (element.ContextMenu is { Items.Count: >= 2 } contextMenu
                && contextMenu.Items[0] is WpfMenuItem pinItem
                && contextMenu.Items[1] is WpfMenuItem removeItem)
            {
                pinItem.Header = AppLanguage.Text("pin");
                removeItem.Header = AppLanguage.Text("remove");
            }
        }
    }

    private void TogglePin_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item)
        {
            item.IsPinned = !item.IsPinned;
            if (item.IsPinned)
            {
                item.ExpiresUtc = null;
            }
            else if (item.Kind == ShelfItemKind.Screenshot && _settings.RetentionHours > 0)
            {
                item.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(_settings.RetentionHours);
            }

            _store.Save(Items);
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is not { } item)
        {
            return;
        }

        Items.Remove(item);
        if (item.Kind == ShelfItemKind.Screenshot)
        {
            TryDelete(item.Path);
        }

        _store.Save(Items);
    }

    private static ShelfItem? GetContextItem(object sender)
    {
        if (sender is not WpfMenuItem menuItem || menuItem.Parent is not WpfContextMenu contextMenu)
        {
            return null;
        }

        return (contextMenu.PlacementTarget as FrameworkElement)?.DataContext as ShelfItem;
    }

    private void RefreshItems()
    {
        foreach (var item in Items)
        {
            item.RefreshDerivedState();
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var item in Items.Where(item => item.Kind == ShelfItemKind.Screenshot && !item.IsPinned && item.ExpiresUtc <= now).ToList())
        {
            Items.Remove(item);
            TryDelete(item.Path);
        }

        _store.Save(Items);
    }

    private static void TryDelete(string path)
    {
        var cacheRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EdgeShelf", "cache")) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool AddClipboardFormatListener(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool RemoveClipboardFormatListener(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern uint GetClipboardSequenceNumber();
    }
}
