#:property TargetFramework=net10.0-windows
#:property EnableWindowsTargeting=true
#:property BuiltInComInteropSupport=true
#:package System.Drawing.Common@10.0.0

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

const int OutputSize = 128;
const byte AlphaThreshold = 24;

if (args.Length == 0)
{
    Console.WriteLine("Usage: dotnet run tools/icon-debug/icon-debug.cs -- <image-or-exe-path> [output-dir]");
    return;
}

var inputPath = Path.GetFullPath(args[0]);
var outputDir = Path.GetFullPath(args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "icon-debug-output"));
Directory.CreateDirectory(outputDir);

using var source = LoadBitmap(inputPath);
using var normalized = ResizeToBgra(source, OutputSize, OutputSize);
var analysis = Analyze(normalized);
using var mask = CreateMaskBitmap(normalized.Width, normalized.Height, analysis);
using var residual = CreateResidualBitmap(normalized, analysis);

var normalizedPath = Path.Combine(outputDir, "normalized.png");
var maskPath = Path.Combine(outputDir, "mask.png");
var residualPath = Path.Combine(outputDir, "residual.png");
var reportPath = Path.Combine(outputDir, "report.html");

normalized.Save(normalizedPath, ImageFormat.Png);
mask.Save(maskPath, ImageFormat.Png);
residual.Save(residualPath, ImageFormat.Png);
File.WriteAllText(reportPath, CreateReport(inputPath, analysis), Encoding.UTF8);

Console.WriteLine($"Decision: {analysis.Decision}");
Console.WriteLine($"Report:   {reportPath}");
Console.WriteLine($"Images:   {outputDir}");

static Bitmap LoadBitmap(string path)
{
    var extension = Path.GetExtension(path);
    if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
    {
        return LoadShellIcon(path, 256)
            ?? throw new InvalidOperationException("No shell icon was found.");
    }

    using var image = Image.FromFile(path);
    return new Bitmap(image);
}

static Bitmap? LoadShellIcon(string path, int size)
{
    nint hBitmap = 0;
    object? shellItem = null;

    try
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        var hr = SHCreateItemFromParsingName(path, 0, ref iid, out shellItem);
        if (hr != 0 || shellItem is not IShellItemImageFactory factory) return null;

        var requested = new SIZE(size, size);
        hr = factory.GetImage(requested, SIIGBF.ICONONLY, out hBitmap);
        if (hr != 0 || hBitmap == 0) return null;

        using var shellBitmap = Image.FromHbitmap(hBitmap);
        return new Bitmap(shellBitmap);
    }
    finally
    {
        if (hBitmap != 0) DeleteObject(hBitmap);
        if (shellItem != null) Marshal.ReleaseComObject(shellItem);
    }
}

static Bitmap ResizeToBgra(Image source, int width, int height)
{
    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Transparent);
    graphics.SmoothingMode = SmoothingMode.AntiAlias;
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    graphics.DrawImage(source, new Rectangle(0, 0, width, height));
    return bitmap;
}

static IconAnalysis Analyze(Bitmap bitmap)
{
    var minX = bitmap.Width;
    var minY = bitmap.Height;
    var maxX = 0;
    var maxY = 0;
    var opaque = 0;

    for (var y = 0; y < bitmap.Height; y++)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            if (bitmap.GetPixel(x, y).A < AlphaThreshold) continue;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            opaque++;
        }
    }

    if (opaque == 0)
    {
        return new IconAnalysis("Empty", new Rectangle(0, 0, bitmap.Width, bitmap.Height), new ShapeFit(1, 1, 1, 0), 0, 0, 0, 0, false, false, true);
    }

    var bounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    var fillRatio = opaque / (double)(bounds.Width * bounds.Height);
    var canvasRatio = Math.Max(bounds.Width / (double)bitmap.Width, bounds.Height / (double)bitmap.Height);
    var cornerOpacity = GetCornerOpacityRatio(bitmap);
    var edgeOpacity = GetEdgeOpacityRatio(bitmap);
    var roundedFit = GetRoundedSquareFit(bitmap, bounds);
    var transparentCorners = cornerOpacity < 0.22;
    var circleLike = transparentCorners
        && fillRatio >= 0.68
        && fillRatio <= 0.84
        && edgeOpacity >= 0.56;
    var fullBleed = canvasRatio >= 0.9 && fillRatio >= 0.84 && !transparentCorners;
    var roundedSquare = canvasRatio >= 0.78
        && fillRatio >= 0.58
        && edgeOpacity >= 0.48
        && roundedFit.MissingInside <= 0.07
        && roundedFit.OutsideLeak <= 0.04
        && !circleLike;
    var organic = circleLike || (transparentCorners && fillRatio < 0.84);
    var needsBacking = organic || (!fullBleed && !roundedSquare && (fillRatio < 0.82 || canvasRatio < 0.84));
    var decision = fullBleed ? "Full square" : roundedSquare ? "Rounded-square" : needsBacking ? "Organic / backing" : "Compact";

    return new IconAnalysis(decision, bounds, roundedFit, fillRatio, canvasRatio, cornerOpacity, edgeOpacity, fullBleed, roundedSquare, needsBacking);
}

static ShapeFit GetRoundedSquareFit(Bitmap bitmap, Rectangle bounds)
{
    var best = new ShapeFit(1, 1, 1, 0);
    var scales = new[] { 1.0, 0.96, 0.92 };
    var radiusFactors = new[] { 0.06, 0.16, 0.26 };

    foreach (var scale in scales)
    {
        foreach (var radiusFactor in radiusFactors)
        {
            var fit = GetMaskFit(bitmap, bounds, scale, radiusFactor);
            if (fit.TotalError < best.TotalError)
            {
                best = fit;
            }
        }
    }

    return best;
}

static ShapeFit GetMaskFit(Bitmap bitmap, Rectangle bounds, double scale, double radiusFactor)
{
    var maskWidth = bounds.Width * scale;
    var maskHeight = bounds.Height * scale;
    var offsetX = (bounds.Width - maskWidth) * 0.5;
    var offsetY = (bounds.Height - maskHeight) * 0.5;
    var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * radiusFactor);
    var maskPixels = 0;
    var missingInside = 0;
    var visibleOutside = 0;
    var visibleTotal = 0;

    for (var y = 0; y < bounds.Height; y++)
    {
        for (var x = 0; x < bounds.Width; x++)
        {
            var visible = bitmap.GetPixel(bounds.X + x, bounds.Y + y).A >= AlphaThreshold;
            var inMask = IsInsideRoundedRect(x + 0.5 - offsetX, y + 0.5 - offsetY, maskWidth, maskHeight, radius);

            if (visible) visibleTotal++;
            if (inMask)
            {
                maskPixels++;
                if (!visible) missingInside++;
            }
            else if (visible)
            {
                visibleOutside++;
            }
        }
    }

    if (maskPixels == 0 || visibleTotal == 0)
    {
        return new ShapeFit(1, 1, scale, radiusFactor);
    }

    return new ShapeFit(missingInside / (double)maskPixels, visibleOutside / (double)visibleTotal, scale, radiusFactor);
}

static Bitmap CreateMaskBitmap(int width, int height, IconAnalysis analysis)
{
    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    var bounds = analysis.Bounds;
    var maskWidth = bounds.Width * analysis.RoundedFit.Scale;
    var maskHeight = bounds.Height * analysis.RoundedFit.Scale;
    var offsetX = (bounds.Width - maskWidth) * 0.5;
    var offsetY = (bounds.Height - maskHeight) * 0.5;
    var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * analysis.RoundedFit.RadiusFactor);

    using var graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.Transparent);
    using var brush = new SolidBrush(Color.FromArgb(150, 255, 255, 255));

    for (var y = 0; y < bounds.Height; y++)
    {
        for (var x = 0; x < bounds.Width; x++)
        {
            if (IsInsideRoundedRect(x + 0.5 - offsetX, y + 0.5 - offsetY, maskWidth, maskHeight, radius))
            {
                bitmap.SetPixel(bounds.X + x, bounds.Y + y, Color.FromArgb(150, 255, 255, 255));
            }
        }
    }

    return bitmap;
}

static Bitmap CreateResidualBitmap(Bitmap source, IconAnalysis analysis)
{
    var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
    var bounds = analysis.Bounds;
    var maskWidth = bounds.Width * analysis.RoundedFit.Scale;
    var maskHeight = bounds.Height * analysis.RoundedFit.Scale;
    var offsetX = (bounds.Width - maskWidth) * 0.5;
    var offsetY = (bounds.Height - maskHeight) * 0.5;
    var radius = Math.Max(2.0, Math.Min(maskWidth, maskHeight) * analysis.RoundedFit.RadiusFactor);

    for (var y = 0; y < bounds.Height; y++)
    {
        for (var x = 0; x < bounds.Width; x++)
        {
            var sourcePixel = source.GetPixel(bounds.X + x, bounds.Y + y);
            var visible = sourcePixel.A >= AlphaThreshold;
            var inMask = IsInsideRoundedRect(x + 0.5 - offsetX, y + 0.5 - offsetY, maskWidth, maskHeight, radius);

            if (inMask && !visible)
            {
                bitmap.SetPixel(bounds.X + x, bounds.Y + y, Color.FromArgb(210, 255, 64, 64));
            }
            else if (!inMask && visible)
            {
                bitmap.SetPixel(bounds.X + x, bounds.Y + y, Color.FromArgb(230, 0, 170, 255));
            }
            else if (visible)
            {
                bitmap.SetPixel(bounds.X + x, bounds.Y + y, Color.FromArgb(120, sourcePixel.R, sourcePixel.G, sourcePixel.B));
            }
        }
    }

    return bitmap;
}

static double GetCornerOpacityRatio(Bitmap bitmap)
{
    var sample = Math.Max(6, bitmap.Width / 8);
    var opaque = 0;
    var total = 0;

    AddRegion(0, 0, sample, sample);
    AddRegion(bitmap.Width - sample, 0, sample, sample);
    AddRegion(0, bitmap.Height - sample, sample, sample);
    AddRegion(bitmap.Width - sample, bitmap.Height - sample, sample, sample);
    return total == 0 ? 0 : opaque / (double)total;

    void AddRegion(int startX, int startY, int regionWidth, int regionHeight)
    {
        var (regionOpaque, regionTotal) = CountOpaqueRegion(bitmap, startX, startY, regionWidth, regionHeight);
        opaque += regionOpaque;
        total += regionTotal;
    }
}

static double GetEdgeOpacityRatio(Bitmap bitmap)
{
    var sample = Math.Max(6, bitmap.Width / 12);
    var opaque = 0;
    var total = 0;

    AddRegion(bitmap.Width / 2 - sample / 2, 0, sample, sample);
    AddRegion(bitmap.Width / 2 - sample / 2, bitmap.Height - sample, sample, sample);
    AddRegion(0, bitmap.Height / 2 - sample / 2, sample, sample);
    AddRegion(bitmap.Width - sample, bitmap.Height / 2 - sample / 2, sample, sample);
    return total == 0 ? 0 : opaque / (double)total;

    void AddRegion(int startX, int startY, int regionWidth, int regionHeight)
    {
        var (regionOpaque, regionTotal) = CountOpaqueRegion(bitmap, startX, startY, regionWidth, regionHeight);
        opaque += regionOpaque;
        total += regionTotal;
    }
}

static (int opaque, int total) CountOpaqueRegion(Bitmap bitmap, int startX, int startY, int regionWidth, int regionHeight)
{
    var opaque = 0;
    var total = 0;

    for (var y = startY; y < startY + regionHeight; y++)
    {
        for (var x = startX; x < startX + regionWidth; x++)
        {
            total++;
            if (bitmap.GetPixel(x, y).A >= AlphaThreshold) opaque++;
        }
    }

    return (opaque, total);
}

static bool IsInsideRoundedRect(double x, double y, double width, double height, double radius)
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

static string CreateReport(string inputPath, IconAnalysis analysis)
{
    var metrics = new Dictionary<string, string>
    {
        ["Decision"] = analysis.Decision,
        ["FillRatio"] = analysis.FillRatio.ToString("0.000", CultureInfo.InvariantCulture),
        ["CanvasRatio"] = analysis.CanvasRatio.ToString("0.000", CultureInfo.InvariantCulture),
        ["CornerOpacity"] = analysis.CornerOpacity.ToString("0.000", CultureInfo.InvariantCulture),
        ["EdgeOpacity"] = analysis.EdgeOpacity.ToString("0.000", CultureInfo.InvariantCulture),
        ["MissingInside"] = analysis.RoundedFit.MissingInside.ToString("0.000", CultureInfo.InvariantCulture),
        ["OutsideLeak"] = analysis.RoundedFit.OutsideLeak.ToString("0.000", CultureInfo.InvariantCulture),
        ["MaskScale"] = analysis.RoundedFit.Scale.ToString("0.00", CultureInfo.InvariantCulture),
        ["MaskRadius"] = analysis.RoundedFit.RadiusFactor.ToString("0.00", CultureInfo.InvariantCulture),
        ["FullBleed"] = analysis.IsFullBleed.ToString(),
        ["RoundedSquare"] = analysis.IsRoundedSquare.ToString(),
        ["NeedsBacking"] = analysis.NeedsBacking.ToString()
    };

    var rows = string.Join("\n", metrics.Select(item => $"<tr><th>{WebUtility.HtmlEncode(item.Key)}</th><td>{WebUtility.HtmlEncode(item.Value)}</td></tr>"));
    return $$"""
    <!doctype html>
    <html>
    <head>
      <meta charset="utf-8">
      <title>Icon Debug</title>
      <style>
        body { margin: 0; padding: 28px; background: #0b0d10; color: #f5f6f8; font: 14px Segoe UI, sans-serif; }
        h1 { margin: 0 0 6px; font-size: 24px; }
        .path { color: #aeb3bc; margin-bottom: 24px; }
        .grid { display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: 18px; }
        .card { background: #0f1319; border: 1px solid #202633; border-radius: 18px; padding: 16px; }
        .image { display: grid; place-items: center; min-height: 210px; background: #080a0d; border-radius: 14px; margin-top: 12px; }
        img { width: 180px; height: 180px; image-rendering: pixelated; object-fit: contain; }
        table { border-collapse: collapse; width: 100%; margin-top: 12px; }
        th, td { padding: 8px 0; border-bottom: 1px solid #202633; text-align: left; }
        th { color: #aeb3bc; font-weight: 600; }
        .note { color: #aeb3bc; margin-top: 10px; }
      </style>
    </head>
    <body>
      <h1>Icon Debug</h1>
      <div class="path">{{WebUtility.HtmlEncode(inputPath)}}</div>
      <div class="grid">
        <div class="card"><b>Normalizado</b><div class="image"><img src="normalized.png"></div></div>
        <div class="card"><b>Máscara candidata</b><div class="image"><img src="mask.png"></div></div>
        <div class="card"><b>Residuo</b><div class="image"><img src="residual.png"></div><div class="note">Rojo: falta dentro. Azul: sobra fuera.</div></div>
      </div>
      <div class="card" style="margin-top:18px">
        <b>Métricas</b>
        <table>{{rows}}</table>
      </div>
    </body>
    </html>
    """;
}

[DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
static extern int SHCreateItemFromParsingName(
    string pszPath,
    nint pbc,
    ref Guid riid,
    [MarshalAs(UnmanagedType.Interface)] out object? ppv);

[DllImport("gdi32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool DeleteObject(nint hObject);

record ShapeFit(double MissingInside, double OutsideLeak, double Scale, double RadiusFactor)
{
    public double TotalError => MissingInside + OutsideLeak * 1.8;
}

record IconAnalysis(
    string Decision,
    Rectangle Bounds,
    ShapeFit RoundedFit,
    double FillRatio,
    double CanvasRatio,
    double CornerOpacity,
    double EdgeOpacity,
    bool IsFullBleed,
    bool IsRoundedSquare,
    bool NeedsBacking);

[StructLayout(LayoutKind.Sequential)]
readonly record struct SIZE(int cx, int cy);

[Flags]
enum SIIGBF
{
    ICONONLY = 0x00000004
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(SIZE size, SIIGBF flags, out nint phbm);
}
