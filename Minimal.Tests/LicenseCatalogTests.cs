using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class LicenseCatalogTests
{
    [Fact]
    public void AllComponentNoticesAreAvailableFromAssemblyResources()
    {
        var notices = LicenseCatalog.Load();
        Assert.Equal(4, notices.Count);
        Assert.Contains("MIT License", notices[0].Text);
        Assert.Contains("AvalonEdit Contributors", notices[0].Text);
        Assert.Contains("Alexandre Mutel", notices[1].Text);
        Assert.Contains("Microsoft Corporation", notices[2].Text);
        Assert.Contains("Antlr3.Runtime", notices[3].Text);
        Assert.Contains("StringTemplate4", notices[3].Text);
        foreach (var notice in notices) Assert.False(string.IsNullOrWhiteSpace(notice.Text));
    }
    [Fact]
    public void ComponentVersionsComeFromBuildMetadata()
    {
        var notices = LicenseCatalog.Load();
        foreach (var notice in notices.Where(x => x.Version is not null))
            Assert.True(Version.TryParse(notice.Version, out _), notice.DisplayName);
        Assert.Equal(notices[2].Version, notices[3].Version);
        Assert.True(Version.TryParse(LicenseCatalog.ApplicationVersion, out _));
    }
}
