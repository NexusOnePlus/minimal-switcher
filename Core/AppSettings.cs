namespace minimal_switcher;

public enum AppThemeMode
{
    Shader,
    Preset,
    Custom
}

public enum IconTreatmentMode
{
    Native,
    Unified
}

public sealed class AppSettings
{
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Shader;
    public string ThemePresetId { get; set; } = "Graphite";
    public string CustomBackgroundColor { get; set; } = "#000000";
    public int CustomBackgroundOpacity { get; set; } = 100;
    public IconTreatmentMode IconTreatmentMode { get; set; } = IconTreatmentMode.Native;
    public string IconTintColor { get; set; } = "#F5F6F8";
    public int IconTintStrength { get; set; } = 0;
}

public sealed record ThemePreset(
    string Id,
    string Name,
    string Background,
    string Border,
    string Overlay,
    string Accent);
