using minimal_switcher.Shaders;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace minimal_switcher;

public partial class MainWindow : Window
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "minimal-switcher.log");
    private readonly WindowService _windowService = new();
    private readonly AppThemeService _themeService = AppThemeService.Instance;
    private ObservableCollection<SwitcherItem> _items = new();
    private int _currentIndex;
    private GlassyEffect? _glassyEffect;
    private ImageBrush? _backdropBrush;
    private RectangleGeometry? _windowFrameClip;
    private RectangleGeometry? _windowClipRootClip;
    private bool _isRenderingSubscribed;
    private bool _useShaderTheme = true;

    public MainWindow()
    {
        InitializeComponent();
        IconsPanel.ItemsSource = _items;
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        LocationChanged += OnLocationChanged;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
        AppSettingsService.Instance.SettingsChanged += OnSettingsChanged;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch { }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        SetBlurBehind(hwnd);
        // Exclude from our capture, but we'll re-enable for recorders later
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SetupGlassyLayer();
        StartRecaptureTimer();
        Dispatcher.BeginInvoke(new Action(CenterWindowOnMonitor), DispatcherPriority.Loaded);
    }

    private void OnClosing(object? sender, EventArgs e)
    {
        AppSettingsService.Instance.SettingsChanged -= OnSettingsChanged;
        if (_isRenderingSubscribed)
        {
            CompositionTarget.Rendering -= OnRendering;
            _isRenderingSubscribed = false;
        }
    }

    private void CenterWindowOnMonitor()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || Visibility != Visibility.Visible) return;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero) return;

        var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo)) return;

        // Get DPI scale (pixels per DIP)
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.PixelsPerDip;
        var scaleY = dpi.PixelsPerDip;

        // Convert monitor work area from pixels to DIP
        var workLeftDip = monitorInfo.rcWork.Left / scaleX;
        var workTopDip = monitorInfo.rcWork.Top / scaleY;
        var workWidthDip = (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / scaleX;
        var workHeightDip = (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / scaleY;

        var widthDip = ActualWidth;
        var heightDip = ActualHeight;

        var leftDip = workLeftDip + (workWidthDip - widthDip) / 2;
        var topDip = workTopDip + (workHeightDip - heightDip) / 2;

        Left = leftDip;
        Top = topDip;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
    private const int WDA_NONE = 0x00000000;
    private const int WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGlassyParameters();
        UpdateBackdrop();
        UpdateWindowClip();
        // Defer centering to after layout completes
        Dispatcher.BeginInvoke(new Action(CenterWindowOnMonitor), DispatcherPriority.Loaded);
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        UpdateGlassyParameters();
        UpdateBackdrop();
    }

    private void StartRecaptureTimer()
    {
        if (!_useShaderTheme) return;

        if (!_isRenderingSubscribed)
        {
            CompositionTarget.Rendering += OnRendering;
            _isRenderingSubscribed = true;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_useShaderTheme || Visibility != Visibility.Visible) return;
        CaptureBehindWindow();
    }

    private void SetupGlassyLayer()
    {
        ApplyTheme();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var settings = AppSettingsService.Instance.Current;
        _useShaderTheme = settings.ThemeMode == AppThemeMode.Shader;

        if (_useShaderTheme)
        {
            ApplyShaderTheme();
            return;
        }

        StopRecaptureTimer();
        _glassyEffect = null;
        GlassyLayer.Effect = null;

        var color = _themeService.GetSwitcherBackground(settings);
        var border = _themeService.GetSwitcherBorder(settings);

        WindowFrame.Background = _themeService.CreateBrush(color);
        WindowFrame.BorderBrush = _themeService.CreateBrush(border);
        GlassyLayer.Background = _themeService.CreateBrush(color);
        ContentOverlay.Background = Brushes.Transparent;

        UpdateWindowClip();
    }

    private void ApplyShaderTheme()
    {
        _backdropBrush = new ImageBrush
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };

        WindowFrame.Background = _backdropBrush;
        GlassyLayer.Background = _backdropBrush;
        _glassyEffect = new GlassyEffect();
        GlassyLayer.Effect = _glassyEffect;
        ContentOverlay.Background = _themeService.CreateBrush("#2A000000");

        CaptureBehindWindow();
        UpdateBackdrop();
        UpdateGlassyParameters();
        UpdateWindowClip();
    }

    private void StopRecaptureTimer()
    {
        if (!_isRenderingSubscribed) return;

        CompositionTarget.Rendering -= OnRendering;
        _isRenderingSubscribed = false;
    }

    private void CaptureBehindWindow()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        
        // Use SetWindowDisplayAffinity to exclude from our capture
        // This prevents self-capture without hide/show flicker
        if (hwnd != IntPtr.Zero)
        {
            SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
        }

        ScreenCaptureHelper.CaptureFullScreen();

        if (hwnd != IntPtr.Zero)
        {
            // Reset so recorders can see us
            SetWindowDisplayAffinity(hwnd, WDA_NONE);
        }

        UpdateBackdrop();
    }

    private void UpdateGlassyParameters()
    {
        if (_glassyEffect == null) return;

        var width = Math.Max(1.0, WindowFrame.ActualWidth);
        var height = Math.Max(1.0, WindowFrame.ActualHeight);

        _glassyEffect.TextureSize = new global::System.Windows.Point(width, height);
        _glassyEffect.GlassCenter = new global::System.Windows.Point(width * 0.5, height * 0.5);
        _glassyEffect.GlassSize = new global::System.Windows.Point(width, height);
        _glassyEffect.BlurIntensity = 0.6f;
    }

    private void UpdateWindowClip()
    {
        const double cornerRadius = 20;
        ApplyRectangularClip(WindowFrame, ref _windowFrameClip, cornerRadius);
        ApplyRectangularClip(WindowClipRoot, ref _windowClipRootClip, cornerRadius);
    }

    private static void ApplyRectangularClip(FrameworkElement? element, ref RectangleGeometry? clip, double cornerRadius)
    {
        if (element == null) return;

        var width = Math.Max(0.0, element.ActualWidth);
        var height = Math.Max(0.0, element.ActualHeight);
        if (width <= 0.0 || height <= 0.0) return;

        var rect = new Rect(0, 0, width, height);
        if (clip == null)
        {
            clip = new RectangleGeometry(rect, cornerRadius, cornerRadius);
            element.Clip = clip;
            return;
        }

        clip.Rect = rect;
        clip.RadiusX = cornerRadius;
        clip.RadiusY = cornerRadius;
    }

    private void UpdateBackdrop()
    {
        if (_backdropBrush == null || WindowState == WindowState.Minimized) return;

        var snapshot = ScreenCaptureHelper.FullScreenSnapshot;
        if (snapshot == null) return;

        var topLeft = PointToScreen(new Point(0, 0));
        var bottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        var x = (int)Math.Round(topLeft.X - ScreenCaptureHelper.VirtualScreenX);
        var y = (int)Math.Round(topLeft.Y - ScreenCaptureHelper.VirtualScreenY);
        var width = Math.Max(1, (int)Math.Round(bottomRight.X - topLeft.X));
        var height = Math.Max(1, (int)Math.Round(bottomRight.Y - topLeft.Y));

        if (x < 0) { width += x; x = 0; }
        if (y < 0) { height += y; y = 0; }
        if (x + width > snapshot.PixelWidth) width = snapshot.PixelWidth - x;
        if (y + height > snapshot.PixelHeight) height = snapshot.PixelHeight - y;
        if (width <= 0 || height <= 0) return;

        _backdropBrush.ImageSource = snapshot;
        _backdropBrush.ViewboxUnits = BrushMappingMode.Absolute;
        _backdropBrush.Viewbox = new Rect(x, y, width, height);
    }

    public void HandleAltTab(bool isFirstTime)
    {
        Log($"HandleAltTab isFirstTime={isFirstTime}");

        if (isFirstTime)
        {
                RefreshWindows();
                Log($"Refreshed {_items.Count} windows");
                if (_items.Count == 0) return;
                _currentIndex = _items.Count > 1 ? 1 : 0;
                if (_useShaderTheme) CaptureBehindWindow();
                Show();
                StartRecaptureTimer();
        }
        else
        {
            if (_items.Count == 0) return;
            _currentIndex = (_currentIndex + 1) % _items.Count;
        }

        UpdateSelection();
    }

    public void HandleAltRelease()
    {
        Log($"HandleAltRelease items.Count={_items.Count} currentIndex={_currentIndex}");

        if (_items.Count == 0)
        {
            Hide();
            return;
        }

        var selected = _items[_currentIndex];
        Log($"Selected: '{selected.Title}' Hwnd={selected.Hwnd}");

        var placement = new NativeMethods.WINDOWPLACEMENT { length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>() };
        NativeMethods.GetWindowPlacement(selected.Hwnd, ref placement);
        if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
            NativeMethods.ShowWindow(selected.Hwnd, NativeMethods.SW_RESTORE);

        StopRecaptureTimer();

        // Re-enable display affinity so recorders can see us
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowDisplayAffinity(hwnd, WDA_NONE);
        }

        Hide();
        System.Threading.Thread.Sleep(50);
        ForceForegroundWindow(selected.Hwnd);
        _items.Clear();
    }

    private void RefreshWindows()
    {
        _items.Clear();
        var windows = _windowService.EnumerateWindowsWithIcons();
        foreach (var w in windows)
        {
            _items.Add(new SwitcherItem
            {
                Hwnd = w.Hwnd,
                Title = w.Title,
                Icon = w.Icon,
                IconSize = w.IconSize,
                IsSelected = false
            });
        }
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i].IsSelected = i == _currentIndex;
        }
    }

    private void ForceForegroundWindow(IntPtr hwnd)
    {
        var foregroundHwnd = GetForegroundWindow();
        var currentThreadId = GetCurrentThreadId();
        var targetThreadId = GetWindowThreadProcessId(hwnd, out _);
        var foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, out _);

        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, true);
        if (foregroundThreadId != targetThreadId && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
        }

        SetForegroundWindow(hwnd);

        if (foregroundThreadId != targetThreadId && foregroundThreadId != currentThreadId)
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        if (currentThreadId != targetThreadId)
            AttachThreadInput(currentThreadId, targetThreadId, false);
    }

    private void IconBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is SwitcherItem item)
        {
            var placement = new NativeMethods.WINDOWPLACEMENT { length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>() };
            NativeMethods.GetWindowPlacement(item.Hwnd, ref placement);
            if (placement.showCmd == NativeMethods.SW_SHOWMINIMIZED)
                NativeMethods.ShowWindow(item.Hwnd, NativeMethods.SW_RESTORE);
            ForceForegroundWindow(item.Hwnd);
            Hide();
            _items.Clear();
        }
    }

    private void SetBlurBehind(IntPtr hwnd)
    {
        try
        {
            var region = CreateRectRgn(0, 0, -1, -1);
            var blurBehind = new DwmBlurBehind { Flags = 3, Enabled = true, BlurRegion = region };
            DwmEnableBlurBehindWindow(hwnd, ref blurBehind);
            if (region != IntPtr.Zero) DeleteObject(region);
        }
        catch { }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;


    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, int dwAffinity);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DwmBlurBehind blurBehind);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmBlurBehind
    {
        public int Flags;
        public bool Enabled;
        public IntPtr BlurRegion;
        public bool TransitionOnMaximized;
    }
}
