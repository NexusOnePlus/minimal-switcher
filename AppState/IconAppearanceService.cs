using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace minimal_switcher;

public static class IconAppearanceService
{
    private const int OutputSize = 128;

    public static string SettingsKey
    {
        get
        {
            var settings = AppSettingsService.Instance.Current;
            return $"{settings.IconTreatmentMode}:{settings.IconTintColor}:{settings.IconTintStrength}";
        }
    }

    public static ImageSource? Apply(ImageSource? source)
    {
        if (source is not BitmapSource bitmapSource) return source;

        var settings = AppSettingsService.Instance.Current;
        if (settings.IconTreatmentMode == IconTreatmentMode.Native && settings.IconTintStrength == 0)
        {
            return source;
        }

        try
        {
            var bitmap = EnsureBgra32(bitmapSource);
            var analysis = Analyze(bitmap);
            var destination = new Rect(GetInset(analysis), GetInset(analysis),
                OutputSize - GetInset(analysis) * 2,
                OutputSize - GetInset(analysis) * 2);

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                if (settings.IconTreatmentMode == IconTreatmentMode.Unified && analysis.NeedsBacking)
                {
                    var backing = new SolidColorBrush(analysis.BackingColor);
                    backing.Freeze();
                    context.DrawRoundedRectangle(backing, null, new Rect(8, 8, 112, 112), 30, 30);
                }

                context.DrawImage(bitmap, destination);
            }

            var render = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            render.Freeze();

            if (settings.IconTintStrength <= 0) return render;

            return Tint(render, settings.IconTintColor, settings.IconTintStrength);
        }
        catch
        {
            return source;
        }
    }

    private static double GetInset(IconAnalysis analysis)
    {
        if (analysis.IsFullBleed) return 3;
        if (analysis.NeedsBacking) return 20;
        return 12;
    }

    private static BitmapSource EnsureBgra32(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        if (converted.PixelWidth == OutputSize && converted.PixelHeight == OutputSize)
        {
            converted.Freeze();
            return converted;
        }

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(converted, new Rect(0, 0, OutputSize, OutputSize));
        }

        var render = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze();
        return render;
    }

    private static IconAnalysis Analyze(BitmapSource source)
    {
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);

        var minX = source.PixelWidth;
        var minY = source.PixelHeight;
        var maxX = 0;
        var maxY = 0;
        var opaque = 0;
        long red = 0;
        long green = 0;
        long blue = 0;

        for (var y = 0; y < source.PixelHeight; y++)
        {
            for (var x = 0; x < source.PixelWidth; x++)
            {
                var index = y * stride + x * 4;
                var alpha = pixels[index + 3];
                if (alpha < 24) continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                opaque++;
                blue += pixels[index];
                green += pixels[index + 1];
                red += pixels[index + 2];
            }
        }

        if (opaque == 0)
        {
            return new IconAnalysis(false, false, Color.FromRgb(28, 32, 40));
        }

        var boundsWidth = maxX - minX + 1;
        var boundsHeight = maxY - minY + 1;
        var fillRatio = opaque / (double)(boundsWidth * boundsHeight);
        var canvasRatio = Math.Max(boundsWidth / (double)source.PixelWidth, boundsHeight / (double)source.PixelHeight);
        var fullBleed = canvasRatio >= 0.86 && fillRatio >= 0.72;
        var needsBacking = !fullBleed && (fillRatio < 0.78 || canvasRatio < 0.82);

        var dominant = Color.FromRgb((byte)(red / opaque), (byte)(green / opaque), (byte)(blue / opaque));
        return new IconAnalysis(fullBleed, needsBacking, CreateBackingColor(dominant));
    }

    private static BitmapSource Tint(BitmapSource source, string tintHex, int strength)
    {
        var tint = ParseColor(tintHex);
        var amount = Math.Clamp(strength / 100.0, 0, 1);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var alpha = pixels[index + 3];
            if (alpha == 0) continue;

            pixels[index] = Blend(pixels[index], tint.B, amount);
            pixels[index + 1] = Blend(pixels[index + 1], tint.G, amount);
            pixels[index + 2] = Blend(pixels[index + 2], tint.R, amount);
        }

        var result = BitmapSource.Create(
            source.PixelWidth,
            source.PixelHeight,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            stride);
        result.Freeze();
        return result;
    }

    private static byte Blend(byte current, byte target, double amount)
    {
        return (byte)Math.Clamp(current + (target - current) * amount, 0, 255);
    }

    private static Color CreateBackingColor(Color color)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * 0.38, 12, 92),
            (byte)Math.Clamp(color.G * 0.38, 12, 92),
            (byte)Math.Clamp(color.B * 0.38, 12, 92));
    }

    private static Color ParseColor(string hex)
    {
        return Color.FromRgb(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
    }

    private sealed record IconAnalysis(bool IsFullBleed, bool NeedsBacking, Color BackingColor);
}
