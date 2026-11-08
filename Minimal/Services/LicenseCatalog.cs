using System.IO;
using System.Reflection;

namespace Minimal.Services;

public sealed record LicenseNotice(string Name, string? Version, string Terms, string Text)
{
    public string DisplayName => Name + (Version is null ? "" : " " + Version) + " — " + Terms;
}

public static class LicenseCatalog
{
    private static readonly Assembly Assembly = typeof(LicenseCatalog).Assembly;
    public static string ApplicationVersion => Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static IReadOnlyList<LicenseNotice> Load() => Array.AsReadOnly<LicenseNotice>(
    [
        new("AvalonEdit", Version("AvalonEdit"), "MIT", Read("AvalonEdit.txt")),
        new("Markdig", Version("Markdig"), "BSD-2-Clause", Read("Markdig.txt")),
        new("Microsoft WebView2 SDK", Version("WebView2"), "License", Read("WebView2.txt")),
        new("Microsoft WebView2 SDK", Version("WebView2"), "Third-party notices", Read("WebView2-NOTICE.txt"))
    ]);

    private static string Version(string component) => Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(attribute => attribute.Key == "DependencyVersion." + component).Value!;

    private static string Read(string name)
    {
        using var stream = Assembly.GetManifestResourceStream("Minimal.Assets.Licenses." + name)
            ?? throw new InvalidOperationException("Missing embedded license notice: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
