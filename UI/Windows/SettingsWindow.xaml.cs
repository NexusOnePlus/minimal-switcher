using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace minimal_switcher;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel = new();
    private readonly AppThemeService _themeService = AppThemeService.Instance;
    private bool _isLoading;
    private bool _isSyncingIconColor;

    public SettingsWindow()
    {
        InitializeComponent();
        Icon = AppIconFactory.CreateWindowIcon();
        PresetItems.ItemsSource = _viewModel.Presets;
        VersionText.Text = $"Version {_viewModel.AppVersion}";
        LoadSettings();
        RefreshWindowLists();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        var settings = _viewModel.Current;
        ShaderModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Shader;
        PresetModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Preset;
        CustomModeButton.IsChecked = settings.ThemeMode == AppThemeMode.Custom;
        CustomColorTextBox.Text = settings.CustomBackgroundColor;
        OpacitySlider.Value = settings.CustomBackgroundOpacity;
        OpacityValueText.Text = $"{settings.CustomBackgroundOpacity}%";
        IconTreatmentCheckBox.IsChecked = settings.IconTreatmentMode == IconTreatmentMode.Unified;
        IconTintTextBox.Text = settings.IconTintColor;
        SyncIconColorPicker(settings.IconTintColor);
        IconTintSlider.Value = settings.IconTintStrength;
        IconTintValueText.Text = $"{settings.IconTintStrength}%";
        SameProcessShortcutCheckBox.IsChecked = settings.EnableSameProcessShortcut;
        CombineAppInstancesCheckBox.IsChecked = settings.CombineAppInstances;
        UpdatePreview(settings);

        _isLoading = false;
    }

    private void CombineAppInstancesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _viewModel.SetCombineAppInstances(CombineAppInstancesCheckBox.IsChecked == true);
    }

    private void UpdatePreview(AppSettings settings)
    {
        if (settings.ThemeMode == AppThemeMode.Shader)
        {
            PreviewCard.Background = _themeService.CreateBrush("#F0151517");
            PreviewCard.BorderBrush = _themeService.CreateBrush("#55FFFFFF");
            return;
        }

        PreviewCard.Background = _themeService.CreateBrush(_themeService.GetSwitcherBackground(settings));
        PreviewCard.BorderBrush = _themeService.CreateBrush(_themeService.GetSwitcherBorder(settings));
    }

    private void ThemeMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender == ShaderModeButton)
        {
            _viewModel.SetThemeMode(AppThemeMode.Shader);
        }
        else if (sender == PresetModeButton)
        {
            _viewModel.SetThemeMode(AppThemeMode.Preset);
        }
        else
        {
            _viewModel.SetThemeMode(AppThemeMode.Custom);
        }

        UpdatePreview(_viewModel.Current);
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ThemePreset preset }) return;

        _viewModel.SetPreset(preset);
        LoadSettings();
    }

    private void CustomColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;

        var color = CustomColorTextBox.Text.Trim();
        if (!_viewModel.TrySetCustomColor(color)) return;

        CustomModeButton.IsChecked = true;
        UpdatePreview(_viewModel.Current);
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText == null) return;

        var opacity = (int)Math.Round(e.NewValue);
        OpacityValueText.Text = $"{opacity}%";

        if (_isLoading) return;

        _viewModel.SetOpacity(opacity);
        UpdatePreview(_viewModel.Current);
    }

    private void IconTreatmentCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _viewModel.SetIconTreatment(IconTreatmentCheckBox.IsChecked == true);
        RefreshWindowLists();
    }

    private void IconTintTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || _isSyncingIconColor) return;

        var color = IconTintTextBox.Text.Trim();
        if (!_viewModel.TrySetIconTintColor(color)) return;

        SyncIconColorPicker(color);
        RefreshWindowLists();
    }

    private void IconColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string color }) return;

        SetIconColor(color);
    }

    private void IconColorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || _isSyncingIconColor || IconTintTextBox == null) return;

        var color = $"#{(int)Math.Round(IconRedSlider.Value):X2}{(int)Math.Round(IconGreenSlider.Value):X2}{(int)Math.Round(IconBlueSlider.Value):X2}";
        SetIconColor(color);
    }

    private void IconTintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IconTintValueText == null) return;

        var strength = (int)Math.Round(e.NewValue);
        IconTintValueText.Text = $"{strength}%";

        if (_isLoading) return;

        _viewModel.SetIconTintStrength(strength);
        RefreshWindowLists();
    }

    private void SameProcessShortcutCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _viewModel.SetSameProcessShortcut(SameProcessShortcutCheckBox.IsChecked == true);
    }

    private void SetIconColor(string color)
    {
        _isSyncingIconColor = true;
        IconTintTextBox.Text = color;
        SyncIconColorPicker(color);
        _isSyncingIconColor = false;

        if (_isLoading) return;
        if (!_viewModel.TrySetIconTintColor(color)) return;

        RefreshWindowLists();
    }

    private void SyncIconColorPicker(string color)
    {
        if (!TryParseHexColor(color, out var parsed)) return;

        _isSyncingIconColor = true;
        IconColorPreview.Background = new SolidColorBrush(parsed);
        IconRedSlider.Value = parsed.R;
        IconGreenSlider.Value = parsed.G;
        IconBlueSlider.Value = parsed.B;
        IconRedValueText.Text = parsed.R.ToString();
        IconGreenValueText.Text = parsed.G.ToString();
        IconBlueValueText.Text = parsed.B.ToString();
        _isSyncingIconColor = false;
    }

    private static bool TryParseHexColor(string value, out Color color)
    {
        color = Colors.Transparent;
        if (value.Length != 7 || value[0] != '#') return false;

        try
        {
            color = Color.FromRgb(
                Convert.ToByte(value.Substring(1, 2), 16),
                Convert.ToByte(value.Substring(3, 2), 16),
                Convert.ToByte(value.Substring(5, 2), 16));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TabButton_Checked(object sender, RoutedEventArgs e)
    {
        if (AppearancePanel == null || BehaviorPanel == null || IgnoredPanel == null || AboutPanel == null) return;

        var showBehavior = sender == BehaviorTabButton;
        var showIgnored = sender == IgnoredTabButton;
        var showAbout = sender == AboutTabButton;

        AppearancePanel.Visibility = !showBehavior && !showIgnored && !showAbout ? Visibility.Visible : Visibility.Collapsed;
        BehaviorPanel.Visibility = showBehavior ? Visibility.Visible : Visibility.Collapsed;
        IgnoredPanel.Visibility = showIgnored ? Visibility.Visible : Visibility.Collapsed;
        AboutPanel.Visibility = showAbout ? Visibility.Visible : Visibility.Collapsed;

        if (showIgnored)
        {
            RefreshWindowLists();
        }
    }

    private void OpenRepositoryButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenRepository();
    }

    private void OpenChangelogButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenChangelog();
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowLists();
    }

    private void IgnoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WindowItem item })
        {
            _viewModel.IgnoreWindow(item);
            RefreshWindowLists();
        }
    }

    private void RestoreWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: WindowItem item })
        {
            _viewModel.RestoreWindow(item);
            RefreshWindowLists();
        }
    }

    private void RefreshWindowLists()
    {
        _viewModel.RefreshWindowLists();
        RecentWindowsList.ItemsSource = _viewModel.RecentWindows;
        IgnoredWindowsList.ItemsSource = _viewModel.IgnoredWindows;
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
