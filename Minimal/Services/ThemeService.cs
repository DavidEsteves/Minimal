using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Minimal.Services;

public static class ThemeService
{
    public static bool Apply(string theme)
    {
        bool dark = theme == "Dark";
        if (theme == "System")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        Set("Surface", dark ? "#202020" : "#FFFFFF");
        Set("Chrome", dark ? "#292929" : "#F3F3F3");
        Set("Ink", dark ? "#E5E5E5" : "#202020");
        Set("Rule", dark ? "#444444" : "#DDDDDD");
        Set("TitleBar", dark ? "#1F1F1F" : "#F3F3F3");
        Set("CaptionInk", dark ? "#AAAAAA" : "#555555");
        Set("CaptionHover", dark ? "#333333" : "#E5E5E5");
        Application.Current.Resources["ScrollTrackColor"] = (Color)ColorConverter.ConvertFromString(dark ? "#202020" : "#FFFFFF");
        // Replace the brush itself: a shared Freezable containing a nested
        // DynamicResource can keep a stale color after changing themes.
        Set("ScrollTrack", dark ? "#202020" : "#FFFFFF");
        Set("ScrollThumbBrush", dark ? "#555555" : "#C2C2C2");
        Set("ScrollThumbHover", dark ? "#737373" : "#A6A6A6");
        Set("ScrollThumbPressed", dark ? "#909090" : "#8A8A8A");
        return dark;
    }
    private static void Set(string key, string color) => Application.Current.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
