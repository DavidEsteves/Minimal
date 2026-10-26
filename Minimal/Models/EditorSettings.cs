namespace Minimal.Models;

public sealed record EditorSettings
{
    public string FontFamily { get; init; } = "Cascadia Mono, Consolas";
    public double FontSize { get; init; } = 15;
    public double LineSpacing { get; init; } = 1;

    public EditorSettings Normalize() => this with
    {
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) || FontFamily.Length > 128 || FontFamily.IndexOfAny([':', '/', '\\', '#']) >= 0
            ? new EditorSettings().FontFamily : FontFamily.Trim(),
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 8, 96) : 15,
        LineSpacing = double.IsFinite(LineSpacing) ? Math.Clamp(LineSpacing, 1, 3) : 1
    };
}
