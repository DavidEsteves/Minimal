using System.Windows;
using ICSharpCode.AvalonEdit.Indentation.CSharp;
using Minimal.Models;
using Minimal.Services;

namespace Minimal;

public partial class MainWindow
{
    private readonly FormattingChord formatChord = new();
    private void ConfigureIndentation()
    {
        var language = displayed is null ? SyntaxLanguage.PlainText : CodeStructureService.Resolve(displayed.FilePath, displayed.Language);
        Editor.TextArea.IndentationStrategy = language == SyntaxLanguage.CSharp ? new CSharpIndentationStrategy(Editor.Options) : new EditorIndentationStrategy(language, Editor.Options);
        FormatDocumentMenu.IsEnabled = displayed?.IsPreview == false && FormattingService.Supports(language);
    }
    private async void FormatDocumentClick(object sender, RoutedEventArgs e) => await FormatDocumentAsync();
    private async Task FormatDocumentAsync()
    {
        if (displayed is not { IsPreview: false } doc) return;
        var language = CodeStructureService.Resolve(doc.FilePath, doc.Language);
        if (!FormattingService.Supports(language)) { MessageBox.Show(this, "Format Document is available for JSON and XML.", "Format document"); return; }
        await RunBusyAsync(async () =>
        {
            var snapshot = doc.Buffer.CreateSnapshot();
            var version = doc.Buffer.Version;
            string indentation = Editor.Options.IndentationString;
            string lineEnding = doc.Buffer.Text.IndexOfAny(['\r', '\n']) >= 0 ? FileService.DetectLineEnding(doc.Buffer.Text) : doc.LineEnding;
            var result = await Task.Run(() => FormattingService.Format(snapshot.Text, language, indentation, lineEnding));
            if (result.Error is not null) { MessageBox.Show(this, result.Error, "Format document"); return; }
            if (doc != displayed || doc.Buffer.Version.CompareAge(version) != 0 || result.Text == snapshot.Text) return;
            int line = Editor.TextArea.Caret.Line, column = Editor.TextArea.Caret.Column;
            using (doc.Buffer.RunUpdate()) doc.Buffer.Replace(0, doc.Buffer.TextLength, result.Text!);
            Editor.TextArea.Caret.Line = Math.Min(line, doc.Buffer.LineCount);
            Editor.TextArea.Caret.Column = Math.Min(column, doc.Buffer.GetLineByNumber(Editor.TextArea.Caret.Line).Length + 1);
            Editor.SelectionLength = 0;
            Editor.TextArea.Caret.BringCaretToView();
        });
    }
}
