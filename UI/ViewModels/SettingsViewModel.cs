using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace minimal_switcher;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettingsService _settingsService = AppSettingsService.Instance;
    private readonly AppInfoService _appInfo = AppInfoService.Instance;

    public IReadOnlyList<ThemePreset> Presets => _settingsService.Presets;
    public IReadOnlyList<WindowItem> RecentWindows { get; private set; } = [];
    public IReadOnlyList<WindowItem> IgnoredWindows { get; private set; } = [];
    public AppSettings Current => _settingsService.Current;
    public string AppName => _appInfo.Name;
    public string AppVersion => _appInfo.Version;
    public string RepositoryUrl => _appInfo.RepositoryUrl;
    public string ChangelogUrl => _appInfo.ChangelogUrl;

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

    public void OpenRepository() => _appInfo.OpenUrl(RepositoryUrl);

    public void OpenChangelog() => _appInfo.OpenUrl(ChangelogUrl);

    private void UpdateSettings(Action<AppSettings> update)
    {
        var current = _settingsService.Current;
        var next = new AppSettings
        {
            ThemeMode = current.ThemeMode,
            ThemePresetId = current.ThemePresetId,
            CustomBackgroundColor = current.CustomBackgroundColor,
            CustomBackgroundOpacity = current.CustomBackgroundOpacity,
            IconTreatmentMode = current.IconTreatmentMode,
            IconTintColor = current.IconTintColor,
            IconTintStrength = current.IconTintStrength,
            EnableSameProcessShortcut = current.EnableSameProcessShortcut,
            CombineAppInstances = current.CombineAppInstances
        };

        update(next);
        _settingsService.Update(next);
        OnPropertyChanged(nameof(Current));
    }

    public void SetCombineAppInstances(bool enabled)
    {
        UpdateSettings(settings => settings.CombineAppInstances = enabled);
    }

    public void SetIconTreatment(bool enabled)
    {
        UpdateSettings(settings =>
            settings.IconTreatmentMode = enabled ? IconTreatmentMode.Unified : IconTreatmentMode.Native);
    }

    public bool TrySetIconTintColor(string color)
    {
        if (!IsValidRgbHex(color)) return false;

        UpdateSettings(settings => settings.IconTintColor = color);
        return true;
    }

    public void SetIconTintStrength(int strength)
    {
        UpdateSettings(settings => settings.IconTintStrength = strength);
    }

    public void SetSameProcessShortcut(bool enabled)
    {
        UpdateSettings(settings => settings.EnableSameProcessShortcut = enabled);
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
