using System.Text;

namespace Minimal.Models;

public sealed record FileSnapshot(string Text, Encoding Encoding, string LineEnding, string Fingerprint, bool UsedFallback);
