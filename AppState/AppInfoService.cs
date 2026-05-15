using System;
using System.Diagnostics;
using System.Reflection;

namespace minimal_switcher;

public sealed class AppInfoService
{
    private readonly Assembly _assembly = typeof(AppInfoService).Assembly;

    public static AppInfoService Instance { get; } = new();

    private AppInfoService()
    {
    }

    public string Name => GetAttribute<AssemblyProductAttribute>()?.Product ?? "Minimal Switcher";

    public string Version =>
        _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? _assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    public string RepositoryUrl =>
        GetMetadataAttribute("RepositoryUrl")?.Value
        ?? "https://github.com/wv357/minimal-switcher";

    public string ReleasesUrl => $"{RepositoryUrl.TrimEnd('/')}/releases";

    public string ChangelogUrl => $"{RepositoryUrl.TrimEnd('/')}/releases/latest";

    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private T? GetAttribute<T>() where T : Attribute => _assembly.GetCustomAttribute<T>();

    private AssemblyMetadataAttribute? GetMetadataAttribute(string key)
    {
        foreach (var attribute in _assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key == key)
            {
                return attribute;
            }
        }

        return null;
    }
}
