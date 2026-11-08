using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Minimal.Services;

public sealed record SearchOptions(string Query, bool MatchCase = false, bool WholeWord = false, bool RegularExpression = false);
public sealed record SearchHit(int Offset, int Length, int Line, int Column, string Source, int LineStart, int LineEnd)
{
    public string Location => $"{Line}:{Column}";
    public string Preview
    {
        get
        {
            int start = Math.Max(LineStart, Offset - 60), end = Math.Min(LineEnd, start + 170);
            return (start > LineStart ? "…" : "") + Source[start..end].Replace('\t', ' ') + (end < LineEnd ? "…" : "");
        }
    }
    public override string ToString() => Location + "  " + Preview;
}
public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, string? Error = null);

public static class SearchService
{
    private static Regex CreateRegex(SearchOptions options)
    {
        string pattern = options.RegularExpression ? options.Query : Regex.Escape(options.Query);
        if (options.WholeWord) pattern = @"(?<![\p{L}\p{M}\p{N}\p{Pc}])(?:" + pattern + @")(?![\p{L}\p{M}\p{N}\p{Pc}])";
        return new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase), TimeSpan.FromMilliseconds(200));
    }
    public static SearchResult Find(string text, SearchOptions options, CancellationToken cancellation = default)
    {
        if (options.Query.Length == 0) return new([]);
        try
        {
            var regex = CreateRegex(options);
            var hits = new List<SearchHit>();
            int line = 1, lineStart = 0, scan = 0;
            int lineEnd = NextLineEnd(text, 0);
            var elapsed = Stopwatch.StartNew();
            foreach (Match match in regex.Matches(text))
            {
                cancellation.ThrowIfCancellationRequested();
                if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) return new([], "Search took too long. Narrow the query.");
                while (scan < match.Index)
                {
                    char c = text[scan++];
                    if (c == '\r') { if (scan < text.Length && text[scan] == '\n') scan++; line++; lineStart = scan; }
                    else if (c == '\n') { line++; lineStart = scan; }
                }
                if (lineEnd < match.Index) lineEnd = NextLineEnd(text, Math.Min(match.Index, text.Length));
                hits.Add(new(match.Index, match.Length, line, match.Index - lineStart + 1, text, lineStart, lineEnd));
            }
            return new(hits);
        }
        catch (RegexParseException ex) { return new([], $"Invalid regular expression at character {ex.Offset + 1}: {ex.Error}."); }
        catch (ArgumentException) { return new([], "Invalid regular expression."); }
        catch (RegexMatchTimeoutException) { return new([], "Search took too long. Simplify the regular expression."); }
    }
    private static int NextLineEnd(string text, int offset)
    {
        int index = text.AsSpan(offset).IndexOfAny('\r', '\n');
        return index < 0 ? text.Length : offset + index;
    }
    public static string Replace(string text, SearchOptions options, string replacement, int? onlyOffset = null, CancellationToken cancellation = default)
    {
        if (options.Query.Length == 0) return text;
        var regex = CreateRegex(options);
        var result = new StringBuilder();
        int last = 0;
        var elapsed = Stopwatch.StartNew();
        foreach (Match match in regex.Matches(text))
        {
            cancellation.ThrowIfCancellationRequested();
            if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new RegexMatchTimeoutException();
            if (onlyOffset is not null && match.Index != onlyOffset) continue;
            result.Append(text, last, match.Index - last);
            result.Append(options.RegularExpression ? match.Result(replacement) : replacement);
            last = match.Index + match.Length;
            if (onlyOffset is not null) break;
        }
        result.Append(text, last, text.Length - last);
        return result.ToString();
    }
}
