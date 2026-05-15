using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace minimal_switcher;

internal static class AppIconFactory
{
    public static Drawing.Icon CreateTrayIcon()
    {
        using var bitmap = CreateIconBitmap(64);
        var handle = bitmap.GetHicon();

        try
        {
            using var icon = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public static ImageSource CreateWindowIcon()
    {
        using var bitmap = CreateIconBitmap(64);
        var handle = bitmap.GetHbitmap();

        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));

            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static Drawing.Bitmap CreateIconBitmap(int size)
    {
        var bitmap = new Drawing.Bitmap(size, size);
        using var graphics = Drawing.Graphics.FromImage(bitmap);

        graphics.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Drawing.Color.Transparent);

        var scale = size / 64f;
        using var shellBrush = new Drawing.Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle(0, 0, size, size),
            Drawing.Color.FromArgb(255, 18, 20, 25),
            Drawing.Color.FromArgb(255, 42, 47, 58),
            45f);
        using var accentBrush = new Drawing.Drawing2D.LinearGradientBrush(
            new Drawing.Rectangle((int)(14 * scale), (int)(14 * scale), (int)(36 * scale), (int)(36 * scale)),
            Drawing.Color.FromArgb(255, 245, 247, 250),
            Drawing.Color.FromArgb(255, 156, 170, 194),
            35f);
        using var shadowBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(70, 0, 0, 0));
        using var ringPen = new Drawing.Pen(Drawing.Color.FromArgb(90, 255, 255, 255), 2f * scale);

        using var shadow = RoundedRect(8 * scale, 10 * scale, 48 * scale, 48 * scale, 15 * scale);
        graphics.FillPath(shadowBrush, shadow);

        using var shell = RoundedRect(8 * scale, 7 * scale, 48 * scale, 48 * scale, 15 * scale);
        graphics.FillPath(shellBrush, shell);
        graphics.DrawPath(ringPen, shell);

        using var topBar = RoundedRect(18 * scale, 20 * scale, 28 * scale, 8 * scale, 4 * scale);
        using var bottomBar = RoundedRect(18 * scale, 36 * scale, 28 * scale, 8 * scale, 4 * scale);
        graphics.FillPath(accentBrush, topBar);
        graphics.FillPath(accentBrush, bottomBar);

        return bitmap;
    }

    private static Drawing.Drawing2D.GraphicsPath RoundedRect(float x, float y, float width, float height, float radius)
    {
        var path = new Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180, 90);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(nint hObject);
}
