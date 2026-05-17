using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace minimal_switcher;

public static class IconAppearanceService
{
    private const int AlgorithmVersion = 9;
    private const int OutputSize = 128;
    private const double DefaultIconSize = 68;
    private const double OrganicIconSize = 88;
    private const double RoundedSquareIconSize = 88;
    private const double FullBleedIconSize = 88;
    private const double BackingInset = 4;
    private const double BackingRadius = 20;
    private const double BackingIconInset = 20;
    private const double UnifiedClipRadius = 22;

    public static string SettingsKey
    {
        get
        {
            var settings = AppSettingsService.Instance.Current;
            return $"{AlgorithmVersion}:{settings.IconTreatmentMode}:{settings.IconTintColor}:{settings.IconTintStrength}";
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
            var destination = ShouldFillUnifiedFrame(analysis, settings)
                ? CreateOverscanRect()
                : FitUniform(
                    crop.PixelWidth,
                    crop.PixelHeight,
                    new Rect(iconInset, iconInset, OutputSize - iconInset * 2, OutputSize - iconInset * 2));

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                if (settings.IconTreatmentMode == IconTreatmentMode.Unified && analysis.NeedsBacking)
                {
                    var backing = new SolidColorBrush(analysis.BackingColor);
                    backing.Freeze();
                    var rect = new Rect(
                        BackingInset,
                        BackingInset,
                        OutputSize - BackingInset * 2,
                        OutputSize - BackingInset * 2);
                    context.DrawRoundedRectangle(backing, null, rect, BackingRadius, BackingRadius);
                }

                context.DrawImage(crop, destination);
            }

            var render = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
            render.Render(visual);
            render.Freeze();

            BitmapSource image = settings.IconTintStrength <= 0
                ? render
                : RemapPalette(render, settings.IconTintColor, settings.IconTintStrength);

            if (settings.IconTreatmentMode == IconTreatmentMode.Unified)
            {
                image = ClipToUnifiedFrame(image);
            }

            return new IconAppearance(image, GetIconSize(analysis, settings));
        }
        catch
        {
            return new IconAppearance(source, DefaultIconSize);
        }
    }

    public static IconDebugInfo? AnalyzeForDebug(ImageSource? source)
    {
        if (source is not BitmapSource bitmapSource) return null;

        var bitmap = EnsureBgra32(bitmapSource);
        var analysis = Analyze(bitmap);
        var processed = Apply(source);

        return new IconDebugInfo(
            bitmap,
            CreateMaskBitmap(bitmap.PixelWidth, bitmap.PixelHeight, analysis),
            CreateResidualBitmap(bitmap, analysis),
            processed.Source,
            analysis.IsFullBleed,
            analysis.IsRoundedSquare,
            analysis.NeedsBacking,
            analysis.FillRatio,
            analysis.CanvasRatio,
            analysis.CornerOpacity,
            analysis.EdgeOpacity,
            analysis.RoundedFit.MissingInside,
            analysis.RoundedFit.OutsideLeak,
            analysis.HasSmallInnerContent,
            analysis.ContentBounds,
            processed.Size);
    }

    private static double GetRenderedInset(IconAnalysis analysis, AppSettings settings)
    {
        if (settings.IconTreatmentMode != IconTreatmentMode.Unified) return 0;
        if (analysis.IsFullBleed) return 0;
        if (analysis.IsRoundedSquare) return 0;
        if (analysis.HasSmallInnerContent) return 10;
        if (analysis.NeedsBacking) return BackingIconInset;
        return 6;
    }

    private static double GetIconSize(IconAnalysis analysis, AppSettings settings)
    {
        if (settings.IconTreatmentMode != IconTreatmentMode.Unified) return DefaultIconSize;
        if (analysis.IsFullBleed) return FullBleedIconSize;
        if (analysis.IsRoundedSquare) return RoundedSquareIconSize;
        return OrganicIconSize;
    }

    private static bool ShouldFillUnifiedFrame(IconAnalysis analysis, AppSettings settings)
    {
        return settings.IconTreatmentMode == IconTreatmentMode.Unified
            && !analysis.NeedsBacking
            && (analysis.IsFullBleed || analysis.IsRoundedSquare);
    }

    private static Rect CreateOverscanRect()
    {
        const double overscan = 92.0 / 88.0;
        var size = OutputSize * overscan;
        var offset = (OutputSize - size) / 2;
        return new Rect(offset, offset, size, size);
    }

    private static BitmapSource ClipToUnifiedFrame(BitmapSource source)
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushClip(new RectangleGeometry(
                new Rect(0, 0, OutputSize, OutputSize),
                UnifiedClipRadius,
                UnifiedClipRadius));
            context.DrawImage(source, new Rect(0, 0, OutputSize, OutputSize));
            context.Pop();
        }

        var render = new RenderTargetBitmap(OutputSize, OutputSize, 96, 96, PixelFormats.Pbgra32);
        render.Render(visual);
        render.Freeze();
        return render;
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
            var emptyBounds = new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight);
            return new IconAnalysis(false, false, false, Color.FromRgb(28, 32, 40), emptyBounds, new ShapeFit(1, 1, 1, 0), 0, 0, 0, 0, false);
        }

        var boundsWidth = maxX - minX + 1;
        var boundsHeight = maxY - minY + 1;
        var fillRatio = opaque / (double)(boundsWidth * boundsHeight);
        var canvasRatio = Math.Max(boundsWidth / (double)source.PixelWidth, boundsHeight / (double)source.PixelHeight);
        var aspectRatio = boundsWidth / (double)boundsHeight;
        var squareAspect = aspectRatio is >= 0.86 and <= 1.14;
        var cornerOpacity = GetCornerOpacityRatio(pixels, stride, source.PixelWidth, source.PixelHeight);
        var edgeOpacity = GetEdgeOpacityRatio(pixels, stride, source.PixelWidth, source.PixelHeight);
        var roundedFit = GetRoundedSquareFit(pixels, stride, minX, minY, boundsWidth, boundsHeight);
        var innerContent = GetInnerContentBounds(pixels, stride, source.PixelWidth, source.PixelHeight);
        var transparentCorners = cornerOpacity < 0.22;
        var circleLike = transparentCorners
            && fillRatio >= 0.68
            && fillRatio <= 0.84
            && edgeOpacity >= 0.56;
        var fullBleed = canvasRatio >= 0.9
            && fillRatio >= 0.84
            && roundedFit.MissingInside <= 0.03
            && roundedFit.OutsideLeak <= 0.04
            && squareAspect
            && !transparentCorners;
        var internalSquare = canvasRatio >= 0.45
            && fillRatio >= 0.78
            && roundedFit.MissingInside <= 0.035
            && roundedFit.OutsideLeak <= 0.04
            && squareAspect
            && !circleLike;
        var roundedSquare = (canvasRatio >= 0.78
            && fillRatio >= 0.58
            && edgeOpacity >= 0.48
            && roundedFit.MissingInside <= 0.035
            && roundedFit.OutsideLeak <= 0.04
            && squareAspect
            && !circleLike)
            || internalSquare;
        var organicShape = circleLike || (transparentCorners && fillRatio < 0.84);
        var rescueContent = fillRatio <= 0.36 && roundedFit.MissingInside >= 0.34
            ? GetSalientContentBounds(pixels, stride, source.PixelWidth, source.PixelHeight)
            : null;
        var contentForCrop = IsSmallInnerContent(innerContent, source.PixelWidth, source.PixelHeight)
            ? innerContent
            : rescueContent;
        var hasSmallInnerContent = IsSmallInnerContent(contentForCrop, source.PixelWidth, source.PixelHeight);
        var needsBacking = organicShape
            || hasSmallInnerContent
            || !squareAspect
            || (!fullBleed && !roundedSquare && (fillRatio < 0.82 || canvasRatio < 0.84));

        var dominant = Color.FromRgb((byte)(red / opaque), (byte)(green / opaque), (byte)(blue / opaque));
        var crop = hasSmallInnerContent && contentForCrop.HasValue
            ? ExpandRect(contentForCrop.Value, source.PixelWidth, source.PixelHeight, 4)
            : ExpandRect(new Int32Rect(minX, minY, boundsWidth, boundsHeight), source.PixelWidth, source.PixelHeight, needsBacking ? 2 : 0);

        return new IconAnalysis(
            fullBleed,
            roundedSquare,
            needsBacking,
            CreateBackingColor(dominant),
            crop,
            roundedFit,
            fillRatio,
            canvasRatio,
            cornerOpacity,
            edgeOpacity,
            hasSmallInnerContent);
    }

    private static Int32Rect ExpandRect(Int32Rect rect, int width, int height, int padding)
    {
        var x = Math.Max(0, rect.X - padding);
        var y = Math.Max(0, rect.Y - padding);
        return new Int32Rect(
            x,
            y,
            Math.Min(width - x, rect.Width + padding * 2),
            Math.Min(height - y, rect.Height + padding * 2));
    }

    private static bool IsSmallInnerContent(Int32Rect? rect, int width, int height)
    {
        if (!rect.HasValue) return false;

        var bounds = rect.Value;
        var maxRatio = Math.Max(bounds.Width / (double)width, bounds.Height / (double)height);
        var minRatio = Math.Min(bounds.Width / (double)width, bounds.Height / (double)height);
        return maxRatio is >= 0.02 and <= 0.50 && minRatio >= 0.02;
    }

    private static Int32Rect? GetInnerContentBounds(byte[] pixels, int stride, int width, int height)
    {
        var background = EstimateEdgeColor(pixels, stride, width, height);
        if (!background.HasValue) return null;
        if (!LooksLikePaddingBackground(background.Value)) return null;

        var minX = width;
        var minY = height;
        var maxX = 0;
        var maxY = 0;
        var count = 0;

        var margin = Math.Max(4, Math.Min(width, height) / 10);

        for (var y = margin; y < height - margin; y++)
        {
            for (var x = margin; x < width - margin; x++)
            {
                var index = y * stride + x * 4;
                if (pixels[index + 3] < 24) continue;
                if (ColorDistance(pixels[index + 2], pixels[index + 1], pixels[index], background.Value) < 30) continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                count++;
            }
        }

        if (count < 8) return null;

        return new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static Int32Rect? GetSalientContentBounds(byte[] pixels, int stride, int width, int height)
    {
        var background = EstimateInteriorBackgroundColor(pixels, stride, width, height)
            ?? EstimateEdgeColor(pixels, stride, width, height);
        if (!background.HasValue) return null;

        var backgroundColor = background.Value;
        var backgroundLuma = GetLuma(backgroundColor.R, backgroundColor.G, backgroundColor.B);
        var backgroundChroma = GetChroma(backgroundColor.R, backgroundColor.G, backgroundColor.B);
        var margin = Math.Max(4, Math.Min(width, height) / 9);
        var minX = width;
        var minY = height;
        var maxX = 0;
        var maxY = 0;
        var count = 0;

        for (var y = margin; y < height - margin; y++)
        {
            for (var x = margin; x < width - margin; x++)
            {
                var index = y * stride + x * 4;
                if (pixels[index + 3] < 40) continue;

                var blue = pixels[index];
                var green = pixels[index + 1];
                var red = pixels[index + 2];
                var distance = ColorDistance(red, green, blue, backgroundColor);
                var lumaDelta = Math.Abs(GetLuma(red, green, blue) - backgroundLuma);
                var chromaDelta = GetChroma(red, green, blue) - backgroundChroma;

                if (distance < 42 && lumaDelta < 28 && chromaDelta < 24) continue;
                if (GetLuma(red, green, blue) <= 36 && GetChroma(red, green, blue) <= 22) continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                count++;
            }
        }

        if (count < 8) return null;

        var rect = new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        var maxRatio = Math.Max(rect.Width / (double)width, rect.Height / (double)height);
        var minRatio = Math.Min(rect.Width / (double)width, rect.Height / (double)height);
        return maxRatio is >= 0.04 and <= 0.64 && minRatio >= 0.04
            ? rect
            : null;
    }

    private static Color? EstimateEdgeColor(byte[] pixels, int stride, int width, int height)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        var count = 0;
        var edge = Math.Max(3, width / 14);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (x >= edge && x < width - edge && y >= edge && y < height - edge) continue;

                var index = y * stride + x * 4;
                if (pixels[index + 3] < 180) continue;

                blue += pixels[index];
                green += pixels[index + 1];
                red += pixels[index + 2];
                count++;
            }
        }

        if (count < 24) return null;

        return Color.FromRgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    private static Color? EstimateInteriorBackgroundColor(byte[] pixels, int stride, int width, int height)
    {
        long red = 0;
        long green = 0;
        long blue = 0;
        var count = 0;
        var margin = Math.Max(4, Math.Min(width, height) / 6);

        for (var y = margin; y < height - margin; y++)
        {
            for (var x = margin; x < width - margin; x++)
            {
                var index = y * stride + x * 4;
                if (pixels[index + 3] < 180) continue;

                var pixelBlue = pixels[index];
                var pixelGreen = pixels[index + 1];
                var pixelRed = pixels[index + 2];
                var luma = GetLuma(pixelRed, pixelGreen, pixelBlue);
                var chroma = GetChroma(pixelRed, pixelGreen, pixelBlue);
                if (luma > 74 || chroma > 34) continue;

                red += pixelRed;
                green += pixelGreen;
                blue += pixelBlue;
                count++;
            }
        }

        if (count < 24) return null;

        return Color.FromRgb((byte)(red / count), (byte)(green / count), (byte)(blue / count));
    }

    private static bool LooksLikePaddingBackground(Color color)
    {
        var luma = GetLuma(color.R, color.G, color.B);
        var chroma = GetChroma(color.R, color.G, color.B);

        return chroma <= 40 || luma <= 48 || luma >= 210;
    }

    private static double GetLuma(byte red, byte green, byte blue)
    {
        return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    }

    private static int GetChroma(byte red, byte green, byte blue)
    {
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        return max - min;
    }

    private static double ColorDistance(byte red, byte green, byte blue, Color background)
    {
        var dr = red - background.R;
        var dg = green - background.G;
        var db = blue - background.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static ShapeFit GetRoundedSquareFit(byte[] pixels, int stride, int startX, int startY, int width, int height)
    {
        var best = new ShapeFit(1, 1, 1, 0);
        var scales = new[] { 1.0, 0.96, 0.92 };
        var radiusFactors = new[] { 0.06, 0.16, 0.26 };

        foreach (var scale in scales)
        {
            foreach (var radiusFactor in radiusFactors)
            {
                var fit = GetMaskFit(pixels, stride, startX, startY, width, height, scale, radiusFactor);
                if (fit.TotalError < best.TotalError)
                {
                    best = fit;
                }
            }
        }

        return best;
    }

    private static Rect FitUniform(double sourceWidth, double sourceHeight, Rect bounds)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        var scale = Math.Min(bounds.Width / sourceWidth, bounds.Height / sourceHeight);
        var width = sourceWidth * scale;
        var height = sourceHeight * scale;
        return new Rect(
            bounds.X + (bounds.Width - width) * 0.5,
            bounds.Y + (bounds.Height - height) * 0.5,
            width,
            height);
    }

    private static ShapeFit GetMaskFit(
        byte[] pixels,
        int stride,
        int startX,
        int startY,
        int width,
        int height,
        double scale,
        double radiusFactor)
    {
        var maskWidth = width * scale;
        var maskHeight = height * scale;
        var offsetX = (width - maskWidth) * 0.5;
        var offsetY = (height - maskHeight) * 0.5;
        var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * radiusFactor);
        var maskPixels = 0;
        var missingInside = 0;
        var visibleOutside = 0;
        var visibleTotal = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var isVisible = pixels[(startY + y) * stride + (startX + x) * 4 + 3] >= 24;
                var inMask = IsInsideRoundedRect(
                    x + 0.5 - offsetX,
                    y + 0.5 - offsetY,
                    maskWidth,
                    maskHeight,
                    radius);

                if (isVisible) visibleTotal++;
                if (inMask)
                {
                    maskPixels++;
                    if (!isVisible) missingInside++;
                }
                else if (isVisible)
                {
                    visibleOutside++;
                }
            }
        }

        if (maskPixels == 0 || visibleTotal == 0)
        {
            return new ShapeFit(1, 1, scale, radiusFactor);
        }

        return new ShapeFit(
            missingInside / (double)maskPixels,
            visibleOutside / (double)visibleTotal,
            scale,
            radiusFactor);
    }

    private static bool IsInsideRoundedRect(double x, double y, double width, double height, double radius)
    {
        var innerLeft = radius;
        var innerRight = width - radius;
        var innerTop = radius;
        var innerBottom = height - radius;

        if ((x >= innerLeft && x <= innerRight) || (y >= innerTop && y <= innerBottom))
        {
            return true;
        }

        var centerX = x < innerLeft ? innerLeft : innerRight;
        var centerY = y < innerTop ? innerTop : innerBottom;
        var dx = x - centerX;
        var dy = y - centerY;
        return dx * dx + dy * dy <= radius * radius;
    }

    private static BitmapSource CreateMaskBitmap(int width, int height, IconAnalysis analysis)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        var bounds = analysis.ContentBounds;
        var maskWidth = bounds.Width * analysis.RoundedFit.Scale;
        var maskHeight = bounds.Height * analysis.RoundedFit.Scale;
        var offsetX = (bounds.Width - maskWidth) * 0.5;
        var offsetY = (bounds.Height - maskHeight) * 0.5;
        var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * analysis.RoundedFit.RadiusFactor);

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var inMask = IsInsideRoundedRect(
                    x - bounds.X + 0.5 - offsetX,
                    y - bounds.Y + 0.5 - offsetY,
                    maskWidth,
                    maskHeight,
                    radius);
                if (!inMask) continue;

                var index = y * stride + x * 4;
                pixels[index] = 255;
                pixels[index + 1] = 255;
                pixels[index + 2] = 255;
                pixels[index + 3] = 150;
            }
        }

        return CreateFrozenBitmap(width, height, pixels, stride);
    }

    private static BitmapSource CreateResidualBitmap(BitmapSource source, IconAnalysis analysis)
    {
        var width = source.PixelWidth;
        var height = source.PixelHeight;
        var stride = width * 4;
        var sourcePixels = new byte[stride * height];
        var pixels = new byte[stride * height];
        source.CopyPixels(sourcePixels, stride, 0);

        var bounds = analysis.ContentBounds;
        var maskWidth = bounds.Width * analysis.RoundedFit.Scale;
        var maskHeight = bounds.Height * analysis.RoundedFit.Scale;
        var offsetX = (bounds.Width - maskWidth) * 0.5;
        var offsetY = (bounds.Height - maskHeight) * 0.5;
        var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * analysis.RoundedFit.RadiusFactor);

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
        {
            for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
            {
                var inMask = IsInsideRoundedRect(
                    x - bounds.X + 0.5 - offsetX,
                    y - bounds.Y + 0.5 - offsetY,
                    maskWidth,
                    maskHeight,
                    radius);
                var index = y * stride + x * 4;
                var isVisible = sourcePixels[index + 3] >= 24;

                if (inMask && !isVisible)
                {
                    pixels[index] = 64;
                    pixels[index + 1] = 64;
                    pixels[index + 2] = 255;
                    pixels[index + 3] = 210;
                }
                else if (!inMask && isVisible)
                {
                    pixels[index] = 255;
                    pixels[index + 1] = 170;
                    pixels[index + 2] = 0;
                    pixels[index + 3] = 230;
                }
                else if (isVisible)
                {
                    pixels[index] = sourcePixels[index];
                    pixels[index + 1] = sourcePixels[index + 1];
                    pixels[index + 2] = sourcePixels[index + 2];
                    pixels[index + 3] = 120;
                }
            }
        }

        return CreateFrozenBitmap(width, height, pixels, stride);
    }

    private static BitmapSource CreateFrozenBitmap(int width, int height, byte[] pixels, int stride)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
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
        if (analysis.IsFullBleed && !analysis.HasSmallInnerContent) return source;

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
    private sealed record ShapeFit(double MissingInside, double OutsideLeak, double Scale, double RadiusFactor)
    {
        public double TotalError => MissingInside + OutsideLeak * 1.8;
    }
    private sealed record IconAnalysis(
        bool IsFullBleed,
        bool IsRoundedSquare,
        bool NeedsBacking,
        Color BackingColor,
        Int32Rect ContentBounds,
        ShapeFit RoundedFit,
        double FillRatio,
        double CanvasRatio,
        double CornerOpacity,
        double EdgeOpacity,
        bool HasSmallInnerContent);
}

public sealed record IconAppearance(ImageSource? Source, double Size);

public sealed record IconDebugInfo(
    ImageSource Normalized,
    ImageSource Mask,
    ImageSource Residual,
    ImageSource? Processed,
    bool IsFullBleed,
    bool IsRoundedSquare,
    bool NeedsBacking,
    double FillRatio,
    double CanvasRatio,
    double CornerOpacity,
    double EdgeOpacity,
    double MissingInside,
    double OutsideLeak,
    bool HasSmallInnerContent,
    Int32Rect ContentBounds,
    double IconSize);
