using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Minimal.Models;

namespace Minimal.Services;

// Each process owns a lease. Recovery never reads a live instance or overwrites its
// checkpoint. Old checkpoints are retired only after a new durable manifest exists.
public sealed class SessionManager : IDisposable
{
    private readonly string root;
    private readonly string directory;
    private readonly FileStream lease;
    private readonly List<(string Directory, FileStream Lease)> claimed = [];
    private readonly SemaphoreSlim writes = new(1);
    private readonly Dictionary<Guid, (ICSharpCode.AvalonEdit.Document.ITextSourceVersion Version, string Hash)> contentCache = [];
    public string DirectoryPath => directory;
    public SessionManager(string? root = null)
    {
        this.root = Path.GetFullPath(root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Minimal", "Sessions"));
        directory = Path.Combine(this.root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        lease = Lock(directory);
    }
    private static FileStream Lock(string path) => new(Path.Combine(path, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    public async Task<SessionRecovery> RecoverAsync()
    {
        var tabs = new List<RecoveredTab>(); var notices = new List<string>();
        var seen = new HashSet<Guid>(); Guid? active = null;
        foreach (string candidate in Directory.EnumerateDirectories(root).Where(x => x != directory).OrderByDescending(x => File.GetLastWriteTimeUtc(Path.Combine(x, "session.json"))))
        {
            if (!Guid.TryParseExact(Path.GetFileName(candidate), "N", out _)) continue;
            FileStream candidateLease;
            try { candidateLease = Lock(candidate); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            bool canRetire = true;
            try
            {
                var state = await ReadManifestAsync(candidate);
                if (state is null) continue;
                if (state.Version != 1 || state.Tabs is null || state.Tabs.Any(x => x is null)) throw new InvalidDataException("Unknown or invalid recovery file version.");
                if (active is null) active = state.ActiveId;
                foreach (var tab in state.Tabs)
                {
                    if (!seen.Add(tab.Id)) continue;
                    string? content = null;
                    try
                    {
                        if (tab.ContentHash is not null)
                        {
                            string blob = BlobPath(candidate, tab.ContentHash);
                            byte[] bytes = await File.ReadAllBytesAsync(blob).ConfigureAwait(false);
                            if (Convert.ToHexString(SHA256.HashData(bytes)) != tab.ContentHash) throw new InvalidDataException("Recovery content failed its integrity check.");
                            content = new UTF8Encoding(false, true).GetString(bytes);
                        }
                        if ((tab.IsModified || tab.FilePath is null) && content is null) throw new InvalidDataException("Recovery content is missing.");
                        tabs.Add(new(tab, content));
                    }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
                    { canRetire = false; notices.Add($"Could not recover {tab.Name}: {ex.Message} The recovery files were retained."); }
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
            { canRetire = false; notices.Add("A previous recovery checkpoint could not be read. Its files were retained. " + ex.Message); }
            finally
            {
                if (canRetire) claimed.Add((candidate, candidateLease));
                else candidateLease.Dispose();
            }
        }
        return new(active, tabs, notices);
    }
    private static async Task<SessionState?> ReadManifestAsync(string directory)
    {
        foreach (string name in new[] { "session.json", "session.bak" })
        {
            string path = Path.Combine(directory, name);
            try
            {
                if (!File.Exists(path)) continue;
                if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Recovery manifest is too large.");
                return JsonSerializer.Deserialize<SessionState>(await File.ReadAllTextAsync(path).ConfigureAwait(false)) ?? throw new JsonException("Empty recovery manifest.");
            }
            catch (Exception ex) when (name == "session.json" && File.Exists(Path.Combine(directory, "session.bak")) && ex is IOException or InvalidDataException or JsonException) { }
        }
        return null;
    }

    public async Task SaveAsync(SessionCapture capture, bool forgetPrevious = false)
    {
        await writes.WaitAsync().ConfigureAwait(false);
        try
        {
            var tabs = new List<SessionTab>();
            foreach (var captured in capture.Tabs)
            {
                string? hash = null;
                if (captured.Content is not null)
                {
                    if (captured.ContentVersion is not null && contentCache.TryGetValue(captured.State.Id, out var cached) && File.Exists(BlobPath(directory, cached.Hash)) && cached.Version.BelongsToSameDocumentAs(captured.ContentVersion) && cached.Version.CompareAge(captured.ContentVersion) == 0) hash = cached.Hash;
                    else
                    {
                        byte[] bytes = new UTF8Encoding(false, true).GetBytes(captured.Content.Text);
                        hash = Convert.ToHexString(SHA256.HashData(bytes));
                        string path = BlobPath(directory, hash);
                        if (!File.Exists(path)) await AtomicWriteAsync(path, bytes).ConfigureAwait(false);
                        if (captured.ContentVersion is not null) contentCache[captured.State.Id] = (captured.ContentVersion, hash);
                    }
                }
                else contentCache.Remove(captured.State.Id);
                tabs.Add(captured.State with { ContentHash = hash });
            }
            var state = new SessionState(1, capture.ActiveId, tabs);
            byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(state);
            await AtomicWriteAsync(Path.Combine(directory, "session.json"), manifest, true).ConfigureAwait(false);
            // Explicit tab closes/discards must not be resurrected by a backup fallback.
            if (forgetPrevious) await AtomicWriteAsync(Path.Combine(directory, "session.bak"), manifest).ConfigureAwait(false);
            foreach (var old in claimed.ToArray())
            {
                old.Lease.Dispose();
                TryDeleteCheckpoint(old.Directory);
                claimed.Remove(old);
            }
            // Keep content referenced by either generation, so the backup remains usable.
            var hashes = tabs.Select(x => x.ContentHash).Where(x => x is not null).ToHashSet();
            string backup = Path.Combine(directory, "session.bak");
            if (File.Exists(backup))
                try { foreach (var tab in JsonSerializer.Deserialize<SessionState>(await File.ReadAllTextAsync(backup).ConfigureAwait(false))?.Tabs ?? []) hashes.Add(tab.ContentHash); }
                catch (JsonException) { return; }
            foreach (string blob in Directory.EnumerateFiles(directory, "*.txt"))
                if (!hashes.Contains(Path.GetFileNameWithoutExtension(blob))) File.Delete(blob);
            foreach (var id in contentCache.Keys.Where(id => !tabs.Any(x => x.Id == id)).ToArray()) contentCache.Remove(id);
        }
        finally { writes.Release(); }
    }
    public async Task ClearAsync()
    {
        await writes.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory).Where(x => Path.GetFileName(x) != "session.lock")) File.Delete(path);
            foreach (var old in claimed) { old.Lease.Dispose(); TryDeleteCheckpoint(old.Directory); }
            claimed.Clear(); contentCache.Clear();
        }
        finally { writes.Release(); }
    }
    public Task ClearInactiveAsync() => Task.Run(() =>
    {
        foreach (string candidate in Directory.EnumerateDirectories(root).Where(x => x != directory))
        {
            if (!Guid.TryParseExact(Path.GetFileName(candidate), "N", out _)) continue;
            try { using (Lock(candidate)) { } TryDeleteCheckpoint(candidate); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    });
    public void RetainRecoveredCheckpoints()
    {
        foreach (var old in claimed) old.Lease.Dispose();
        claimed.Clear();
    }
    private void TryDeleteCheckpoint(string path)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) != root || !Guid.TryParseExact(Path.GetFileName(path), "N", out _)) return;
        try
        {
            // No recursive delete: these directories contain only our checkpoint files.
            foreach (var file in Directory.EnumerateFiles(path)) File.Delete(file);
            Directory.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static string BlobPath(string directory, string hash)
    {
        if (hash.Length != 64 || hash.Any(c => !char.IsAsciiHexDigit(c))) throw new InvalidDataException("Invalid recovery content identifier.");
        return Path.Combine(directory, hash + ".txt");
    }
    private static async Task AtomicWriteAsync(string path, byte[] bytes, bool backup = false)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            { await stream.WriteAsync(bytes).ConfigureAwait(false); await stream.FlushAsync().ConfigureAwait(false); stream.Flush(true); }
            if (backup && File.Exists(path)) File.Replace(temporary, path, Path.Combine(Path.GetDirectoryName(path)!, "session.bak"));
            else File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Dispose()
    {
        foreach (var old in claimed) old.Lease.Dispose();
        claimed.Clear(); lease.Dispose();
    }
}
