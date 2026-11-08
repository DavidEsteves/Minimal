using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Minimal.Models;
using Minimal.Services;

namespace Minimal.Views;

public partial class EditorSettingsDialog : Window
{
    private readonly Action<EditorSettings> preview;
    private bool ready;
    private EditorSettings selected;

    private EditorSettingsDialog(EditorSettings settings, Action<EditorSettings> preview)
    {
        this.preview = preview;
        selected = settings.Normalize();
        InitializeComponent();
        FontPicker.ItemsSource = Fonts.SystemFontFamilies.Select(font => font.Source).Append(selected.FontFamily).Append(new EditorSettings().FontFamily)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        SpacingPicker.ItemsSource = new[] { 1, 1.15, 1.25, 1.5, 1.75, 2, 2.5, 3 }.Append(selected.LineSpacing).Distinct().Order().ToArray();
        IndentPicker.ItemsSource = Enumerable.Range(1, 8).ToArray();
        SampleEditor.Text = "The quick brown fox jumps over the lazy dog.\n0123456789  Aa Bb Cc  {}[]()<>=\nA little more room between the lines.\nUnicode: café — 日本語";
        SetChoices(selected);
        ready = true;
        UpdatePreview();
    }

    public static EditorSettings? Show(Window owner, EditorSettings settings, Action<EditorSettings> preview)
    {
        var dialog = new EditorSettingsDialog(settings, preview) { Owner = owner, Icon = owner.Icon };
        return dialog.ShowDialog() == true ? dialog.selected : null;
    }

    private void SetChoices(EditorSettings settings)
    {
        FontPicker.SelectedItem = settings.FontFamily;
        SizeText.Text = (settings.FontSize * 72 / 96).ToString("0.##", CultureInfo.CurrentCulture);
        SpacingPicker.SelectedItem = settings.LineSpacing;
        SpacesChoice.IsChecked = settings.ConvertTabsToSpaces;
        IndentPicker.SelectedItem = settings.IndentationSize;
    }

    private void UpdatePreview()
    {
        if (!ready) return;
        bool valid = double.TryParse(SizeText.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var points) && double.IsFinite(points) && points >= 6 && points <= 72;
        ApplyButton.IsEnabled = valid;
        ValidationMessage.Text = valid ? "" : "Enter a font size from 6 to 72 pt.";
        if (!valid || FontPicker.SelectedItem is not string font || SpacingPicker.SelectedItem is not double spacing) return;
        selected = selected with { FontFamily = font, FontSize = points * 96 / 72, LineSpacing = spacing, ConvertTabsToSpaces = SpacesChoice.IsChecked == true, IndentationSize = IndentPicker.SelectedItem is int size ? size : 4 };
        SampleEditor.FontFamily = EditorTypography.CreateFont(selected);
        SampleEditor.FontSize = selected.FontSize;
        preview(selected);
    }

    private void ChoicesChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
    private void IndentationChanged(object sender, RoutedEventArgs e) => UpdatePreview();
    private void FontSizeTextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void ResetClick(object sender, RoutedEventArgs e) { ready = false; SetChoices(new()); ready = true; UpdatePreview(); }
    private void ApplyClick(object sender, RoutedEventArgs e) { if (ApplyButton.IsEnabled) DialogResult = true; }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void DragTitleBar(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
