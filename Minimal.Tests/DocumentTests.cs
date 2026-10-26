using System.Text;
using Minimal.Models;
using Minimal.ViewModels;
using Xunit;

namespace Minimal.Tests;

public sealed class DocumentTests
{
    [Fact]
    public void LanguageSelectionIsPerTabAndDoesNotModifyTheDocument()
    {
        var model = new MainViewModel();
        var one = model.NewDocument();
        one.Load(@"C:\example.txt", new FileSnapshot("print('hello')\n", new UTF8Encoding(false), "\n", "hash", false));
        one.Language = SyntaxLanguage.Python;
        var two = model.NewDocument();
        two.Language = SyntaxLanguage.Json;
        model.NextTab(1);
        Assert.Same(one, model.Active);
        Assert.Equal(SyntaxLanguage.Python, one.Language);
        Assert.Equal(SyntaxLanguage.Json, two.Language);
        Assert.False(one.IsModified);
        Assert.False(two.IsModified);
        Assert.Equal("print('hello')\n", one.Buffer.Text);
        one.MarkSaved(@"C:\renamed.cs", "new hash");
        Assert.Equal(SyntaxLanguage.Python, one.Language);
    }

    [Fact]
    public void ManualMarkdownEnablesUntitledPreviewAndPlainTextReturnsToSource()
    {
        var doc = new DocumentViewModel("Untitled");
        doc.Buffer.Text = "# In-memory Markdown";
        doc.Language = SyntaxLanguage.Markdown;
        Assert.True(doc.IsMarkdown);
        doc.IsPreview = true;
        Assert.True(doc.IsPreview);
        doc.Language = SyntaxLanguage.PlainText;
        Assert.False(doc.IsMarkdown);
        Assert.False(doc.IsPreview);
        Assert.Equal("# In-memory Markdown", doc.Buffer.Text);
        Assert.True(doc.IsModified);
        doc.MarkSaved(@"C:\example.md", "hash");
        Assert.False(doc.IsMarkdown);
        doc.Language = SyntaxLanguage.Automatic;
        Assert.True(doc.IsMarkdown);
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void ModifiedTrackingFollowsUndoRedoAndSavePoint()
    {
        var doc = new DocumentViewModel("Untitled 1");
        Assert.False(doc.IsModified);
        doc.Buffer.Insert(0, "hello");
        Assert.True(doc.IsModified);
        doc.Buffer.UndoStack.Undo();
        Assert.False(doc.IsModified);
        doc.Buffer.UndoStack.Redo();
        doc.MarkSaved(@"C:\hello.txt", "hash");
        Assert.False(doc.IsModified);
        doc.Buffer.Insert(5, " world");
        Assert.True(doc.IsModified);
        doc.Buffer.UndoStack.Undo();
        Assert.False(doc.IsModified);
        doc.Encoding = new UTF8Encoding(true, true);
        Assert.True(doc.IsModified);
    }

    [Fact]
    public void LoadResetsUndoAndRetainsExactSource()
    {
        var doc = new DocumentViewModel("Untitled");
        doc.Buffer.Text = "old";
        doc.Load(@"C:\test.md", new FileSnapshot("# heading\n\nunsaved?", new UTF8Encoding(false), "\n", "fingerprint", false));
        Assert.False(doc.IsModified);
        Assert.False(doc.Buffer.UndoStack.CanUndo);
        Assert.Equal("# heading\n\nunsaved?", doc.Buffer.Text);
        Assert.Equal("\n", doc.LineEnding);
    }

    [Fact]
    public void PreviewIsPerTabAndRestrictedToMarkdown()
    {
        var model = new MainViewModel();
        var one = model.NewDocument();
        one.IsPreview = true;
        Assert.False(one.IsPreview);
        one.MarkSaved(@"C:\one.md", "one");
        one.Buffer.Text = "# latest unsaved source";
        one.CaretOffset = 5;
        one.VerticalOffset = 128;
        one.IsPreview = true;
        var two = model.NewDocument();
        two.MarkSaved(@"C:\two.md", "two");
        model.NextTab(1);
        Assert.Same(one, model.Active);
        Assert.True(one.IsPreview);
        Assert.False(two.IsPreview);
        Assert.Equal(5, one.CaretOffset);
        Assert.Equal(128, one.VerticalOffset);
        one.IsPreview = false;
        Assert.Equal("# latest unsaved source", one.Buffer.Text);
        Assert.True(one.IsModified);
        one.IsPreview = true;
        one.MarkSaved(@"C:\one.txt", "hash");
        Assert.False(one.IsPreview);
    }

    [Fact]
    public void TabsCycleBothWaysDeduplicateAndChooseNeighborOnClose()
    {
        var model = new MainViewModel();
        var one = model.NewDocument();
        one.MarkSaved(@"C:\unique.txt", "hash");
        var two = model.NewDocument();
        var three = model.NewDocument();
        model.NextTab(1); Assert.Same(one, model.Active);
        model.NextTab(-1); Assert.Same(three, model.Active);
        Assert.Same(one, model.FindPath(@"c:\UNIQUE.txt"));
        model.Remove(three); Assert.Same(two, model.Active);
        model.Remove(one); Assert.Same(two, model.Active);
        model.Remove(two); Assert.Null(model.Active);
    }
}
