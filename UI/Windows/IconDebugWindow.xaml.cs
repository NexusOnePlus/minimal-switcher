using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace minimal_switcher;

public partial class IconDebugWindow : Window
{
    private readonly WindowService _windowService = new();

    public IconDebugWindow()
    {
        InitializeComponent();
        RefreshWindowList();
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Icon sources|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.ico;*.exe;*.dll|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true) return;
        LoadPath(dialog.FileName);
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void WindowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WindowsList.SelectedItem is not WindowItem item) return;

        PathText.Text = $"{item.AppName} - {item.Title}";
        AnalyzeSource(item.Icon);
    }

    private void RefreshWindowList()
    {
        WindowsList.ItemsSource = _windowService.EnumerateWindowsForDebug();
    }

    private void LoadPath(string path)
    {
        try
        {
            PathText.Text = path;
            var source = LoadImageSource(path);
            AnalyzeSource(source);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo cargar el archivo", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AnalyzeSource(ImageSource? source)
    {
        var debug = IconAppearanceService.AnalyzeForDebug(source);
        if (debug == null)
        {
            MessageBox.Show(this, "No se pudo analizar el icono.", "Icon Debug", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        NormalizedImage.Source = debug.Normalized;
        MaskImage.Source = debug.Mask;
        ResidualImage.Source = debug.Residual;
        ProcessedImage.Source = debug.Processed;
        ProcessedImage.Width = debug.IconSize;
        ProcessedImage.Height = debug.IconSize;

        DecisionText.Text = debug.IsFullBleed
            ? "Full square"
            : debug.IsRoundedSquare
                ? "Rounded-square"
                : debug.NeedsBacking
                    ? "Organico / backing"
                    : "Compacto";

        MetricsText.Text =
            $"IconSize       {debug.IconSize.ToString("0.##", CultureInfo.InvariantCulture)}\n" +
            $"FullBleed      {debug.IsFullBleed}\n" +
            $"RoundedSquare  {debug.IsRoundedSquare}\n" +
            $"NeedsBacking   {debug.NeedsBacking}\n\n" +
            $"FillRatio      {debug.FillRatio:0.000}\n" +
            $"CanvasRatio    {debug.CanvasRatio:0.000}\n" +
            $"CornerOpacity  {debug.CornerOpacity:0.000}\n" +
            $"EdgeOpacity    {debug.EdgeOpacity:0.000}\n" +
            $"MissingInside  {debug.MissingInside:0.000}\n" +
            $"OutsideLeak    {debug.OutsideLeak:0.000}";
    }

    private static BitmapSource? LoadImageSource(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return WindowService.GetIconFromPathForDebug(path);
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
