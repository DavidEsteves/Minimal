using Minimal.Models;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class CodeStructureTests
{
    [Theory]
    [InlineData(SyntaxLanguage.CSharp, "{ /* } */ var a = @\"{\"; var b = \"}\"; ([]) }")]
    [InlineData(SyntaxLanguage.CSharp, "{ var a = \"\"\" { } \"\"\"; }")]
    [InlineData(SyntaxLanguage.PowerShell, "{ <# } <# { #> #> '}' # }\n ([])}")]
    [InlineData(SyntaxLanguage.JavaScript, "{ const a = `}`; const r = /[{}]/; // }\n([]) }")]
    [InlineData(SyntaxLanguage.Json, "{\"}\\\"[\": [1, 2]}")]
    public void BracketsIgnoreStringsAndComments(SyntaxLanguage language, string text)
    {
        var result = CodeStructureService.Analyze(text, language);
        Assert.Equal(text.Length - 1, result.Brackets[0]);
        Assert.Equal((0, text.Length - 1), result.PairAt(0));
        Assert.Equal((text.Length - 1, 0), result.PairAt(text.Length));
    }
    [Fact]
    public void UnmatchedAndOversizedDocumentsAreSafe()
    {
        Assert.Null(CodeStructureService.Analyze("{]", SyntaxLanguage.CSharp).PairAt(0));
        Assert.Empty(CodeStructureService.Analyze(new string('{', CodeStructureService.MaximumLength + 1), SyntaxLanguage.CSharp).Brackets);
        Assert.Empty(CodeStructureService.Analyze("", SyntaxLanguage.CSharp).Brackets);
    }
    [Theory]
    [InlineData(SyntaxLanguage.CSharp, "namespace A {\n class B {\n void F() {\n var s = \"}\";\n }\n }\n}", 3)]
    [InlineData(SyntaxLanguage.PowerShell, "function F {\n & {\n '$x }'\n }\n}", 2)]
    [InlineData(SyntaxLanguage.Json, "{\n\"a\": [\n{\n\"b\": \"}\"\n}\n]\n}", 3)]
    [InlineData(SyntaxLanguage.Xml, "<root>\n<!-- <fake> -->\n<child x=\"<\">\n<![CDATA[ </child> ]]>\n</child>\n</root>", 2)]
    [InlineData(SyntaxLanguage.Html, "<html>\n<br><script>\nconst x = '<tag>';\n</script>\n</html>", 2)]
    [InlineData(SyntaxLanguage.Markdown, "# One\ntext\n```\n## fake\n```\n## Two\ntext\n# Three\ntext", 3)]
    [InlineData(SyntaxLanguage.Python, "def f():\n  pass\n", 0)]
    public void CreatesNestedLanguageAwareFolds(SyntaxLanguage language, string text, int count) =>
        Assert.Equal(count, CodeStructureService.Analyze(text, language).Regions.Count);

    [Fact]
    public void AnalysisIsCancellable()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CodeStructureService.Analyze("{\n}", SyntaxLanguage.CSharp, cancellation.Token));
    }
}
