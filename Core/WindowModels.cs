using System;
using System.ComponentModel;
using System.Windows.Media;

namespace minimal_switcher;

public enum AppType
{
    Path,
    Aumid
}

public enum SwitcherFilter
{
    AllWindows,
    SameProcess
}

public class WindowItem
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public uint ProcessId { get; set; }
    public bool IsIgnored { get; set; }
    public ImageSource? Icon { get; set; }
    public double IconSize { get; set; } = 64;
}

public class SwitcherItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _useZoomSelection;
    private string _selectionBorderColor = "#FFFFFF";
    private int _selectionBorderOpacity = 44;
    private int _selectionZoomPercent = 18;

    public IntPtr Hwnd { get; set; }
    public uint ProcessId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
    public double IconSize { get; set; } = 64;
    public Brush CurrentBackground => CreateSelectionBrush(_isSelected ? Math.Max(8, _selectionBorderOpacity / 3) : 6);
    public Brush CurrentBorderBrush => _isSelected
        ? CreateSelectionBrush(_selectionBorderOpacity)
        : CreateSelectionBrush(12);
    public double CurrentScale => _isSelected && _useZoomSelection
        ? 1 + _selectionZoomPercent / 100.0
        : 1;

    public bool UseZoomSelection
    {
        get => _useZoomSelection;
        set
        {
            _useZoomSelection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseZoomSelection)));
            NotifyVisualStateChanged();
        }
    }

    public string SelectionBorderColor
    {
        get => _selectionBorderColor;
        set
        {
            _selectionBorderColor = value;
            NotifyVisualStateChanged();
        }
    }

    public int SelectionBorderOpacity
    {
        get => _selectionBorderOpacity;
        set
        {
            _selectionBorderOpacity = value;
            NotifyVisualStateChanged();
        }
    }

    public int SelectionZoomPercent
    {
        get => _selectionZoomPercent;
        set
        {
            _selectionZoomPercent = value;
            NotifyVisualStateChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            NotifyVisualStateChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyVisualStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBackground)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentBorderBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentScale)));
    }

    private Brush CreateSelectionBrush(int opacity)
    {
        var color = ParseColor(_selectionBorderColor);
        color.A = (byte)Math.Clamp(opacity * 255 / 100, 0, 255);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#') return Colors.White;

        try
        {
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
        }
        catch
        {
            return Colors.White;
        }
    }
}
