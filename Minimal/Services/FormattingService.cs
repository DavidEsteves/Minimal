using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Minimal.Models;

namespace Minimal.Services;

public sealed record FormattingResult(string? Text, string? Error = null);
public static class FormattingService
{
    public static bool Supports(SyntaxLanguage language) => language is SyntaxLanguage.Json or SyntaxLanguage.Xml;
    public static FormattingResult Format(string text, SyntaxLanguage language, string indentation, string lineEnding)
    {
        if (!Supports(language)) return new(null, "Formatting is available for JSON and XML.");
        if (text.Length > CodeStructureService.MaximumLength) return new(null, "Formatting is limited to 5 MB to keep editing responsive.");
        try
        {
            string formatted = language == SyntaxLanguage.Json ? Json(text, indentation, lineEnding) : Xml(text, indentation, lineEnding);
            if (text.EndsWith('\n') || text.EndsWith('\r')) formatted += lineEnding;
            return new(formatted);
        }
        catch (Exception ex) when (ex is JsonException or XmlException or InvalidOperationException or RegexMatchTimeoutException) { return new(null, "Cannot format this document: " + ex.Message); }
    }
    private static string Json(string text, string indent, string newline)
    {
        using var validated = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 256 });
        var output = new StringBuilder(); int depth = 0;
        void NewLine() { output.Append(newline); for (int d = 0; d < depth; d++) output.Append(indent); }
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c == '\"')
            {
                int start = i++;
                while (i < text.Length) { if (text[i] == '\\') i += 2; else if (text[i++] == '\"') break; }
                output.Append(text, start, i - start); i--; continue;
            }
            if (c is '{' or '[')
            {
                output.Append(c); depth++;
                int next = i + 1; while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                if (next < text.Length && text[next] is '}' or ']') { output.Append(text[next]); depth--; i = next; }
                else NewLine();
            }
            else if (c is '}' or ']') { depth--; NewLine(); output.Append(c); }
            else if (c == ',') { output.Append(c); NewLine(); }
            else if (c == ':') output.Append(": ");
            else output.Append(c);
        }
        return output.ToString();
    }
    private sealed class Node(string raw, bool text = false, bool preserve = false)
    {
        public string Raw { get; } = raw;
        public bool Text { get; } = text;
        public bool Preserve { get; } = preserve;
        public string? Close { get; set; }
        public List<Node> Children { get; } = [];
    }
    private static string Xml(string text, string indent, string newline)
    {
        using (var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = CodeStructureService.MaximumLength }))
            while (reader.Read()) { }
        // Validate with XmlReader, then retain raw tokens. XmlWriter/XDocument serialization
        // can change quote style, entity references and attribute values unnecessarily.
        var root = new Node(""); var stack = new Stack<Node>(); stack.Push(root);
        for (int i = 0; i < text.Length;)
        {
            if (text[i] != '<')
            { int end = text.IndexOf('<', i); if (end < 0) end = text.Length; stack.Peek().Children.Add(new(text[i..end], true)); i = end; continue; }
            int start = i; string? terminator = text.AsSpan(i).StartsWith("<!--") ? "-->" : text.AsSpan(i).StartsWith("<![CDATA[") ? "]]>" : text.AsSpan(i).StartsWith("<?") ? "?>" : null;
            if (terminator is not null)
            {
                i = text.IndexOf(terminator, i + 2, StringComparison.Ordinal) + terminator.Length;
                bool cdata = terminator == "]]>";
                stack.Peek().Children.Add(new(text[start..i], cdata)); continue;
            }
            char quote = '\0';
            for (i++; i < text.Length; i++)
            {
                if (quote != '\0') { if (text[i] == quote) quote = '\0'; }
                else if (text[i] is '\'' or '\"') quote = text[i];
                else if (text[i] == '>') { i++; break; }
            }
            string raw = text[start..i];
            if (raw.StartsWith("</", StringComparison.Ordinal)) { stack.Pop().Close = raw; continue; }
            bool preserve = stack.Peek().Preserve;
            var space = Regex.Match(raw, "\\bxml:space\\s*=\\s*(['\"])(preserve|default)\\1", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            if (space.Success) preserve = space.Groups[2].Value == "preserve";
            var node = new Node(raw, preserve: preserve); stack.Peek().Children.Add(node);
            if (!raw.EndsWith("/>", StringComparison.Ordinal))
            {
                if (stack.Count > 256) throw new XmlException("XML nesting exceeds the formatting limit of 256 levels.");
                stack.Push(node);
            }
        }
        var output = new StringBuilder();
        void Write(Node node, int depth, bool preserve)
        {
            output.Append(node.Raw);
            bool mixed = node.Children.Any(x => x.Text && !string.IsNullOrWhiteSpace(x.Raw));
            bool keep = preserve || node.Preserve || mixed;
            bool layout = !keep && node.Children.Any(x => !x.Text);
            foreach (var child in node.Children)
            {
                if (layout && child.Text && string.IsNullOrWhiteSpace(child.Raw)) continue;
                if (layout) { output.Append(newline); for (int d = 0; d <= depth; d++) output.Append(indent); }
                Write(child, depth + 1, keep);
            }
            if (layout && node.Close is not null) { output.Append(newline); for (int d = 0; d < depth; d++) output.Append(indent); }
            output.Append(node.Close);
        }
        bool first = true;
        foreach (var node in root.Children.Where(x => !x.Text || !string.IsNullOrWhiteSpace(x.Raw)))
        { if (!first) output.Append(newline); Write(node, 0, false); first = false; }
        return output.ToString();
    }
}
