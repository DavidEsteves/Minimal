using System.Text;
using System.IO;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

public sealed class FileServiceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Minimal-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileService files = new();
    public FileServiceTests() => Directory.CreateDirectory(directory);
    private string FilePath => Path.Combine(directory, "日本語-é.txt");

    [Theory]
    [InlineData("utf8", "\r\n")]
    [InlineData("utf8bom", "\n")]
    [InlineData("utf16le", "\r")]
    [InlineData("utf16be", "\r\n")]
    [InlineData("utf32le", "\n")]
    [InlineData("utf32be", "\r\n")]
    [InlineData("1252", "\n")]
    public async Task LoadAndSavePreserveBytes(string kind, string ending)
    {
        Encoding encoding = kind switch
        {
            "utf8bom" => new UTF8Encoding(true, true), "utf16le" => new UnicodeEncoding(false, true, true),
            "utf16be" => new UnicodeEncoding(true, true, true), "utf32le" => new UTF32Encoding(false, true, true),
            "utf32be" => new UTF32Encoding(true, true, true), "1252" => FileService.LegacyEncoding, _ => new UTF8Encoding(false, true)
        };
        var text = "café €" + ending + (kind == "1252" ? "fin" : "日本語 😀") + ending;
        var original = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        await File.WriteAllBytesAsync(FilePath, original);
        var loaded = await files.LoadAsync(FilePath);
        Assert.Equal(text, loaded.Text);
        Assert.Equal(ending, loaded.LineEnding);
        await files.SaveAsync(FilePath, loaded.Text, loaded.Encoding, loaded.Fingerprint);
        Assert.Equal(original, await File.ReadAllBytesAsync(FilePath));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task EmptyAndMixedNewlinesRoundTripWithoutNormalization()
    {
        foreach (var source in new[] { "", "a\r\nb\nc\rd\r\n" })
        {
            await File.WriteAllTextAsync(FilePath, source, new UTF8Encoding(false));
            var loaded = await files.LoadAsync(FilePath);
            await files.SaveAsync(FilePath, loaded.Text, loaded.Encoding, loaded.Fingerprint);
            Assert.Equal(source, await File.ReadAllTextAsync(FilePath));
        }
    }

    [Fact]
    public async Task ExternalChangesAreNeverSilentlyOverwritten()
    {
        await File.WriteAllTextAsync(FilePath, "first");
        var original = await files.LoadAsync(FilePath);
        await File.WriteAllTextAsync(FilePath, "external");
        await Assert.ThrowsAsync<FileConflictException>(() => files.SaveAsync(FilePath, "edited", original.Encoding, original.Fingerprint));
        Assert.Equal("external", await File.ReadAllTextAsync(FilePath));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public async Task DeletedFileRequiresExplicitConflictResolution()
    {
        await File.WriteAllTextAsync(FilePath, "first");
        var loaded = await files.LoadAsync(FilePath);
        File.Delete(FilePath);
        Assert.Null(await files.FingerprintAsync(FilePath));
        await Assert.ThrowsAsync<FileConflictException>(() => files.SaveAsync(FilePath, "edited", loaded.Encoding, loaded.Fingerprint));
        await files.SaveAsync(FilePath, "edited", loaded.Encoding, null);
        Assert.Equal("edited", await File.ReadAllTextAsync(FilePath));
    }

    [Fact]
    public async Task LegacyFallbackIsReportedAndEncodingLossFails()
    {
        await File.WriteAllBytesAsync(FilePath, [0xE9]);
        var loaded = await files.LoadAsync(FilePath);
        Assert.True(loaded.UsedFallback);
        Assert.Equal("é", loaded.Text);
        await Assert.ThrowsAsync<EncoderFallbackException>(() => files.SaveAsync(FilePath, "日本語", loaded.Encoding, loaded.Fingerprint));
        Assert.Equal(new byte[] { 0xE9 }, await File.ReadAllBytesAsync(FilePath));
    }

    [Fact]
    public async Task ReadOnlyFileStaysIntactOnFailedSave()
    {
        await File.WriteAllTextAsync(FilePath, "original");
        var loaded = await files.LoadAsync(FilePath);
        File.SetAttributes(FilePath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => files.SaveAsync(FilePath, "changed", loaded.Encoding, loaded.Fingerprint));
            Assert.Equal("original", await File.ReadAllTextAsync(FilePath));
        }
        finally { File.SetAttributes(FilePath, FileAttributes.Normal); }
    }

    [Fact]
    public async Task InvalidBomEncodingFailsRatherThanReplacingCharacters()
    {
        await File.WriteAllBytesAsync(FilePath, [0xEF, 0xBB, 0xBF, 0xFF]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => files.LoadAsync(FilePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
