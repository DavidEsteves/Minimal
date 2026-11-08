using ICSharpCode.AvalonEdit.Document;

namespace Minimal.Models;

public sealed record SessionTab
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "Untitled";
    public string? FilePath { get; init; }
    public int CodePage { get; init; } = 65001;
    public bool HasPreamble { get; init; }
    public string LineEnding { get; init; } = "\r\n";
    public string? Fingerprint { get; init; }
    public bool IsModified { get; init; }
    public SyntaxLanguage Language { get; init; }
    public int CaretOffset { get; init; }
    public double VerticalOffset { get; init; }
    public double HorizontalOffset { get; init; }
    public string? ContentHash { get; init; }
}
public sealed record SessionState(int Version, Guid? ActiveId, IReadOnlyList<SessionTab> Tabs);
public sealed record CapturedTab(SessionTab State, ITextSource? Content, ITextSourceVersion? ContentVersion);
public sealed record SessionCapture(Guid? ActiveId, IReadOnlyList<CapturedTab> Tabs);
public sealed record RecoveredTab(SessionTab State, string? Content);
public sealed record SessionRecovery(Guid? ActiveId, IReadOnlyList<RecoveredTab> Tabs, IReadOnlyList<string> Notices);
