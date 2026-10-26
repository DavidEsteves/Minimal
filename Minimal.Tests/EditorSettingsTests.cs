using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using ICSharpCode.AvalonEdit;
using Minimal.Models;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class EditorSettingsTests
{
    [Fact]
    public async Task PreferencesRoundTripAndReplaceWithoutTemporaryFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Minimal-font-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new EditorSettingsService(Path.Combine(directory, "preferences.json"));
            Assert.Equal(new EditorSettings(), service.Load());
            var settings = new EditorSettings { FontFamily = "Consolas", FontSize = 20, LineSpacing = 1.5 };
            await service.SaveAsync(settings);
            Assert.Equal(settings, service.Load());
            var updated = settings with { FontFamily = "Segoe UI", LineSpacing = 2 };
            await service.SaveAsync(updated);
            Assert.Equal(updated, service.Load());
            Assert.Single(Directory.GetFiles(directory));
            await File.WriteAllTextAsync(Path.Combine(directory, "preferences.json"), "invalid json");
            Assert.Equal(new EditorSettings(), service.Load());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void InvalidPreferencesUseSafeFontNamesAndBoundedMetrics()
    {
        var defaults = new EditorSettings();
        var normalized = new EditorSettings { FontFamily = "https://example.com/font", FontSize = double.NaN, LineSpacing = double.PositiveInfinity }.Normalize();
        Assert.Equal(defaults, normalized);
        Assert.Equal(8, (defaults with { FontSize = -1 }).Normalize().FontSize);
        Assert.Equal(96, (defaults with { FontSize = 1000 }).Normalize().FontSize);
        Assert.Equal(1, (defaults with { LineSpacing = 0 }).Normalize().LineSpacing);
        Assert.Equal(3, (defaults with { LineSpacing = 10 }).Normalize().LineSpacing);
        Assert.Equal(defaults.FontFamily, (defaults with { FontFamily = null! }).Normalize().FontFamily);
    }

    [Theory]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void SpacingChangesActualLineMetricsWithoutChangingTextOrGlyphWidth(double spacing) => RunSta(() =>
    {
        var editor = new TextEditor { FontSize = 16, Text = "first line\nsecond line\nthird line" };
        var settings = new EditorSettings { FontFamily = "Consolas", FontSize = 16 };
        editor.FontFamily = EditorTypography.CreateFont(settings);
        editor.Document.UndoStack.MarkAsOriginalFile();
        editor.Select(1, 3);
        Layout(editor);
        var view = editor.TextArea.TextView;
        double originalHeight = view.VisualLines[0].Height;
        double originalWidth = view.WideSpaceWidth;
        editor.FontFamily = EditorTypography.CreateFont(settings with { LineSpacing = spacing });
        Layout(editor);
        Assert.InRange(view.VisualLines[0].Height / originalHeight, spacing - 0.05, spacing + 0.05);
        Assert.InRange(view.DefaultLineHeight / originalHeight, spacing - 0.05, spacing + 0.05);
        Assert.InRange(view.WideSpaceWidth, originalWidth - 0.05, originalWidth + 0.05);
        Assert.Equal("first line\nsecond line\nthird line", editor.Text);
        Assert.True(editor.Document.UndoStack.IsOriginalFile);
        Assert.Equal(1, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionLength);
        var second = view.VisualLines[1];
        Assert.Equal(2, view.GetPosition(new Point(2, second.VisualTop + second.Height / 2))?.Line);
    });

    [Fact]
    public void WrappedLinesAlsoUseSpacingWithoutChangingWrapping() => RunSta(() =>
    {
        var editor = new TextEditor { FontSize = 16, WordWrap = true, Text = string.Concat(Enumerable.Repeat("word ", 35)) };
        var settings = new EditorSettings { FontFamily = "Consolas", FontSize = 16 };
        editor.FontFamily = EditorTypography.CreateFont(settings);
        Layout(editor, 220);
        var view = editor.TextArea.TextView;
        int count = view.VisualLines[0].TextLines.Count;
        double height = view.VisualLines[0].TextLines[0].Height;
        Assert.True(count > 1);
        editor.FontFamily = EditorTypography.CreateFont(settings with { LineSpacing = 1.5 });
        Layout(editor, 220);
        Assert.Equal(count, view.VisualLines[0].TextLines.Count);
        foreach (var line in view.VisualLines[0].TextLines) Assert.InRange(line.Height / height, 1.45, 1.55);
    });

    private static void Layout(TextEditor editor, double width = 800)
    {
        editor.Measure(new Size(width, 500));
        editor.Arrange(new Rect(0, 0, width, 500));
        editor.UpdateLayout();
        var view = editor.TextArea.TextView;
        // A detached editor has no ScrollViewer viewport until it is hosted.
        // Give the formatter a real viewport without showing a test window.
        System.Windows.Documents.TextElement.SetFontFamily(view, editor.FontFamily);
        System.Windows.Documents.TextElement.SetFontSize(view, editor.FontSize);
        ((System.Windows.Controls.Primitives.IScrollInfo)view).CanHorizontallyScroll = !editor.WordWrap;
        view.Measure(new Size(width, 500));
        view.Arrange(new Rect(0, 0, width, 500));
        view.EnsureVisualLines();
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF font verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
