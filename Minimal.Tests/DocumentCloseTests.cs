using Minimal.Services;
using Minimal.ViewModels;
using Xunit;

namespace Minimal.Tests;

public sealed class DocumentCloseTests
{
    [Fact]
    public async Task CleanDocumentClosesWithoutPromptOrSave()
    {
        var doc = new DocumentViewModel("Untitled");
        Assert.True(await DocumentCloseService.CanCloseAsync(doc,
            _ => throw new Exception("Unexpected prompt"), _ => throw new Exception("Unexpected save")));
    }

    [Theory]
    [InlineData(SaveChangesChoice.Cancel, false)]
    [InlineData(SaveChangesChoice.Discard, true)]
    public async Task CancelAndDiscardHaveDistinctResultsAndDoNotSave(SaveChangesChoice choice, bool expected)
    {
        var doc = ModifiedDocument();
        var result = await DocumentCloseService.CanCloseAsync(doc, _ => choice, _ => throw new Exception("Unexpected save"));
        Assert.Equal(expected, result);
        Assert.Equal("Keep these edits", doc.Buffer.Text);
        Assert.True(doc.IsModified);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveMustFinishSuccessfullyBeforeClosing(bool saved)
    {
        var doc = ModifiedDocument();
        var completion = new TaskCompletionSource<bool>();
        int saves = 0;
        var closing = DocumentCloseService.CanCloseAsync(doc, _ => SaveChangesChoice.Save,
            requested => { Assert.Same(doc, requested); saves++; return completion.Task; });
        Assert.False(closing.IsCompleted);
        completion.SetResult(saved);
        Assert.Equal(saved, await closing);
        Assert.Equal(1, saves);
        Assert.Equal("Keep these edits", doc.Buffer.Text);
    }

    [Fact]
    public async Task SaveFailureKeepsEditsAndDoesNotReportSuccessfulClose()
    {
        var doc = ModifiedDocument();
        await Assert.ThrowsAsync<System.IO.IOException>(() => DocumentCloseService.CanCloseAsync(doc,
            _ => SaveChangesChoice.Save, _ => Task.FromException<bool>(new System.IO.IOException("Read-only file"))));
        Assert.True(doc.IsModified);
        Assert.Equal("Keep these edits", doc.Buffer.Text);
    }

    private static DocumentViewModel ModifiedDocument()
    {
        var doc = new DocumentViewModel("Untitled");
        doc.Buffer.Text = "Keep these edits";
        return doc;
    }
}
