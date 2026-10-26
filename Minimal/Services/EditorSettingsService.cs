using System.IO;
using System.Text.Json;
using Minimal.Models;

namespace Minimal.Services;

public sealed class EditorSettingsService(string? path = null)
{
    private readonly string path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Minimal", "editor-settings.json");

    public EditorSettings Load()
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 16_384) return new();
            return (JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(path)) ?? new()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public async Task SaveAsync(EditorSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(settings.Normalize(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(temporary, json).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
