using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace minimal_switcher;

public static class IconAppearanceService
{
    private const int OutputSize = 128;
    private const double DefaultIconSize = 64;
    private const double OrganicIconSize = 64;
    private const double RoundedSquareIconSize = 86;
    private const double FullBleedIconSize = 88;

    public static string SettingsKey
    {
        get
        {
            var settings = AppSettingsService.Instance.Current;
            return $"{settings.IconTreatmentMode}:{settings.IconTintColor}:{settings.IconTintStrength}";
        }
    }

    public static IconAppearance Apply(ImageSource? source)
    {
        if (source is not BitmapSource bitmapSource) return new IconAppearance(source, DefaultIconSize);

        var settings = AppSettingsService.Instance.Current;
        if (settings.IconTreatmentMode == IconTreatmentMode.Native && settings.IconTintStrength == 0)
        {
            return new IconAppearance(source, DefaultIconSize);
        }

        try
        {
            var bitmap = EnsureBgra32(bitmapSource);
            var analysis = Analyze(bitmap);
            var crop = CreateContentCrop(bitmap, analysis);
            var iconInset = GetRenderedInset(analysis, settings);
            var destination = new Rect(iconInset, iconInset, OutputSize - iconInset * 2, OutputSize - iconInset * 2);

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                if (settings.IconTreatmentMode == IconTreatmentMode.Unified && analysis.NeedsBacking)
                {
                    var backing = new SolidColorBrush(analysis.BackingColor);
                    backing.Freeze();
                    context.DrawRoundedRectangle(backing, null, new Rect(8, 8, 112, 112), 30, 30);
                }

                context.DrawImage(crop, destination);
            }

            var render = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            render.Freeze();

            var image = settings.IconTintStrength <= 0
                ? render
                : RemapPalette(render, settings.IconTintColor, settings.IconTintStrength);

            return new IconAppearance(image, GetIconSize(analysis, settings));
        }
        catch
        {
            return new IconAppearance(source, DefaultIconSize);
        }
    }

    private static double GetRenderedInset(IconAnalysis analysis, AppSettings settings)
    {
        if (settings.IconTreatmentMode != IconTreatmentMode.Unified) return 0;
        if (analysis.IsFullBleed) return 0;
        if (analysis.IsRoundedSquare) return 0;
        if (analysis.NeedsBacking) return 16;
        return 6;
    }

    private static double GetIconSize(IconAnalysis analysis, AppSettings settings)
    {
        if (settings.IconTreatmentMode != IconTreatmentMode.Unified) return DefaultIconSize;
        if (analysis.IsFullBleed) return FullBleedIconSize;
        if (analysis.IsRoundedSquare) return RoundedSquareIconSize;
        return OrganicIconSize;
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
            return new IconAnalysis(false, false, false, Color.FromRgb(28, 32, 40), new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight));
        }

        var boundsWidth = maxX - minX + 1;
        var boundsHeight = maxY - minY + 1;
        var fillRatio = opaque / (double)(boundsWidth * boundsHeight);
        var canvasRatio = Math.Max(boundsWidth / (double)source.PixelWidth, boundsHeight / (double)source.PixelHeight);
        var cornerOpacity = GetCornerOpacityRatio(pixels, stride, source.PixelWidth, source.PixelHeight);
        var edgeOpacity = GetEdgeOpacityRatio(pixels, stride, source.PixelWidth, source.PixelHeight);
        var transparentCorners = cornerOpacity < 0.22;
        var fullBleed = canvasRatio >= 0.9 && fillRatio >= 0.84 && !transparentCorners;
        var roundedSquare = canvasRatio >= 0.82
            && fillRatio >= 0.72
            && edgeOpacity >= 0.64;
        var organicShape = transparentCorners && fillRatio < 0.84;
        var needsBacking = organicShape || (!fullBleed && !roundedSquare && (fillRatio < 0.82 || canvasRatio < 0.84));

        var dominant = Color.FromRgb((byte)(red / opaque), (byte)(green / opaque), (byte)(blue / opaque));
        var padding = needsBacking ? 2 : 0;
        var crop = new Int32Rect(
            Math.Max(0, minX - padding),
            Math.Max(0, minY - padding),
            Math.Min(source.PixelWidth - Math.Max(0, minX - padding), boundsWidth + padding * 2),
            Math.Min(source.PixelHeight - Math.Max(0, minY - padding), boundsHeight + padding * 2));

        return new IconAnalysis(fullBleed, roundedSquare, needsBacking, CreateBackingColor(dominant), crop);
    }

    private static double GetCornerOpacityRatio(byte[] pixels, int stride, int width, int height)
    {
        var sample = Math.Max(6, width / 8);
        var opaque = 0;
        var total = 0;

        CountRegion(0, 0);
        CountRegion(width - sample, 0);
        CountRegion(0, height - sample);
        CountRegion(width - sample, height - sample);
        return total == 0 ? 0 : opaque / (double)total;

        void CountRegion(int startX, int startY)
        {
            for (var y = startY; y < startY + sample; y++)
            {
                for (var x = startX; x < startX + sample; x++)
                {
                    total++;
                    if (pixels[y * stride + x * 4 + 3] >= 24) opaque++;
                }
            }
        }
    }

    private static double GetEdgeOpacityRatio(byte[] pixels, int stride, int width, int height)
    {
        var sample = Math.Max(6, width / 12);
        var opaque = 0;
        var total = 0;

        CountRegion(width / 2 - sample / 2, 0, sample, sample);
        CountRegion(width / 2 - sample / 2, height - sample, sample, sample);
        CountRegion(0, height / 2 - sample / 2, sample, sample);
        CountRegion(width - sample, height / 2 - sample / 2, sample, sample);
        return total == 0 ? 0 : opaque / (double)total;

        void CountRegion(int startX, int startY, int regionWidth, int regionHeight)
        {
            for (var y = startY; y < startY + regionHeight; y++)
            {
                for (var x = startX; x < startX + regionWidth; x++)
                {
                    total++;
                    if (pixels[y * stride + x * 4 + 3] >= 24) opaque++;
                }
            }
        }
    }

    private static BitmapSource CreateContentCrop(BitmapSource source, IconAnalysis analysis)
    {
        if (analysis.IsFullBleed) return source;

        var cropped = new CroppedBitmap(source, analysis.ContentBounds);
        cropped.Freeze();
        return cropped;
    }

    private static BitmapSource RemapPalette(BitmapSource source, string tintHex, int strength)
    {
        var tint = ParseColor(tintHex);
        var amount = Math.Clamp(strength / 100.0, 0, 1);
        var target = RgbToHsl(tint.R, tint.G, tint.B);
        var stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var alpha = pixels[index + 3];
            if (alpha == 0) continue;

            var original = RgbToHsl(pixels[index + 2], pixels[index + 1], pixels[index]);
            var saturation = Math.Clamp(Math.Max(original.Saturation, target.Saturation * 0.58), 0, 1);
            var remapped = HslToRgb(target.Hue, saturation, original.Lightness);

            pixels[index] = Blend(pixels[index], remapped.B, amount);
            pixels[index + 1] = Blend(pixels[index + 1], remapped.G, amount);
            pixels[index + 2] = Blend(pixels[index + 2], remapped.R, amount);
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

    private static HslColor RgbToHsl(byte red, byte green, byte blue)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightness = (max + min) / 2.0;

        if (Math.Abs(max - min) < 0.0001)
        {
            return new HslColor(0, 0, lightness);
        }

        var delta = max - min;
        var saturation = lightness > 0.5
            ? delta / (2.0 - max - min)
            : delta / (max + min);

        double hue;
        if (Math.Abs(max - r) < 0.0001)
        {
            hue = (g - b) / delta + (g < b ? 6 : 0);
        }
        else if (Math.Abs(max - g) < 0.0001)
        {
            hue = (b - r) / delta + 2;
        }
        else
        {
            hue = (r - g) / delta + 4;
        }

        return new HslColor(hue / 6.0, saturation, lightness);
    }

    private static Color HslToRgb(double hue, double saturation, double lightness)
    {
        if (saturation <= 0)
        {
            var gray = (byte)Math.Clamp(lightness * 255, 0, 255);
            return Color.FromRgb(gray, gray, gray);
        }

        var q = lightness < 0.5
            ? lightness * (1 + saturation)
            : lightness + saturation - lightness * saturation;
        var p = 2 * lightness - q;

        return Color.FromRgb(
            ToRgbChannel(p, q, hue + 1.0 / 3.0),
            ToRgbChannel(p, q, hue),
            ToRgbChannel(p, q, hue - 1.0 / 3.0));
    }

    private static byte ToRgbChannel(double p, double q, double value)
    {
        if (value < 0) value += 1;
        if (value > 1) value -= 1;

        double result;
        if (value < 1.0 / 6.0) result = p + (q - p) * 6 * value;
        else if (value < 1.0 / 2.0) result = q;
        else if (value < 2.0 / 3.0) result = p + (q - p) * (2.0 / 3.0 - value) * 6;
        else result = p;

        return (byte)Math.Clamp(result * 255, 0, 255);
    }

    private sealed record HslColor(double Hue, double Saturation, double Lightness);
    private sealed record IconAnalysis(bool IsFullBleed, bool IsRoundedSquare, bool NeedsBacking, Color BackingColor, Int32Rect ContentBounds);
}

public sealed record IconAppearance(ImageSource? Source, double Size);
