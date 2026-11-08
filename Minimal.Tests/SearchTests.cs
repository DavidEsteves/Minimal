using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class SearchTests
{
    [Fact]
    public void FindsUnicodeWithPositionsAndBoundedPreview()
    {
        var result = SearchService.Find("αβ test\r\nTESTing test\n测试 test", new("test", WholeWord: true));
        Assert.Null(result.Error);
        Assert.Equal(new[] { (1, 4), (2, 9), (3, 4) }, result.Hits.Select(x => (x.Line, x.Column)));
        Assert.Equal("αβ test", result.Hits[0].Preview);
        Assert.Single(SearchService.Find("aA aa", new("aa", MatchCase: true)).Hits);
    }
    [Fact]
    public void RegexCapturesAndLiteralReplacementHaveDifferentSemantics()
    {
        Assert.Equal("two one\r\ntwo one", SearchService.Replace("one two\r\none two", new(@"(one) (two)", RegularExpression: true), "$2 $1"));
        Assert.Equal("$1 $1", SearchService.Replace("x x", new("x"), "$1"));
        Assert.Equal("Y x", SearchService.Replace("x x", new("x"), "Y", 0));
    }
    [Fact]
    public void HandlesEmptyZeroWidthInvalidAndSlowQueries()
    {
        Assert.Empty(SearchService.Find("test", new("")).Hits);
        Assert.Equal(3, SearchService.Find("a\nb\n", new("^", RegularExpression: true)).Hits.Count);
        Assert.NotNull(SearchService.Find("test", new("[", RegularExpression: true)).Error);
        Assert.NotNull(SearchService.Find(new string('a', 10000) + "!", new("(a+)+$", RegularExpression: true)).Error);
    }
    [Fact]
    public void LargeDocumentResultsRemainComplete()
    {
        string text = string.Concat(Enumerable.Repeat("match\n", 100000));
        var result = SearchService.Find(text, new("match"));
        Assert.Null(result.Error); Assert.Equal(100000, result.Hits.Count);
        Assert.Equal(100000, result.Hits[^1].Line);
    }
}
