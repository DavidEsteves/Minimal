using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Indentation;
using Minimal.Models;
using System.Text.RegularExpressions;

namespace Minimal.Services;

public sealed class EditorIndentationStrategy(SyntaxLanguage language, TextEditorOptions options) : DefaultIndentationStrategy
{
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        if (line.PreviousLine is not { } previous) return;
        string previousText = document.GetText(previous);
        string indentation = new(previousText.TakeWhile(c => c is ' ' or '\t').ToArray());
        string currentText = document.GetText(line);
        int existing = currentText.TakeWhile(c => c is ' ' or '\t').Count();
        if (document.TextLength <= 200000)
        {
            string masked = CodeStructureService.MaskNonCode(document.GetText(0, line.EndOffset), language);
            string code = masked.Substring(previous.Offset, previous.Length).TrimEnd();
            bool structural = language is SyntaxLanguage.Json or SyntaxLanguage.JavaScript or SyntaxLanguage.TypeScript or SyntaxLanguage.PowerShell or SyntaxLanguage.Css;
            bool increase = structural && code.Length > 0 && code[^1] is '{' or '[' or '(';
            if (language is SyntaxLanguage.Python or SyntaxLanguage.Yaml) increase = code.EndsWith(':');
            if (language is SyntaxLanguage.Xml or SyntaxLanguage.Html)
            {
                try
                {
                    var tag = Regex.Match(code, @"<([\w:.-]+)(?:\s[^<>]*)?(?<!/)>$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                    increase = tag.Success && !(language == SyntaxLanguage.Html && new[] { "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed", "param", "source", "track", "wbr" }.Contains(tag.Groups[1].Value.ToLowerInvariant()));
                }
                catch (RegexMatchTimeoutException) { increase = false; }
            }
            if (increase) indentation += options.IndentationString;
            string current = masked.Substring(line.Offset, line.Length).TrimStart(' ', '\t');
            bool decrease = structural && current.Length > 0 && current[0] is '}' or ']' or ')' || language is SyntaxLanguage.Xml or SyntaxLanguage.Html && current.StartsWith("</", StringComparison.Ordinal);
            if (decrease && indentation.Length > 0)
                indentation = RemoveLevel(indentation, options.IndentationSize);
        }
        document.Replace(line.Offset, existing, indentation);
    }
    private static string RemoveLevel(string text, int size)
    {
        if (text.EndsWith('\t')) return text[..^1];
        int count = 0; for (int i = text.Length - 1; i >= 0 && text[i] == ' ' && count < size; i--) count++;
        return text[..(text.Length - count)];
    }
}
