namespace Minimal.Models;

public sealed record EditorSettings
{
    public string FontFamily { get; init; } = "Cascadia Mono, Consolas";
    public double FontSize { get; init; } = 15;
    public double LineSpacing { get; init; } = 1;
    public bool CodeFolding { get; init; } = true;
    public bool ShowLineNumbers { get; init; }
    public bool RestorePreviousSession { get; init; } = true;
    public bool ConvertTabsToSpaces { get; init; }
    public int IndentationSize { get; init; } = 4;

    public EditorSettings Normalize() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 128 || FontFamily.IndexOfAny([':', '/', '\\', '#']) >= 0
            ? new EditorSettings().FontFamily : FontFamily.Trim(),
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 8, 96) : 15,
        LineSpacing = double.IsFinite(LineSpacing) ? Math.Clamp(LineSpacing, 1, 3) : 1,
        IndentationSize = Math.Clamp(IndentationSize, 1, 8)
    };
}
