using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace minimal_switcher;

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public static AppSettingsService Instance { get; } = new();
    public AppSettings Current { get; private set; }

    public IReadOnlyList<ThemePreset> Presets { get; } =
    [
        new("Graphite", "Graphite", "#F0151517", "#33FFFFFF", "#00000000", "#E6FFFFFF"),
        new("Carbon", "Carbon", "#F0060709", "#2EFFFFFF", "#00000000", "#F5F6F8"),
        new("Midnight", "Midnight", "#F0091020", "#335B7CFA", "#00000000", "#8BA7FF"),
        new("Slate", "Slate", "#F01B2228", "#3388A0AF", "#00000000", "#C7D2DA"),
        new("Moss", "Moss", "#F00E1914", "#3370C69A", "#00000000", "#9ED8B4"),
        new("Signal", "Signal", "#F0101218", "#33F48C6C", "#00000000", "#F48C6C"),
        new("Aubergine", "Aubergine", "#F018111D", "#33D2A5FF", "#00000000", "#D2A5FF"),
        new("Warm", "Warm", "#F0221A14", "#33F4B183", "#00000000", "#F0B37E"),
        new("Paper Dark", "Paper Dark", "#F0191815", "#33D8CCB8", "#00000000", "#D8CCB8"),
        new("Terminal", "Terminal", "#F0050A07", "#3356D364", "#00000000", "#56D364")
    ];

    public event EventHandler? SettingsChanged;

    private AppSettingsService()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());

        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "minimal-switcher");

        _settingsPath = Path.Combine(directory, "settings.json");
        Current = Load(directory);
    }

    public ThemePreset CurrentPreset =>
        GetPreset(Current.ThemePresetId) ?? Presets[0];

    public ThemePreset? GetPreset(string id)
    {
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return preset;
            }
        }

        return null;
    }

    public void Update(AppSettings settings)
    {
        Current = Normalize(settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Normalize(settings ?? new AppSettings());
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
        }
    }

    private AppSettings Normalize(AppSettings settings)
    {
        var presetId = GetPreset(settings.ThemePresetId)?.Id ?? Presets[0].Id;
        var color = IsValidRgbHex(settings.CustomBackgroundColor)
            ? settings.CustomBackgroundColor.ToUpperInvariant()
            : "#000000";

        return new AppSettings
        {
            ThemeMode = settings.ThemeMode,
            ThemePresetId = presetId,
            CustomBackgroundColor = color,
            CustomBackgroundOpacity = Math.Clamp(settings.CustomBackgroundOpacity, 0, 100)
        };
    }

    private static bool IsValidRgbHex(string value)
    {
        if (value.Length != 7 || value[0] != '#') return false;

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            var isHex = c is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F';

            if (!isHex) return false;
        }

        return true;
    }
}
