using System;
using System.Windows;

namespace minimal_switcher;

public sealed class SwitcherController
{
    private MainWindow? _window;

    public static SwitcherController Instance { get; } = new();

    private SwitcherController()
    {
    }

    public void Begin(IntPtr sourceHwnd, SwitcherFilter filter)
    {
        GetWindow().HandleAltTab(isFirstTime: true, sourceHwnd, filter);
    }

    public void Next()
    {
        _window?.HandleAltTab(isFirstTime: false, IntPtr.Zero, SwitcherFilter.AllWindows);
    }

    public void Complete()
    {
        _window?.HandleAltRelease();
    }

    public void Cancel()
    {
        _window?.CancelSwitch();
    }

    private MainWindow GetWindow()
    {
        if (_window is { IsLoaded: true })
        {
            return _window;
        }

        _window = new MainWindow();
        return _window;
    }
}
