using System.Windows.Media;
using Minimal.Models;

namespace Minimal.Services;

public static class EditorTypography
{
    public static FontFamily CreateFont(EditorSettings settings)
    {
        settings = settings.Normalize();
        var font = new FontFamily(settings.FontFamily);
        if (settings.LineSpacing == 1) return font;
        // A composite font supplies real line metrics to WPF's formatter, so
        // AvalonEdit's wrapping, hit testing, selections and caret use the same spacing.
        var spaced = new FontFamily
        {
            LineSpacing = font.LineSpacing * settings.LineSpacing,
            Baseline = font.Baseline + font.LineSpacing * (settings.LineSpacing - 1) / 2
        };
        spaced.FamilyMaps.Add(new FontFamilyMap { Unicode = "0000-10FFFF", Target = settings.FontFamily });
        return spaced;
    }
}
