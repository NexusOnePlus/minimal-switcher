using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace minimal_switcher;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settingsService = AppSettingsService.Instance;

    public IReadOnlyList<ThemePreset> Presets => _settingsService.Presets;
    public IReadOnlyList<WindowItem> RecentWindows { get; private set; } = [];
    public IReadOnlyList<WindowItem> IgnoredWindows { get; private set; } = [];
    public AppSettings Current => _settingsService.Current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void SetThemeMode(AppThemeMode mode)
    {
        UpdateSettings(settings => settings.ThemeMode = mode);
    }

    public void SetPreset(ThemePreset preset)
    {
        UpdateSettings(settings =>
        {
            settings.ThemeMode = AppThemeMode.Preset;
            settings.ThemePresetId = preset.Id;
        });
    }

    public bool TrySetCustomColor(string color)
    {
        if (!IsValidRgbHex(color)) return false;

        UpdateSettings(settings =>
        {
            settings.ThemeMode = AppThemeMode.Custom;
            settings.CustomBackgroundColor = color;
        });

        return true;
    }

    public void SetOpacity(int opacity)
    {
        UpdateSettings(settings => settings.CustomBackgroundOpacity = opacity);
    }

    public void RefreshWindowLists()
    {
        var recent = new List<WindowItem>();
        foreach (var item in WindowService.GetRecentWindows())
        {
            if (!item.IsIgnored)
            {
                recent.Add(item);
            }
        }

        RecentWindows = recent;
        IgnoredWindows = WindowService.GetIgnoredWindows();
        OnPropertyChanged(nameof(RecentWindows));
        OnPropertyChanged(nameof(IgnoredWindows));
    }

    public void IgnoreWindow(WindowItem item)
    {
        WindowService.IgnoreWindow(item.Hwnd);
        RefreshWindowLists();
    }

    public void RestoreWindow(WindowItem item)
    {
        WindowService.RestoreWindow(item.Hwnd);
        RefreshWindowLists();
    }

    private void UpdateSettings(Action<AppSettings> update)
    {
        var current = _settingsService.Current;
        var next = new AppSettings
        {
            ThemeMode = current.ThemeMode,
            ThemePresetId = current.ThemePresetId,
            CustomBackgroundColor = current.CustomBackgroundColor,
            CustomBackgroundOpacity = current.CustomBackgroundOpacity
        };

        update(next);
        _settingsService.Update(next);
        OnPropertyChanged(nameof(Current));
    }

    private static bool IsValidRgbHex(string value)
    {
        if (value.Length != 7 || value[0] != '#') return false;

        for (var i = 1; i < value.Length; i++)
        {
            if (!int.TryParse(value[i].ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            {
                return false;
            }
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
