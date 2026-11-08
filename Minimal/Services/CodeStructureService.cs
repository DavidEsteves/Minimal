using Minimal.Models;

namespace Minimal.Services;

public sealed record CodeRegion(int Start, int End, string Name = "…");
public sealed record CodeStructure(IReadOnlyDictionary<int, int> Brackets, IReadOnlyList<CodeRegion> Regions)
{
    public static CodeStructure Empty { get; } = new(new Dictionary<int, int>(), []);
    public (int First, int Second)? PairAt(int caret)
    {
        if (Brackets.TryGetValue(caret, out int next)) return (caret, next);
        if (caret > 0 && Brackets.TryGetValue(caret - 1, out int previous)) return (caret - 1, previous);
        return null;
    }
}

// A conservative lexical pass. Strings/comments are opaque (including interpolation):
// we prefer omitting a fold inside an expression to inventing a structural bracket.
public static class CodeStructureService
{
    public const int MaximumLength = 5 * 1024 * 1024;
    public static SyntaxLanguage Resolve(string? path, SyntaxLanguage language)
    {
        if (language != SyntaxLanguage.Automatic) return language;
        var extension = System.IO.Path.GetExtension(path ?? "").ToLowerInvariant();
        return extension switch
        {
            ".markdown" or ".mdown" => SyntaxLanguage.Markdown, ".yml" => SyntaxLanguage.Yaml,
            ".htm" => SyntaxLanguage.Html, ".psm1" or ".psd1" => SyntaxLanguage.PowerShell,
            _ => SyntaxService.Languages.FirstOrDefault(x => x.Extension == extension)?.Language ?? SyntaxLanguage.PlainText
        };
    }

    public static CodeStructure Analyze(string text, SyntaxLanguage language, CancellationToken cancellation = default)
    {
        if (text.Length > MaximumLength) return CodeStructure.Empty;
        string code = MaskNonCode(text, language, cancellation);
        var stack = new Stack<(char Character, int Offset, int Line)>();
        var pairs = new Dictionary<int, int>();
        var regions = new List<CodeRegion>();
        bool blockFolds = language is SyntaxLanguage.CSharp or SyntaxLanguage.PowerShell or SyntaxLanguage.JavaScript or SyntaxLanguage.TypeScript or SyntaxLanguage.Json or SyntaxLanguage.Css;
        int line = 0;
        for (int i = 0; i < code.Length; i++)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            char c = code[i];
            if (c is '\r' or '\n') line++;
            if (c is '(' or '[' or '{') stack.Push((c, i, line));
            else if (c is ')' or ']' or '}')
            {
                if (stack.Count == 0) continue;
                var open = stack.Peek();
                if (!IsPair(open.Character, c)) { stack.Clear(); continue; }
                stack.Pop(); pairs[open.Offset] = i; pairs[i] = open.Offset;
                if (blockFolds && (open.Character == '{' || language == SyntaxLanguage.Json && open.Character == '[') && line > open.Line)
                    regions.Add(new(open.Offset, i + 1));
            }
        }
        if (language == SyntaxLanguage.Markdown) regions = MarkdownRegions(text, cancellation);
        if (language is SyntaxLanguage.Xml or SyntaxLanguage.Html) regions = MarkupRegions(text, language == SyntaxLanguage.Html, cancellation);
        return new(pairs, regions.OrderBy(x => x.Start).ThenByDescending(x => x.End).ToArray());
    }
    private static bool IsPair(char open, char close) => (open, close) is ('(', ')') or ('[', ']') or ('{', '}');

    public static string MaskNonCode(string text, SyntaxLanguage language, CancellationToken cancellation = default)
    {
        if (language is SyntaxLanguage.PlainText or SyntaxLanguage.Markdown) return text;
        var output = text.ToCharArray();
        bool shell = language is SyntaxLanguage.PowerShell or SyntaxLanguage.Bash or SyntaxLanguage.Python or SyntaxLanguage.Yaml;
        bool slash = language is SyntaxLanguage.CSharp or SyntaxLanguage.JavaScript or SyntaxLanguage.TypeScript or SyntaxLanguage.Json or SyntaxLanguage.Css;
        bool js = language is SyntaxLanguage.JavaScript or SyntaxLanguage.TypeScript;
        for (int i = 0; i < text.Length;)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            int start = i, end = i;
            bool Has(string value) => text.AsSpan(i).StartsWith(value, StringComparison.Ordinal);
            if (language is SyntaxLanguage.Xml or SyntaxLanguage.Html && (Has("<!--") || Has("<![CDATA[")))
            {
                string terminator = Has("<!--") ? "-->" : "]]>";
                int found = text.IndexOf(terminator, i + 4, StringComparison.Ordinal);
                end = found < 0 ? text.Length : found + terminator.Length;
            }
            else if (shell && text[i] == '#' || slash && Has("//") || language == SyntaxLanguage.Sql && Has("--"))
            { end = i; while (end < text.Length && text[end] is not ('\r' or '\n')) end++; }
            else if (slash && Has("/*") || language == SyntaxLanguage.Sql && Has("/*") || language == SyntaxLanguage.PowerShell && Has("<#"))
            {
                string terminator = language == SyntaxLanguage.PowerShell ? "#>" : "*/";
                int depth = 1; end = i + 2;
                while (end < text.Length && depth > 0)
                {
                    if (language == SyntaxLanguage.PowerShell && text.AsSpan(end).StartsWith("<#")) { depth++; end += 2; }
                    else if (text.AsSpan(end).StartsWith(terminator)) { depth--; end += 2; }
                    else end++;
                }
            }
            else if (language == SyntaxLanguage.PowerShell && (Has("@\"") || Has("@'")) && (i + 2 == text.Length || text[i + 2] is '\r' or '\n'))
            {
                string terminator = text[i + 1] + "@";
                end = i + 2;
                while (end < text.Length)
                {
                    if ((end == 0 || text[end - 1] is '\r' or '\n') && text.AsSpan(end).StartsWith(terminator)) { end += 2; break; }
                    end++;
                }
            }
            else if (text[i] is '\"' or '\'' || js && text[i] == '`')
            {
                char quote = text[i];
                bool verbatim = language == SyntaxLanguage.CSharp && i > 0 && (text[i - 1] == '@' || i > 1 && text.AsSpan(i - 2, 2).SequenceEqual("@$"));
                int quotes = 1;
                if (language is SyntaxLanguage.CSharp or SyntaxLanguage.Python && quote == '\"') while (i + quotes < text.Length && text[i + quotes] == quote) quotes++;
                if (language == SyntaxLanguage.Python && quote == '\'' && Has("'''")) quotes = 3;
                if (quotes < 3) quotes = 1;
                end = i + quotes;
                while (end < text.Length)
                {
                    if (quotes >= 3)
                    {
                        if (end + quotes <= text.Length && text.AsSpan(end, quotes).IndexOfAnyExcept(quote) < 0) { end += quotes; break; }
                    }
                    else if (text[end] == quote)
                    {
                        if ((verbatim || language is SyntaxLanguage.PowerShell or SyntaxLanguage.Sql) && end + 1 < text.Length && text[end + 1] == quote) { end += 2; continue; }
                        end++; break;
                    }
                    if (quotes == 1 && !verbatim && (text[end] == '\\' && language is not (SyntaxLanguage.PowerShell or SyntaxLanguage.Sql) || text[end] == '`' && language == SyntaxLanguage.PowerShell && quote == '\"')) end = Math.Min(text.Length, end + 2);
                    else end++;
                }
            }
            else if (js && text[i] == '/' && IsRegexStart(text, i))
            {
                bool inClass = false; end = i + 1;
                while (end < text.Length && text[end] is not ('\r' or '\n'))
                {
                    char c = text[end++];
                    if (c == '\\') { end = Math.Min(text.Length, end + 1); continue; }
                    if (c == '[') inClass = true;
                    if (c == ']') inClass = false;
                    if (c == '/' && !inClass) break;
                }
            }
            if (end == i) { i++; continue; }
            for (int j = start; j < end; j++) { if ((j & 4095) == 0) cancellation.ThrowIfCancellationRequested(); if (output[j] is not ('\r' or '\n')) output[j] = ' '; }
            i = end;
        }
        return new string(output);
    }
    private static bool IsRegexStart(string text, int offset)
    {
        int previous = offset - 1;
        while (previous >= 0 && char.IsWhiteSpace(text[previous])) previous--;
        if (previous < 0 || "=(:,![{;?&|".Contains(text[previous])) return true;
        int end = previous + 1;
        while (previous >= 0 && char.IsLetter(text[previous])) previous--;
        return text[(previous + 1)..end] is "return" or "throw" or "case" or "yield";
    }

    private static List<CodeRegion> MarkdownRegions(string text, CancellationToken cancellation)
    {
        var result = new List<CodeRegion>();
        var headings = new Stack<(int Level, int Start, string Name)>();
        char fence = '\0'; int fenceLength = 0, offset = 0;
        foreach (var line in text.Split('\n'))
        {
            cancellation.ThrowIfCancellationRequested();
            string trimmed = line.TrimEnd('\r');
            int indent = trimmed.TakeWhile(c => c == ' ').Count();
            var content = trimmed.AsSpan(Math.Min(indent, trimmed.Length));
            if (indent <= 3 && content.Length >= 3 && content[0] is '`' or '~')
            {
                int count = 0; while (count < content.Length && content[count] == content[0]) count++;
                if (fence == '\0' && count >= 3) { fence = content[0]; fenceLength = count; }
                else if (content[0] == fence && count >= fenceLength && content[count..].Trim().IsEmpty) fence = '\0';
            }
            else if (fence == '\0' && indent <= 3)
            {
                int level = 0; while (level < content.Length && content[level] == '#') level++;
                if (level is >= 1 and <= 6 && (level == content.Length || char.IsWhiteSpace(content[level])))
                {
                    while (headings.Count > 0 && headings.Peek().Level >= level) Add(headings.Pop(), offset);
                    headings.Push((level, offset + trimmed.Length, content.ToString()));
                }
            }
            offset += line.Length + 1;
        }
        while (headings.Count > 0) Add(headings.Pop(), text.Length);
        return result;
        void Add((int Level, int Start, string Name) heading, int end)
        {
            while (end > heading.Start && text[end - 1] is '\r' or '\n') end--;
            if (end > heading.Start + 1) result.Add(new(heading.Start, end, heading.Name));
        }
    }

    private static List<CodeRegion> MarkupRegions(string text, bool html, CancellationToken cancellation)
    {
        var result = new List<CodeRegion>();
        var stack = new Stack<(string Name, int Start, int Line)>();
        int counted = 0, line = 0;
        int LineAt(int offset) { while (counted < offset) { if (text[counted++] is '\r' or '\n') line++; } return line; }
        var comparison = html ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (int i = 0; i < text.Length; i++)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            if (text[i] != '<') continue;
            int startLine = LineAt(i);
            if (text.AsSpan(i).StartsWith("<!--")) { int end = text.IndexOf("-->", i + 4, StringComparison.Ordinal); i = end < 0 ? text.Length : end + 2; continue; }
            if (text.AsSpan(i).StartsWith("<![CDATA[")) { int end = text.IndexOf("]]>", i + 9, StringComparison.Ordinal); i = end < 0 ? text.Length : end + 2; continue; }
            if (text.AsSpan(i).StartsWith("<?")) { int end = text.IndexOf("?>", i + 2, StringComparison.Ordinal); i = end < 0 ? text.Length : end + 1; continue; }
            if (text.AsSpan(i).StartsWith("<!"))
            {
                int depth = 0; char quoted = '\0';
                for (i += 2; i < text.Length; i++)
                {
                    if (quoted != '\0') { if (text[i] == quoted) quoted = '\0'; }
                    else if (text[i] is '\'' or '\"') quoted = text[i];
                    else if (text[i] == '[') depth++;
                    else if (text[i] == ']') depth--;
                    else if (text[i] == '>' && depth == 0) break;
                }
                continue;
            }
            int start = i, p = i + 1;
            bool close = p < text.Length && text[p] == '/'; if (close) p++;
            int nameStart = p; while (p < text.Length && (char.IsLetterOrDigit(text[p]) || text[p] is '_' or ':' or '-' or '.')) p++;
            if (p == nameStart) continue;
            string name = text[nameStart..p]; char quote = '\0';
            for (; p < text.Length; p++)
            {
                if (quote != '\0') { if (text[p] == quote) quote = '\0'; }
                else if (text[p] is '\'' or '\"') quote = text[p];
                else if (text[p] == '>') break;
            }
            if (p == text.Length) break;
            i = p;
            if (close)
            {
                if (stack.Count == 0 || !stack.Peek().Name.Equals(name, comparison)) { stack.Clear(); continue; }
                var open = stack.Pop();
                if (LineAt(p) > open.Line) result.Add(new(open.Start, p + 1, "<" + name + ">…"));
            }
            else if (text[p - 1] != '/' && !(html && new[] { "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr" }.Contains(name.ToLowerInvariant())))
            {
                stack.Push((name, start, startLine));
                if (html && name.ToLowerInvariant() is "script" or "style" or "textarea")
                { int end = text.IndexOf("</" + name, p + 1, StringComparison.OrdinalIgnoreCase); if (end < 0) break; i = end - 1; }
            }
        }
        return result;
    }
}
