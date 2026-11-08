using System.Text;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using Minimal.Models;
using Minimal.Services;
using Minimal.ViewModels;
using Xunit;

namespace Minimal.Tests;

public sealed class FormattingTests
{
    [Theory]
    [InlineData("\n", "  ")]
    [InlineData("\r\n", "\t")]
    public void JsonPreservesRawStringAndNumberTokens(string newline, string indent)
    {
        const string input = "{\"é\": [1e+02, \"\\u0061 { }\"], \"empty\":{}}";
        var result = FormattingService.Format(input + newline, SyntaxLanguage.Json, indent, newline);
        Assert.Null(result.Error); Assert.NotNull(result.Text);
        Assert.Contains("1e+02", result.Text); Assert.Contains("\"\\u0061 { }\"", result.Text);
        Assert.Contains(newline + indent + "\"é\"", result.Text);
        Assert.EndsWith(newline, result.Text);
        Assert.Equal(result.Text, FormattingService.Format(result.Text, SyntaxLanguage.Json, indent, newline).Text);
    }
    [Fact]
    public void XmlRetainsAttributeQuotesEntitiesCommentsAndMixedContent()
    {
        const string input = "<?xml version='1.0'?><root a='&amp;'><!-- { comment } --><p>Hello <b>world</b> !</p><x xml:space='preserve'>  <a/> \n </x></root>";
        var result = FormattingService.Format(input, SyntaxLanguage.Xml, "  ", "\n");
        Assert.Null(result.Error);
        Assert.Contains("a='&amp;'", result.Text); Assert.Contains("<!-- { comment } -->", result.Text);
        Assert.Contains("<p>Hello <b>world</b> !</p>", result.Text);
        Assert.Contains("<x xml:space='preserve'>  <a/> \n </x>", result.Text);
        Assert.Equal(result.Text, FormattingService.Format(result.Text!, SyntaxLanguage.Xml, "  ", "\n").Text);
    }
    [Theory]
    [InlineData(SyntaxLanguage.Json, "{bad}")]
    [InlineData(SyntaxLanguage.Json, "{\"a\":1,}")]
    [InlineData(SyntaxLanguage.Xml, "<x></y>")]
    [InlineData(SyntaxLanguage.Xml, "<!DOCTYPE a SYSTEM 'file:///secret'><a/>")]
    [InlineData(SyntaxLanguage.CSharp, "class A{}")]
    [InlineData(SyntaxLanguage.Json, "")]
    public void InvalidAndUnsupportedSyntaxIsNotRewritten(SyntaxLanguage language, string text)
    { var result = FormattingService.Format(text, language, "\t", "\n"); Assert.NotNull(result.Error); Assert.Null(result.Text); }

    [Fact]
    public void ApplyingFormattingIsOneUndoAndPreservesEncoding()
    {
        var doc = new DocumentViewModel("Test");
        doc.Load("example.json", new("{\"a\":1}", new UnicodeEncoding(false, true, true), "\n", "original", false));
        var encoding = doc.Encoding;
        string original = doc.Buffer.Text;
        var result = FormattingService.Format(original, SyntaxLanguage.Json, "\t", "\n");
        using (doc.Buffer.RunUpdate()) doc.Buffer.Replace(0, doc.Buffer.TextLength, result.Text!);
        Assert.Same(encoding, doc.Encoding); Assert.True(doc.IsModified);
        doc.Buffer.UndoStack.Undo(); Assert.Equal(original, doc.Buffer.Text); Assert.False(doc.IsModified);
    }
    [Theory]
    [InlineData(SyntaxLanguage.Json, "{\n", "  ")]
    [InlineData(SyntaxLanguage.PowerShell, "function F {\n", "  ")]
    [InlineData(SyntaxLanguage.JavaScript, "const x = \"{\";\n", "")]
    [InlineData(SyntaxLanguage.Python, "if True:\n", "  ")]
    [InlineData(SyntaxLanguage.PlainText, "\tHello\n", "\t")]
    [InlineData(SyntaxLanguage.Json, "{\n  \"a\": 1\n}", "")]
    [InlineData(SyntaxLanguage.Xml, "<root>\n", "  ")]
    [InlineData(SyntaxLanguage.PowerShell, "{\n  # comment\n", "  ")]
    public void IndentationRespectsConstructsAndExistingWhitespace(SyntaxLanguage language, string input, string expected)
    {
        var document = new TextDocument(input);
        var strategy = new EditorIndentationStrategy(language, new TextEditorOptions { ConvertTabsToSpaces = true, IndentationSize = 2 });
        strategy.IndentLine(document, document.GetLineByNumber(document.LineCount));
        Assert.StartsWith(expected, document.GetText(document.GetLineByNumber(document.LineCount)));
        Assert.Equal(expected.Length, document.GetText(document.GetLineByNumber(document.LineCount)).TakeWhile(c => c is ' ' or '\t').Count());
    }
}
