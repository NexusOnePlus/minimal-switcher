using System;
using System.Windows.Media;

namespace minimal_switcher;

public sealed class AppThemeService
{
    public static AppThemeService Instance { get; } = new();

    private readonly AppSettingsService _settingsService = AppSettingsService.Instance;

    private AppThemeService()
    {
    }

    public string GetSwitcherBackground(AppSettings settings)
    {
        return settings.ThemeMode == AppThemeMode.Custom
            ? WithOpacity(settings.CustomBackgroundColor, settings.CustomBackgroundOpacity)
            : WithOpacity(_settingsService.CurrentPreset.Background, settings.CustomBackgroundOpacity);
    }

    public string GetSwitcherBorder(AppSettings settings)
    {
        return settings.ThemeMode == AppThemeMode.Custom
            ? "#33FFFFFF"
            : _settingsService.CurrentPreset.Border;
    }

    public Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    public string WithOpacity(string rgbHex, int opacity)
    {
        var alpha = (int)Math.Round(Math.Clamp(opacity, 0, 100) * 255 / 100.0);
        var hex = rgbHex.TrimStart('#');

        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        return $"#{alpha:X2}{hex}";
    }
}
