namespace Minimal.Models;

public enum SyntaxLanguage
{
    Automatic,
    PlainText,
    Markdown,
    Json,
    Xml,
    Html,
    Css,
    JavaScript,
    TypeScript,
    CSharp,
    Python,
    PowerShell,
    Sql,
    Yaml,
    Bash
}

public sealed record SyntaxLanguageOption(SyntaxLanguage Language, string Name, string? Extension);
