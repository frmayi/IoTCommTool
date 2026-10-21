using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using IoTCommTool.App.Services;
using IoTCommTool.App.ViewModels;

namespace IoTCommTool.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        try
        {
            Icon = CreateAppIcon();
        }
        catch
        {
            // A missing window icon should never prevent the application from starting.
        }

        ActualThemeVariantChanged += OnActualThemeVariantChanged;
        ThemeManager.ThemeChanged += OnThemeManagerThemeChanged;
        Opened += OnOpenedForTitleBarTheme;
        TabsControl.AddHandler(PointerPressedEvent, OnTabsPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(
        IntPtr hwnd,
        IntPtr updateRect,
        IntPtr updateRegion,
        uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwFrame = 0x0004;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint WmNcActivate = 0x0086;

    private void OnOpenedForTitleBarTheme(object? sender, EventArgs e) => ApplyNativeTitleBarTheme();

    private void OnTabsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(TabsControl).Properties.IsRightButtonPressed
            || DataContext is not MainWindowViewModel main)
            return;

        Point point = e.GetPosition(TabsControl);
        TabItem? tab = TabsControl.GetVisualsAt(point).OfType<TabItem>().FirstOrDefault();
        if (tab is null)
        {
            foreach (Visual visual in TabsControl.GetVisualsAt(point))
            {
                tab = visual.GetVisualAncestors().OfType<TabItem>().FirstOrDefault();
                if (tab is not null)
                    break;
            }
        }

        if (tab?.DataContext is not ConnectionViewModelBase vm)
            return;

        OpenTabMenu(vm, main, tab);
        e.Handled = true;
    }

    private void OnTabMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control target
            && target.DataContext is ConnectionViewModelBase vm
            && DataContext is MainWindowViewModel main)
            OpenTabMenu(vm, main, target);
    }

    private static void OpenTabMenu(ConnectionViewModelBase vm, MainWindowViewModel main, Control placementTarget)
    {
        int index = main.Tabs.IndexOf(vm);
        var menu = new ContextMenu
        {
            PlacementTarget = placementTarget,
            Placement = PlacementMode.Pointer,
        };
        menu.Items.Add(new MenuItem { Header = "关闭当前", Command = vm.CloseRequestCommand });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "关闭左侧全部",
            Command = vm.CloseTabsToLeftCommand,
            IsEnabled = index > 0,
        });
        menu.Items.Add(new MenuItem
        {
            Header = "关闭右侧全部",
            Command = vm.CloseTabsToRightCommand,
            IsEnabled = index >= 0 && index < main.Tabs.Count - 1,
        });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "全部关闭",
            Command = vm.CloseAllTabsCommand,
            IsEnabled = main.Tabs.Count > 0,
        });
        Dispatcher.UIThread.Post(() => menu.Open(placementTarget), DispatcherPriority.Background);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => ScheduleNativeTitleBarTheme();

    private void OnThemeManagerThemeChanged() => ScheduleNativeTitleBarTheme();

    private void ScheduleNativeTitleBarTheme()
    {
        Dispatcher.UIThread.Post(ApplyNativeTitleBarTheme);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ApplyNativeTitleBarTheme();
        };
        timer.Start();
    }

    private void ApplyNativeTitleBarTheme()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IntPtr handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        int dark = ThemeManager.CurrentMode == ThemeManager.DarkLabel
            || (ThemeManager.CurrentMode == ThemeManager.SystemLabel && ActualThemeVariant == ThemeVariant.Dark)
            ? 1
            : 0;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) != 0)
            DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));

        // DWM updates the title bar colors asynchronously. Force a non-client frame
        // repaint so the new color is visible in the same frame as the theme switch.
        SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
        RedrawWindow(
            handle,
            IntPtr.Zero,
            IntPtr.Zero,
            RdwFrame | RdwInvalidate | RdwUpdateNow | RdwAllChildren);

        // Some Windows builds cache the caption colors until a non-client activation
        // cycle occurs; this is the reliable repaint trigger after a live DWM change.
        SendMessage(handle, WmNcActivate, IntPtr.Zero, IntPtr.Zero);
        SendMessage(handle, WmNcActivate, new IntPtr(1), IntPtr.Zero);
    }

    private static WindowIcon CreateAppIcon()
    {
        const int size = 64;
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (DrawingContext context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.Parse("#2F6BFF")),
                null,
                new Rect(0, 0, size, size),
                0,
                0);

            var linePen = new Pen(Brushes.White, 4);
            var left = new Point(18, 22);
            var right = new Point(46, 20);
            var bottom = new Point(32, 46);
            context.DrawLine(linePen, left, right);
            context.DrawLine(linePen, left, bottom);
            context.DrawLine(linePen, right, bottom);
            context.DrawEllipse(Brushes.White, null, left, 7, 7);
            context.DrawEllipse(Brushes.White, null, right, 7, 7);
            context.DrawEllipse(Brushes.White, null, bottom, 8, 8);
        }

        return new WindowIcon(bitmap);
    }

    protected override void OnClosed(EventArgs e)
    {
        ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        ThemeManager.ThemeChanged -= OnThemeManagerThemeChanged;
        Opened -= OnOpenedForTitleBarTheme;

        if (DataContext is MainWindowViewModel vm)
            vm.Shutdown();
        base.OnClosed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && DataContext is MainWindowViewModel vm)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                switch (e.Key)
                {
                    case Key.N:
                        vm.NewConnectionCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.W:
                        vm.CloseSelectedTabCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.F:
                        vm.ShowHistorySidebar();
                        Dispatcher.UIThread.Post(HistoryPanel.FocusSearch);
                        e.Handled = true;
                        break;
                    case Key.L:
                        vm.SelectedTab?.ClearLogCommand.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Enter when vm.SelectedTab is RawConnectionViewModelBase raw:
                        raw.SendManualCommand.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
        }

        base.OnKeyDown(e);
    }
}
