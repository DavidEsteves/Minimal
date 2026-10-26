using System.IO;
using System.Security.Cryptography;
using System.Text;
using Minimal.Models;

namespace Minimal.Services;

public sealed class FileService
{
    static FileService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    public static Encoding LegacyEncoding => Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    public async Task<FileSnapshot> LoadAsync(string path, Encoding? forcedEncoding = null)
    {
        var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        var (encoding, skip) = DetectEncoding(bytes);
        if (forcedEncoding is not null) { encoding = forcedEncoding; skip = bytes.AsSpan().StartsWith(encoding.GetPreamble()) ? encoding.GetPreamble().Length : 0; }
        bool fallback = false;
        string text;
        try { text = encoding.GetString(bytes, skip, bytes.Length - skip); }
        catch (DecoderFallbackException) when (forcedEncoding is null && skip == 0)
        { encoding = LegacyEncoding; text = encoding.GetString(bytes); fallback = true; }
        return new(text, encoding, DetectLineEnding(text), Hash(bytes), fallback);
    }

    public async Task<string?> FingerprintAsync(string path)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(false));
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task<string> SaveAsync(string path, string text, Encoding encoding, string? expectedFingerprint, bool checkConflict = true)
    {
        path = Path.GetFullPath(path);
        var bytes = await Task.Run(() =>
        {
            var preamble = encoding.GetPreamble();
            var output = new byte[preamble.Length + encoding.GetByteCount(text)];
            preamble.CopyTo(output, 0);
            encoding.GetBytes(text.AsSpan(), output.AsSpan(preamble.Length));
            return output;
        }).ConfigureAwait(false);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }
            if (checkConflict && await FingerprintAsync(path).ConfigureAwait(false) != expectedFingerprint)
                throw new FileConflictException();
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return Hash(bytes);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static (Encoding, int) DetectEncoding(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) return (new UTF32Encoding(false, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) return (new UTF32Encoding(true, true, true), 4);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) return (new UTF8Encoding(true, true), 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE })) return (new UnicodeEncoding(false, true, true), 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF })) return (new UnicodeEncoding(true, true, true), 2);
        return (new UTF8Encoding(false, true), 0);
    }
    public static string DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0, cr = 0;
        for (int i = 0; i < text.Length; i++)
            if (text[i] == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; } else cr++; }
            else if (text[i] == '\n') lf++;
        return crlf >= lf && crlf >= cr ? "\r\n" : lf >= cr ? "\n" : "\r";
    }
}

public sealed class FileConflictException : IOException
{
    public FileConflictException() : base("The file changed on disk while saving. Your document has not been overwritten.") { }
}
