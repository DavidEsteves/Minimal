using System.Windows;
using Minimal.Views;

namespace Minimal;

public partial class MainWindow
{
    private void GoToLineClick(object sender, RoutedEventArgs e) => GoToLine();
    private void GoToLine()
    {
        if (model.Active is not { } doc) return;
        doc.IsPreview = false;
        var dialog = new GoToLineDialog(Editor.TextArea.Caret.Line, doc.Buffer.LineCount) { Owner = this, Icon = Icon };
        if (dialog.ShowDialog() == true)
        {
            RevealOffset(doc.Buffer.GetLineByNumber(dialog.LineNumber).Offset);
            Editor.CaretOffset = doc.Buffer.GetLineByNumber(dialog.LineNumber).Offset;
            Editor.SelectionLength = 0;
            Editor.ScrollToLine(dialog.LineNumber);
        }
        Editor.Focus();
    }
    private void RevealOffset(int offset)
    {
        if (foldingManager is null) return;
        foreach (var fold in foldingManager.GetFoldingsContaining(offset)) fold.IsFolded = false;
    }
}
