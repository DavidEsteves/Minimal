using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Minimal.Models;
using Minimal.Services;
using Minimal.ViewModels;
using Minimal.Views;

namespace Minimal;

public partial class MainWindow
{
    private SessionManager? sessions;
    private readonly DispatcherTimer sessionTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool sessionReady, sessionDirty = true;
    private Task? sessionWrite;

    public async Task InitializeAsync(IEnumerable<string> paths)
    {
        await RunBusyAsync(async () =>
        {
            sessions = new SessionManager();
            if (editorSettings.RestorePreviousSession) await RestoreSessionAsync();
            else { await sessions.ClearInactiveAsync(); await sessions.ClearAsync(); }
            foreach (string path in paths) await OpenOneAsync(path);
            sessionReady = true;
            if (editorSettings.RestorePreviousSession) await SaveSessionNowAsync();
        });
        if (!sessionReady) sessionReady = true; // Exit can still save/retry, or use the normal Save command.
        sessionTimer.Tick += async (_, _) =>
        {
            if (!sessionDirty || busy || sessionWrite is { IsCompleted: false } || !editorSettings.RestorePreviousSession) return;
            try { await SaveSessionNowAsync(); FormatStatus.ToolTip = null; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            { sessionDirty = true; FormatStatus.ToolTip = "Session recovery could not be updated: " + ex.Message; }
        };
        Editor.TextChanged += (_, _) => sessionDirty = true;
        Editor.TextArea.Caret.PositionChanged += (_, _) => sessionDirty = true;
        Editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => sessionDirty = true;
        model.Documents.CollectionChanged += (_, _) => sessionDirty = true;
        sessionTimer.Start();
    }
    private async Task RestoreSessionAsync()
    {
        if (sessions is null) return;
        var recovery = await sessions.RecoverAsync();
        var notices = recovery.Notices.ToList();
        var restored = new List<DocumentViewModel>();
        bool incomplete = false;
        foreach (var recovered in recovery.Tabs)
        {
            var state = recovered.State;
            try
            {
                var doc = new DocumentViewModel(string.IsNullOrWhiteSpace(state.Name) ? "Untitled" : state.Name) { Id = state.Id };
                string text;
                Encoding encoding = RecoveryEncoding(state);
                if (state.FilePath is not null && recovered.Content is null)
                {
                    try
                    {
                        var snapshot = await files.LoadAsync(state.FilePath, encoding);
                        text = snapshot.Text; encoding = snapshot.Encoding;
                        state = state with { Fingerprint = snapshot.Fingerprint, LineEnding = snapshot.LineEnding };
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        text = ""; state = state with { IsModified = true };
                        notices.Add($"{state.FilePath}\nThe saved file could not be reopened. Its tab was retained; no cached copy was needed while it was saved. {ex.Message}");
                    }
                }
                else text = recovered.Content ?? "";
                doc.Restore(state, text, encoding);
                if (state.FilePath is not null && recovered.Content is not null)
                {
                    string? current = null;
                    try { current = await files.FingerprintAsync(state.FilePath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { notices.Add($"Could not check {state.FilePath}: {ex.Message}"); }
                    if (current != state.Fingerprint)
                    {
                        doc.ObservedExternalFingerprint = current ?? "missing";
                        notices.Add($"{state.FilePath}\nThis file changed or is unavailable. Your recovered edits were kept. Saving will ask before overwriting it.");
                    }
                }
                restored.Add(doc);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { incomplete = true; notices.Add($"Could not restore {state.Name}: {ex.Message} Its recovery files were retained."); }
        }
        if (incomplete) sessions.RetainRecoveredCheckpoints();
        if (restored.Count > 0)
        {
            model.Active = null;
            model.Documents.Clear();
            foreach (var doc in restored) model.Documents.Add(doc);
            model.Active = restored.FirstOrDefault(x => x.Id == recovery.ActiveId) ?? restored[0];
        }
        if (notices.Count > 0) MessageBox.Show(this, string.Join("\n\n", notices.Take(8)) + (notices.Count > 8 ? $"\n\n{notices.Count - 8} additional recovery notices." : ""), "Session recovery", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private static Encoding RecoveryEncoding(SessionTab state)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return state.CodePage switch
        {
            65001 => new UTF8Encoding(state.HasPreamble, true),
            1200 => new UnicodeEncoding(false, state.HasPreamble, true),
            1201 => new UnicodeEncoding(true, state.HasPreamble, true),
            12000 => new UTF32Encoding(false, state.HasPreamble, true),
            12001 => new UTF32Encoding(true, state.HasPreamble, true),
            _ => Encoding.GetEncoding(state.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        };
    }
    private SessionCapture CaptureSession(ISet<Guid>? discarded = null)
    {
        RememberPosition();
        var tabs = new List<CapturedTab>();
        foreach (var doc in model.Documents)
        {
            bool discard = discarded?.Contains(doc.Id) == true;
            if (discard && doc.FilePath is null) continue;
            var state = new SessionTab
            {
                Id = doc.Id, Name = doc.UntitledName, FilePath = doc.FilePath, Fingerprint = doc.Fingerprint,
                CodePage = doc.Encoding.CodePage, HasPreamble = doc.Encoding.GetPreamble().Length > 0,
                LineEnding = doc.LineEnding, IsModified = doc.IsModified && !discard, Language = doc.Language,
                CaretOffset = doc.CaretOffset, VerticalOffset = doc.VerticalOffset, HorizontalOffset = doc.HorizontalOffset
            };
            bool content = !discard && (doc.IsModified || doc.FilePath is null);
            tabs.Add(new(state, content ? doc.Buffer.CreateSnapshot() : null, content ? doc.Buffer.Version : null));
        }
        return new(model.Active?.Id, tabs);
    }
    private async Task SaveSessionNowAsync(ISet<Guid>? discarded = null, bool forgetPrevious = false)
    {
        if (!sessionReady || !editorSettings.RestorePreviousSession) return;
        if (sessionWrite is { IsCompleted: false })
            try { await sessionWrite; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        sessions ??= new SessionManager();
        var capture = CaptureSession(discarded);
        sessionDirty = false;
        sessionWrite = Task.Run(() => sessions.SaveAsync(capture, forgetPrevious));
        try { await sessionWrite; }
        catch { sessionDirty = true; throw; }
    }
    private async Task<bool> PrepareSessionExitAsync()
    {
        var discarded = new HashSet<Guid>();
        foreach (var doc in model.Documents.ToArray())
        {
            if (!doc.IsModified) continue;
            if (!editorSettings.RestorePreviousSession) { if (!await ConfirmCloseAsync(doc)) return false; continue; }
            var choice = SaveChangesDialog.Ask(this, doc.Title.TrimEnd(' ', '•'), true);
            if (choice == SaveChangesChoice.Cancel) return false;
            if (choice == SaveChangesChoice.Save && !await SaveDocumentAsync(doc)) return false;
            if (choice == SaveChangesChoice.Discard) discarded.Add(doc.Id);
        }
        await SaveSessionNowAsync(discarded, discarded.Count > 0);
        sessionTimer.Stop();
        return true;
    }
    private async void RestoreSessionClick(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var settings = editorSettings with { RestorePreviousSession = RestoreSessionMenu.IsChecked };
            await preferences.SaveAsync(settings);
            ApplyEditorSettings(settings);
            if (settings.RestorePreviousSession) await SaveSessionNowAsync();
            else if (sessions is not null)
            {
                if (sessionWrite is { IsCompleted: false })
                    try { await sessionWrite; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
                await sessions.ClearAsync(); await sessions.ClearInactiveAsync();
            }
        });
    }
}
