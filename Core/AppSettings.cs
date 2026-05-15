namespace minimal_switcher;

public enum AppThemeMode
{
    Shader,
    Preset,
    Custom
}

public sealed class AppSettings
{
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Shader;
    public string ThemePresetId { get; set; } = "Graphite";
    public string CustomBackgroundColor { get; set; } = "#000000";
    public int CustomBackgroundOpacity { get; set; } = 100;
}

public sealed record ThemePreset(
    string Id,
    string Name,
    string Background,
    string Border,
    string Overlay,
    string Accent);
