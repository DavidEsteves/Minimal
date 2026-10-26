using Minimal.Models;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class SyntaxSelectionTests
{
    [Theory]
    [InlineData(SyntaxLanguage.Markdown, ".md")]
    [InlineData(SyntaxLanguage.Json, ".json")]
    [InlineData(SyntaxLanguage.Xml, ".xml")]
    [InlineData(SyntaxLanguage.Html, ".html")]
    [InlineData(SyntaxLanguage.Css, ".css")]
    [InlineData(SyntaxLanguage.JavaScript, ".js")]
    [InlineData(SyntaxLanguage.TypeScript, ".ts")]
    [InlineData(SyntaxLanguage.CSharp, ".cs")]
    [InlineData(SyntaxLanguage.Python, ".py")]
    [InlineData(SyntaxLanguage.PowerShell, ".ps1")]
    [InlineData(SyntaxLanguage.Sql, ".sql")]
    [InlineData(SyntaxLanguage.Yaml, ".yaml")]
    [InlineData(SyntaxLanguage.Bash, ".sh")]
    public void ManualSelectionOverridesExtensionInBothThemes(SyntaxLanguage language, string extension)
    {
        var syntax = new SyntaxService();
        var expected = syntax.ForFile("example" + extension);
        Assert.NotNull(expected);
        Assert.Same(expected, syntax.ForDocument("example.txt", language));
        Assert.Equal(expected.Name, syntax.ForDocument(null, language, true)?.Name);
    }

    [Fact]
    public void AutomaticTracksPathWhilePlainTextDisablesHighlighting()
    {
        var syntax = new SyntaxService();
        Assert.Same(syntax.ForFile("example.py"), syntax.ForDocument("example.py", SyntaxLanguage.Automatic));
        Assert.Same(syntax.ForFile("example.cs"), syntax.ForDocument("example.cs", SyntaxLanguage.Automatic));
        Assert.Null(syntax.ForDocument("example.cs", SyntaxLanguage.PlainText));
        Assert.Null(syntax.ForDocument(null, SyntaxLanguage.Automatic));
        Assert.Equal("YAML", syntax.LanguageName("example.yml", SyntaxLanguage.Automatic));
        Assert.Equal("Markdown", syntax.LanguageName("example.markdown", SyntaxLanguage.Automatic));
        Assert.Equal("Python", syntax.LanguageName("example.txt", SyntaxLanguage.Python));
        Assert.Equal("Plain text", syntax.LanguageName("example.cs", SyntaxLanguage.PlainText));
    }
}
