using System.Windows;

namespace minimal_switcher;

public sealed class SwitcherController
{
    private MainWindow? _window;

    public static SwitcherController Instance { get; } = new();

    private SwitcherController()
    {
    }

    public void Begin()
    {
        GetWindow().HandleAltTab(isFirstTime: true);
    }

    public void Next()
    {
        _window?.HandleAltTab(isFirstTime: false);
    }

    public void Complete()
    {
        _window?.HandleAltRelease();
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
