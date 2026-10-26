using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class MarkdownTests
{
    [Fact]
    public void CommonMarkAndGfmRenderOffline()
    {
        var source = "# Heading\n\n**bold** *italic* ~~gone~~\n\n- [x] done\n- [ ] todo\n  - nested\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n\n> quote\n\n1. first\n2. second\n\n---\n\nhttps://example.org\n\n![Local](images/test.png)\n\n```cs\nvar value = 42;\n```";
        var html = new MarkdownService().Render(source, false);
        foreach (var expected in new[] { "<h1 id=\"heading\">", "<strong>", "<em>", "<del>", "checkbox", "<table>", "<blockquote>", "<ol>", "<hr", "https://example.org", "images/test.png", "<pre><code>", "<span style=\"color:" }) Assert.Contains(expected, html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains("script-src 'none'", html);
        Assert.Contains("<style>", html);
    }

    [Fact]
    public void RawHtmlAndCodeAreInertAndEncoded()
    {
        var html = new MarkdownService().Render("<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>\n\n```html\n<script>unsafe()</script>\n```", true);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x", html);
        Assert.Contains("&lt;", html);
        Assert.Contains("class=\"dark\"", html);
        Assert.Contains("default-src 'none'", html);
    }

    [Theory]
    [InlineData(".md")][InlineData(".json")][InlineData(".xml")][InlineData(".html")][InlineData(".css")][InlineData(".js")][InlineData(".ts")][InlineData(".cs")][InlineData(".py")][InlineData(".ps1")][InlineData(".sql")][InlineData(".yaml")][InlineData(".yml")][InlineData(".sh")]
    public void RequiredSyntaxExistsInBothThemes(string extension)
    {
        var syntax = new SyntaxService();
        Assert.NotNull(syntax.ForFile("test" + extension));
        Assert.NotNull(syntax.ForFile("test" + extension, true)?.MainRuleSet);
        Assert.Null(syntax.ForFile("plain.txt"));
    }

    [Theory]
    [InlineData("https://evil.example/image.png")]
    [InlineData("file:///C:/secrets.png")]
    [InlineData("https://minimal.invalid/%2e%2e%5csecret.png")]
    [InlineData("https://minimal.invalid/C:/secret.png")]
    [InlineData("https://minimal.invalid/image.svg")]
    [InlineData("https://minimal.invalid:999/image.png")]
    [InlineData("https://minimal.invalid/file.txt")]
    public void ResourcePolicyRejectsUnsafeLocations(string address) => Assert.Null(LocalResourcePolicy.Resolve(@"C:\docs", new Uri(address)));

    [Fact]
    public void ResourcePolicyAllowsOnlyImagesUnderDocumentDirectory()
    {
        Assert.Equal(@"C:\docs\images\hello world.png", LocalResourcePolicy.Resolve(@"C:\docs", new Uri("https://minimal.invalid/images/hello%20world.png")));
        Assert.Null(LocalResourcePolicy.Resolve(null, new Uri("https://minimal.invalid/image.png")));
    }
}
