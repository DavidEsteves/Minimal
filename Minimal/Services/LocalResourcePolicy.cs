using System.IO;

namespace Minimal.Services;

public static class LocalResourcePolicy
{
    public const string Host = "minimal.invalid";
    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".avif" };

    public static string? Resolve(string? root, Uri uri, bool imageOnly = true)
    {
        if (root is null || uri.Scheme != "https" || uri.Host != Host || !uri.IsDefaultPort) return null;
        try
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var relative = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (relative.Contains(':') || relative.Contains('\0')) return null;
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            if (imageOnly && !Images.Contains(Path.GetExtension(path))) return null;
            // Reject junctions and symlinks at every level, including the resource root.
            for (string? part = path; part is not null; part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) return null;
            return path;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }

    public static string MimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".bmp" => "image/bmp", ".ico" => "image/x-icon", ".avif" => "image/avif", _ => "image/png"
    };
}
