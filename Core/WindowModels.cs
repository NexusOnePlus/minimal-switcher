using System;
using System.ComponentModel;
using System.Windows.Media;

namespace minimal_switcher;

public enum AppType
{
    Path,
    Aumid
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
}

public class SwitcherItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
