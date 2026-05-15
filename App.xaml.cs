using System;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace minimal_switcher;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private Drawing.Icon? _trayIconImage;
    private SettingsWindow? _settingsWindow;

    internal bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        KeyboardHook.Start();
        CreateTrayIcon();
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisposeTrayIcon();
        KeyboardHook.Stop();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Configuracion", null, (_, _) => Dispatcher.Invoke(ShowSettingsWindow));
        menu.Items.Add("Cerrar", null, (_, _) => Dispatcher.Invoke(ShutdownApplication));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _trayIconImage = AppIconFactory.CreateTrayIcon(),
            Text = "Minimal Switcher",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettingsWindow);
    }

    private void ShowSettingsWindow()
    {
        try
        {
            _settingsWindow ??= new SettingsWindow();

            if (!_settingsWindow.IsVisible)
            {
                _settingsWindow.Show();
            }

            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Activate();
        }
        catch (Exception ex)
        {
            _settingsWindow = null;
            MessageBox.Show(
                ex.Message,
                "No se pudo abrir Configuracion",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ShutdownApplication()
    {
        IsShuttingDown = true;
        Shutdown();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon == null) return;

        _trayIcon.Visible = false;
        _trayIcon.ContextMenuStrip?.Dispose();
        _trayIcon.Dispose();
        _trayIcon = null;
        _trayIconImage?.Dispose();
        _trayIconImage = null;
    }
}
