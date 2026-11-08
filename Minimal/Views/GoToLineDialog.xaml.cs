using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Minimal.Views;

public partial class GoToLineDialog : Window
{
    private readonly int lineCount;
    public int LineNumber { get; private set; }
    public GoToLineDialog(int currentLine, int lineCount)
    {
        this.lineCount = lineCount;
        InitializeComponent();
        RangeLabel.Content = $"_Line number (1–{lineCount:N0})";
        LineText.Text = currentLine.ToString(CultureInfo.CurrentCulture);
        Loaded += (_, _) => { LineText.Focus(); LineText.SelectAll(); };
    }
    public static bool TryValidate(string text, int count, out int line) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out line) && line >= 1 && line <= count;
    private void LineChanged(object sender, TextChangedEventArgs e)
    {
        if (GoButton is null) return;
        GoButton.IsEnabled = TryValidate(LineText.Text, lineCount, out var line);
        LineNumber = line;
        Validation.Text = GoButton.IsEnabled ? "" : $"Enter a number from 1 to {lineCount:N0}.";
    }
    private void GoClick(object sender, RoutedEventArgs e) { if (GoButton.IsEnabled) DialogResult = true; }
}
