using System.IO;
using System.Text;
using ICSharpCode.AvalonEdit.Document;
using Minimal.Models;

namespace Minimal.ViewModels;

public sealed class DocumentViewModel : ObservableObject
{
    private string? path;
    private bool preview;
    private bool encodingChanged;
    private bool wasLarge;
    private SyntaxLanguage language;
    private Encoding encoding = new UTF8Encoding(false, true);
    public TextDocument Buffer { get; } = new();
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UntitledName { get; }
    public string? FilePath { get => path; private set { path = value; Notify(); Notify(nameof(Title)); Notify(nameof(IsMarkdown)); if (!IsMarkdown) IsPreview = false; } }
    public string Title => (FilePath is null ? UntitledName : Path.GetFileName(FilePath)) + (IsModified ? " •" : "");
    public bool IsModified => !Buffer.UndoStack.IsOriginalFile || encodingChanged;
    public SyntaxLanguage Language
    {
        get => language;
        set
        {
            if (language == value) return;
            language = value;
            Notify(); Notify(nameof(IsMarkdown));
            if (!IsMarkdown) IsPreview = false;
        }
    }
    public bool IsMarkdown => Language == SyntaxLanguage.Markdown || Language == SyntaxLanguage.Automatic && Path.GetExtension(FilePath ?? "").ToLowerInvariant() is ".md" or ".markdown" or ".mdown";
    public bool IsPreview { get => preview; set { var next = value && IsMarkdown; if (preview == next) return; preview = next; Notify(); } }
    public Encoding Encoding { get => encoding; set { encoding = value; encodingChanged = true; Notify(); Notify(nameof(IsModified)); Notify(nameof(Title)); } }
    public string LineEnding { get; private set; } = "\r\n";
    public string? Fingerprint { get; set; }
    public string? ObservedExternalFingerprint { get; set; }
    public int CaretOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double HorizontalOffset { get; set; }
    public bool IsLarge => Buffer.TextLength > 5 * 1024 * 1024;

    public DocumentViewModel(string name)
    {
        UntitledName = name;
        Buffer.UndoStack.PropertyChanged += (_, _) => { Notify(nameof(IsModified)); Notify(nameof(Title)); };
        Buffer.TextChanged += (_, _) => { if (wasLarge != IsLarge) { wasLarge = IsLarge; Notify(nameof(IsLarge)); } };
        Buffer.UndoStack.MarkAsOriginalFile();
    }

    public void Load(string filePath, FileSnapshot snapshot)
    {
        Buffer.Text = snapshot.Text;
        Buffer.UndoStack.ClearAll();
        encoding = snapshot.Encoding;
        LineEnding = snapshot.LineEnding;
        FilePath = filePath;
        Fingerprint = snapshot.Fingerprint;
        ObservedExternalFingerprint = null;
        CaretOffset = Math.Min(CaretOffset, Buffer.TextLength);
        MarkSaved(filePath, snapshot.Fingerprint);
        Notify(nameof(Encoding));
    }

    public void MarkSaved(string filePath, string fingerprint)
    {
        FilePath = filePath;
        Fingerprint = fingerprint;
        ObservedExternalFingerprint = null;
        encodingChanged = false;
        Buffer.UndoStack.MarkAsOriginalFile();
        Notify(nameof(IsModified)); Notify(nameof(Title));
    }

    public void Restore(SessionTab state, string text, Encoding restoredEncoding)
    {
        Buffer.Text = text; Buffer.UndoStack.ClearAll();
        encoding = restoredEncoding; encodingChanged = false;
        LineEnding = state.LineEnding is "\r\n" or "\n" or "\r" ? state.LineEnding : Services.FileService.DetectLineEnding(text);
        FilePath = state.FilePath; Fingerprint = state.Fingerprint;
        Language = Enum.IsDefined(state.Language) ? state.Language : SyntaxLanguage.Automatic;
        CaretOffset = Math.Clamp(state.CaretOffset, 0, Buffer.TextLength);
        VerticalOffset = double.IsFinite(state.VerticalOffset) ? Math.Max(0, state.VerticalOffset) : 0;
        HorizontalOffset = double.IsFinite(state.HorizontalOffset) ? Math.Max(0, state.HorizontalOffset) : 0;
        if (state.IsModified) Buffer.UndoStack.DiscardOriginalFileMarker();
        else Buffer.UndoStack.MarkAsOriginalFile();
        Notify(nameof(Encoding)); Notify(nameof(IsModified)); Notify(nameof(Title));
    }
}
