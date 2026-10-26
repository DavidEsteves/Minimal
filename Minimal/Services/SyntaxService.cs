using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Minimal.Models;

namespace Minimal.Services;

public sealed class SyntaxService
{
    public static IReadOnlyList<SyntaxLanguageOption> Languages { get; } = Array.AsReadOnly<SyntaxLanguageOption>(
    [
        new(SyntaxLanguage.Automatic, "Automatic (file extension)", null),
        new(SyntaxLanguage.PlainText, "Plain text", null),
        new(SyntaxLanguage.Markdown, "Markdown", ".md"),
        new(SyntaxLanguage.Json, "JSON", ".json"),
        new(SyntaxLanguage.Xml, "XML", ".xml"),
        new(SyntaxLanguage.Html, "HTML", ".html"),
        new(SyntaxLanguage.Css, "CSS", ".css"),
        new(SyntaxLanguage.JavaScript, "JavaScript", ".js"),
        new(SyntaxLanguage.TypeScript, "TypeScript", ".ts"),
        new(SyntaxLanguage.CSharp, "C#", ".cs"),
        new(SyntaxLanguage.Python, "Python", ".py"),
        new(SyntaxLanguage.PowerShell, "PowerShell", ".ps1"),
        new(SyntaxLanguage.Sql, "SQL", ".sql"),
        new(SyntaxLanguage.Yaml, "YAML", ".yaml"),
        new(SyntaxLanguage.Bash, "Bash", ".sh")
    ]);

    private readonly Dictionary<string, IHighlightingDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = ".cs", ["csharp"] = ".cs", ["js"] = ".js", ["javascript"] = ".js", ["ts"] = ".ts", ["typescript"] = ".ts",
        ["py"] = ".py", ["python"] = ".py", ["sh"] = ".sh", ["bash"] = ".sh", ["shell"] = ".sh", ["powershell"] = ".ps1", ["ps1"] = ".ps1",
        ["md"] = ".md", ["markdown"] = ".md", ["yml"] = ".yml", ["yaml"] = ".yaml", ["json"] = ".json", ["xml"] = ".xml", ["html"] = ".html", ["css"] = ".css", ["sql"] = ".sql"
    };

    public SyntaxService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".xshd")))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = XmlReader.Create(stream);
            var xshd = HighlightingLoader.LoadXshd(reader);
            var definition = HighlightingLoader.Load(xshd, HighlightingManager.Instance);
            foreach (var extension in xshd.Extensions) definitions[extension] = definition;
        }
    }

    public IHighlightingDefinition? ForFile(string? path, bool dark = false)
    {
        var extension = Path.GetExtension(path ?? "");
        var definition = definitions.GetValueOrDefault(extension) ?? HighlightingManager.Instance.GetDefinitionByExtension(extension);
        return dark && definition is not null ? new DarkDefinition(definition) : definition;
    }

    public IHighlightingDefinition? ForDocument(string? path, SyntaxLanguage language, bool dark = false) =>
        language == SyntaxLanguage.Automatic ? ForFile(path, dark) :
        language == SyntaxLanguage.PlainText ? null :
        ForFile("document" + Languages.First(option => option.Language == language).Extension, dark);

    public string LanguageName(string? path, SyntaxLanguage language)
    {
        if (language != SyntaxLanguage.Automatic) return Languages.First(option => option.Language == language).Name;
        var definition = ForFile(path);
        if (definition is null) return "Plain text";
        // Match by definition so aliases such as .yml and .markdown get the same label.
        return Languages.FirstOrDefault(option => option.Extension is not null && ReferenceEquals(ForFile("document" + option.Extension), definition))?.Name ?? definition.Name;
    }

    public string CodeToHtml(string code, string language, bool dark)
    {
        var definition = ForFile("code" + Aliases.GetValueOrDefault(language, "." + language), dark);
        if (definition is null || code.Length > 200_000) return WebUtility.HtmlEncode(code);
        var document = new TextDocument(code);
        using var highlighter = new DocumentHighlighter(document, definition);
        var result = new StringBuilder();
        foreach (var line in document.Lines)
        {
            var highlighted = highlighter.HighlightLine(line.LineNumber);
            // Generate trusted spans only; source is always HTML encoded.
            var boundaries = highlighted.Sections.SelectMany(s => new[] { s.Offset, s.Offset + s.Length }).Append(line.Offset).Append(line.EndOffset).Distinct().Order().ToArray();
            for (int i = 0; i + 1 < boundaries.Length; i++)
            {
                int start = boundaries[i], end = boundaries[i + 1];
                var color = highlighted.Sections.LastOrDefault(s => s.Offset <= start && s.Offset + s.Length >= end)?.Color.Foreground?.GetColor(null);
                var text = WebUtility.HtmlEncode(document.GetText(start, end - start));
                if (color is Color c) result.Append($"<span style=\"color:#{c.R:X2}{c.G:X2}{c.B:X2}\">{text}</span>");
                else result.Append(text);
            }
            if (line.DelimiterLength > 0) result.Append('\n');
        }
        return result.ToString();
    }

    // Keep language rules intact, replacing dark-on-light token colors with readable equivalents.
    private sealed class DarkDefinition(IHighlightingDefinition source) : IHighlightingDefinition
    {
        private readonly Dictionary<HighlightingRuleSet, HighlightingRuleSet> sets = new();
        public string Name => source.Name;
        public HighlightingRuleSet MainRuleSet => Clone(source.MainRuleSet);
        public IEnumerable<HighlightingColor> NamedHighlightingColors => source.NamedHighlightingColors.Select(Colorize);
        public IDictionary<string, string> Properties => source.Properties;
        public HighlightingColor GetNamedColor(string name) => Colorize(source.GetNamedColor(name));
        public HighlightingRuleSet GetNamedRuleSet(string name) => Clone(source.GetNamedRuleSet(name));
        private static HighlightingColor Colorize(HighlightingColor color)
        {
            if (color is null) return null!;
            var clone = color.Clone();
            if (color.Foreground?.GetColor(null) is Color c)
            {
                byte Lift(byte channel) => (byte)(130 + channel * 125 / 255);
                clone.Foreground = new SimpleHighlightingBrush(Color.FromRgb(Lift(c.R), Lift(c.G), Lift(c.B)));
            }
            clone.Background = null;
            return clone;
        }
        private HighlightingRuleSet Clone(HighlightingRuleSet set)
        {
            if (set is null) return null!;
            if (sets.TryGetValue(set, out var existing)) return existing;
            var result = new HighlightingRuleSet { Name = set.Name };
            sets[set] = result;
            foreach (var rule in set.Rules) result.Rules.Add(new HighlightingRule { Regex = rule.Regex, Color = Colorize(rule.Color) });
            foreach (var span in set.Spans) result.Spans.Add(new HighlightingSpan { StartExpression = span.StartExpression, EndExpression = span.EndExpression, StartColor = Colorize(span.StartColor), EndColor = Colorize(span.EndColor), SpanColor = Colorize(span.SpanColor), RuleSet = Clone(span.RuleSet), SpanColorIncludesStart = span.SpanColorIncludesStart, SpanColorIncludesEnd = span.SpanColorIncludesEnd });
            return result;
        }
    }
}
