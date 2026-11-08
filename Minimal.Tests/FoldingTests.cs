using System.Runtime.ExceptionServices;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using Minimal.Models;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class FoldingTests
{
    [Fact]
    public void NativeFoldsTrackEditsAndCanBeDetachedBetweenTabs()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var editor = new TextEditor { Text = "{\n  {\n  }\n}" };
                var document = editor.Document;
                var manager = FoldingManager.Install(editor.TextArea);
                Update(manager, document.Text);
                var inner = manager.AllFoldings.Last(); inner.IsFolded = true;
                var anchor = document.CreateAnchor(inner.StartOffset);
                document.Insert(0, "// header\n");
                Update(manager, document.Text);
                Assert.True(manager.AllFoldings.Last().IsFolded);
                Assert.Equal(anchor.Offset, manager.AllFoldings.Last().StartOffset);
                FoldingManager.Uninstall(manager);
                editor.Document = new("[\n  1\n]");
                var second = FoldingManager.Install(editor.TextArea);
                second.UpdateFoldings(CodeStructureService.Analyze(editor.Text, SyntaxLanguage.Json).Regions.Select(x => new NewFolding(x.Start, x.End)), -1);
                Assert.False(Assert.Single(second.AllFoldings).IsFolded);
                FoldingManager.Uninstall(second);
                editor.Document = document;
                var restored = FoldingManager.Install(editor.TextArea);
                Update(restored, document.Text);
                foreach (var fold in restored.AllFoldings) fold.IsFolded = fold.StartOffset == anchor.Offset;
                Assert.True(restored.AllFoldings.Last().IsFolded);
                FoldingManager.Uninstall(restored);
            }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)));
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static void Update(FoldingManager manager, string text) => manager.UpdateFoldings(CodeStructureService.Analyze(text, SyntaxLanguage.CSharp).Regions.Select(x => new NewFolding(x.Start, x.End)), -1);
}
