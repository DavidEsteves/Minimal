using System.IO;
using System.Text;
using System.Text.Json;
using ICSharpCode.AvalonEdit.Document;
using Minimal.Models;
using Minimal.Services;
using Minimal.ViewModels;
using Xunit;

namespace Minimal.Tests;

public sealed class SessionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "Minimal-session-tests-" + Guid.NewGuid().ToString("N"));
    private static CapturedTab Capture(SessionTab state, string? text)
    {
        if (text is null) return new(state, null, null);
        var document = new TextDocument(text);
        return new(state, document.CreateSnapshot(), document.Version);
    }
    private static SessionTab Tab(string name, bool dirty = true) => new() { Id = Guid.NewGuid(), Name = name, IsModified = dirty };

    [Fact]
    public async Task RecoversTabOrderActiveTabUnsavedTextAndMetadata()
    {
        var one = Tab("Untitled 5") with { CaretOffset = 8, VerticalOffset = 144, HorizontalOffset = 22, Language = SyntaxLanguage.PowerShell, CodePage = 1201, HasPreamble = true, LineEnding = "\n" };
        var two = Tab("saved", false) with { FilePath = Path.Combine(root, "saved.txt"), Fingerprint = "original" };
        const string text = "Unicode café 日本語 😀\nfunction F {\n}";
        using (var writer = new SessionManager(root)) await writer.SaveAsync(new(one.Id, [Capture(one, text), Capture(two, null)]));
        using var reader = new SessionManager(root);
        var restored = await reader.RecoverAsync();
        Assert.Empty(restored.Notices); Assert.Equal(one.Id, restored.ActiveId);
        Assert.Equal(new[] { one.Id, two.Id }, restored.Tabs.Select(x => x.State.Id));
        Assert.Equal(text, restored.Tabs[0].Content); Assert.Null(restored.Tabs[1].Content);
        Assert.Equal(144, restored.Tabs[0].State.VerticalOffset);
        Assert.Equal(1201, restored.Tabs[0].State.CodePage);
        Assert.True(restored.Tabs[0].State.HasPreamble);
    }
    [Fact]
    public async Task RecoveryDoesNotConsumeAnotherRunningInstance()
    {
        var tab = Tab("scratch");
        using var writer = new SessionManager(root);
        await writer.SaveAsync(new(tab.Id, [Capture(tab, "keep this")]));
        using var second = new SessionManager(root);
        Assert.Empty((await second.RecoverAsync()).Tabs);
        writer.Dispose();
        var recovered = await second.RecoverAsync();
        Assert.Single(recovered.Tabs);
        await second.SaveAsync(new(tab.Id, [Capture(recovered.Tabs[0].State, recovered.Tabs[0].Content)]));
        Assert.False(Directory.Exists(writer.DirectoryPath));
    }
    [Fact]
    public async Task StoresOnlyNeededContentAndDeduplicatesIdenticalScratchpads()
    {
        var one = Tab("one"); var two = Tab("two"); var saved = Tab("saved", false) with { FilePath = "existing.txt" };
        using var manager = new SessionManager(root);
        await manager.SaveAsync(new(two.Id, [Capture(one, "same"), Capture(two, "same"), Capture(saved, null)]));
        Assert.Single(Directory.GetFiles(manager.DirectoryPath, "*.txt"));
        string json = await File.ReadAllTextAsync(Path.Combine(manager.DirectoryPath, "session.json"));
        Assert.DoesNotContain("same", json);
        var state = JsonSerializer.Deserialize<SessionState>(json)!;
        Assert.Equal(state.Tabs[0].ContentHash, state.Tabs[1].ContentHash); Assert.Null(state.Tabs[2].ContentHash);
    }
    [Fact]
    public async Task CorruptPrimaryManifestFallsBackToAtomicBackup()
    {
        var tab = Tab("scratch"); string directory;
        using (var writer = new SessionManager(root))
        {
            directory = writer.DirectoryPath;
            await writer.SaveAsync(new(tab.Id, [Capture(tab, "first checkpoint")]));
            await writer.SaveAsync(new(tab.Id, [Capture(tab, "second checkpoint")]));
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "session.json"), "{broken");
        using var reader = new SessionManager(root);
        var restored = await reader.RecoverAsync();
        Assert.Empty(restored.Notices); Assert.Equal("first checkpoint", Assert.Single(restored.Tabs).Content);
    }
    [Fact]
    public async Task CorruptOrMissingContentIsRetainedAndReported()
    {
        var tab = Tab("scratch"); string directory;
        using (var writer = new SessionManager(root))
        { directory = writer.DirectoryPath; await writer.SaveAsync(new(tab.Id, [Capture(tab, "original")])); }
        await File.WriteAllTextAsync(Directory.GetFiles(directory, "*.txt").Single(), "corrupted");
        using var reader = new SessionManager(root);
        var restored = await reader.RecoverAsync();
        Assert.Empty(restored.Tabs); Assert.NotEmpty(restored.Notices);
        await reader.SaveAsync(new(null, []));
        Assert.True(Directory.Exists(directory));
    }
    [Fact]
    public async Task SavesNeverOverwriteOriginalFilesAndRestoreKeepsConflictFingerprint()
    {
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "original.txt");
        await File.WriteAllTextAsync(path, "on disk");
        var state = Tab("file") with { FilePath = path, Fingerprint = "old disk fingerprint", Language = SyntaxLanguage.CSharp };
        using (var manager = new SessionManager(root)) await manager.SaveAsync(new(state.Id, [Capture(state, "unsaved changes")]));
        await File.WriteAllTextAsync(path, "externally modified");
        using var recovery = new SessionManager(root);
        var recovered = Assert.Single((await recovery.RecoverAsync()).Tabs);
        var doc = new DocumentViewModel(state.Name);
        doc.Restore(recovered.State, recovered.Content!, new UTF8Encoding(false, true));
        Assert.True(doc.IsModified); Assert.Equal("old disk fingerprint", doc.Fingerprint);
        Assert.Equal("unsaved changes", doc.Buffer.Text);
        Assert.Equal("externally modified", await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task RemovingTabsAndDisablingRecoveryClearsCheckpoints()
    {
        var tab = Tab("scratch");
        using var manager = new SessionManager(root);
        await manager.SaveAsync(new(tab.Id, [Capture(tab, "text")]));
        await manager.SaveAsync(new(null, []));
        await manager.SaveAsync(new(null, []));
        Assert.Empty(Directory.GetFiles(manager.DirectoryPath, "*.txt"));
        await manager.ClearAsync();
        Assert.Equal(new[] { "session.lock" }, Directory.GetFiles(manager.DirectoryPath).Select(Path.GetFileName));
    }
    [Fact]
    public async Task ExplicitCloseCannotReappearFromBackup()
    {
        var tab = Tab("closed scratchpad"); string directory;
        using (var manager = new SessionManager(root))
        {
            directory = manager.DirectoryPath;
            await manager.SaveAsync(new(tab.Id, [Capture(tab, "discarded text")]));
            await manager.SaveAsync(new(null, []), forgetPrevious: true);
            Assert.Empty(Directory.GetFiles(directory, "*.txt"));
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "session.json"), "corrupt");
        using var recovery = new SessionManager(root);
        Assert.Empty((await recovery.RecoverAsync()).Tabs);
    }
    [Fact]
    public void RestoredDocumentsClampPositionsAndKeepDirtyStateAndUniqueUntitledNames()
    {
        var tab = Tab("Untitled 1") with { CaretOffset = 500, VerticalOffset = double.NaN, HorizontalOffset = -1 };
        var doc = new DocumentViewModel(tab.Name); doc.Restore(tab, "text", new UTF8Encoding(false));
        Assert.True(doc.IsModified); Assert.Equal(4, doc.CaretOffset); Assert.Equal(0, doc.VerticalOffset); Assert.Equal(0, doc.HorizontalOffset);
        var model = new MainViewModel(); model.Documents.Add(doc);
        Assert.Equal("Untitled 2", model.NewDocument().UntitledName);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
