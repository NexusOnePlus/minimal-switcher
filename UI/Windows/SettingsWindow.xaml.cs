using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace minimal_switcher;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsService _settingsService = AppSettingsService.Instance;
    private bool _isLoading;

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();
        PresetItems.ItemsSource = _settingsService.Presets;
        LoadSettings();
        RefreshWindowLists();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        var settings = _settingsService.Current;
        ShaderModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Shader;
        PresetModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Preset;
        CustomModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Custom;
        CustomColorTextBox.Text = settings.CustomBackgroundColor;
        OpacitySlider.Value = settings.CustomBackgroundOpacity;
        OpacityValueText.Text = $"{settings.CustomBackgroundOpacity}%";
        UpdatePreview(settings);

        _isLoading = false;
    }

    private void SaveSettings(Action<AppSettings> update)
    {
        if (_isLoading) return;

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
        UpdatePreview(_settingsService.Current);
    }

    private void UpdatePreview(AppSettings settings)
    {
        if (settings.ThemeMode == AppThemeMode.Shader)
        {
            PreviewCard.Background = BrushFromHex("#F0151517");
            PreviewCard.BorderBrush = BrushFromHex("#55FFFFFF");
            return;
        }

        var background = settings.ThemeMode == AppThemeMode.Custom
            ? WithOpacity(settings.CustomBackgroundColor, settings.CustomBackgroundOpacity)
            : WithOpacity(_settingsService.CurrentPreset.Background, settings.CustomBackgroundOpacity);

        var border = settings.ThemeMode == AppThemeMode.Custom
            ? "#33FFFFFF"
            : _settingsService.CurrentPreset.Border;

        PreviewCard.Background = BrushFromHex(background);
        PreviewCard.BorderBrush = BrushFromHex(border);
    }

    private void ThemeMode_Checked(object sender, RoutedEventArgs e)
    {
        SaveSettings(settings =>
        {
            if (sender == ShaderModeButton)
            {
                settings.ThemeMode = AppThemeMode.Shader;
            }
            else if (sender == PresetModeButton)
            {
                settings.ThemeMode = AppThemeMode.Preset;
            }
            else
            {
                settings.ThemeMode = AppThemeMode.Custom;
            }
        });
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ThemePreset preset }) return;

        SaveSettings(settings =>
        {
            settings.ThemeMode = AppThemeMode.Preset;
            settings.ThemePresetId = preset.Id;
        });

        LoadSettings();
    }

    private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var color = CustomColorTextBox.Text.Trim();
        if (!IsValidRgbHex(color)) return;

        SaveSettings(settings =>
        {
            settings.ThemeMode = AppThemeMode.Custom;
            settings.CustomBackgroundColor = color;
        });

        CustomModeButton.IsChecked = true;
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText == null) return;

        var opacity = (int)Math.Round(e.NewValue);
        OpacityValueText.Text = $"{opacity}%";

        SaveSettings(settings =>
        {
            settings.CustomBackgroundOpacity = opacity;
        });
    }

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (AppearancePanel == null || IgnoredPanel == null) return;

        var showIgnored = sender == IgnoredTabButton;
        AppearancePanel.Visibility = showIgnored ? Visibility.Collapsed : Visibility.Visible;
        IgnoredPanel.Visibility = showIgnored ? Visibility.Visible : Visibility.Collapsed;

        if (showIgnored)
        {
            RefreshWindowLists();
        }
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowLists();
    }

    private void IgnoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WindowItem item })
        {
            WindowService.IgnoreWindow(item.Hwnd);
            RefreshWindowLists();
        }
    }

    private void RestoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WindowItem item })
        {
            WindowService.RestoreWindow(item.Hwnd);
            RefreshWindowLists();
        }
    }

    private void RefreshWindowLists()
    {
        var recent = new List<WindowItem>();
        foreach (var item in WindowService.GetRecentWindows())
        {
            if (!item.IsIgnored)
            {
                recent.Add(item);
            }
        }

        RecentWindowsList.ItemsSource = recent;
        IgnoredWindowsList.ItemsSource = WindowService.GetIgnoredWindows();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed
            && e.OriginalSource is DependencyObject source
            && !HasInteractiveParent(source))
        {
            DragMove();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Application.Current is App { IsShuttingDown: true })
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private static Brush BrushFromHex(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static string WithOpacity(string rgbHex, int opacity)
    {
        var alpha = (int)Math.Round(Math.Clamp(opacity, 0, 100) * 255 / 100.0);
        var hex = rgbHex.TrimStart('#');

        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        return $"#{alpha:X2}{hex}";
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

    private static bool HasInteractiveParent(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is ButtonBase
                or TextBox
                or Slider
                or ListBoxItem)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
