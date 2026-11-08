using System.Collections.ObjectModel;
using System.IO;

namespace Minimal.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private int counter;
    private DocumentViewModel? active;
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];
    public DocumentViewModel? Active { get => active; set { if (active == value) return; active = value; Notify(); } }
    public DocumentViewModel NewDocument()
    {
        string name; do { name = $"Untitled {++counter}"; } while (Documents.Any(d => d.UntitledName == name));
        var doc = new DocumentViewModel(name); Documents.Add(doc); Active = doc; return doc;
    }
    public DocumentViewModel? FindPath(string path) => Documents.FirstOrDefault(d => string.Equals(d.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
    public void NextTab(int direction) { if (Documents.Count > 0) Active = Documents[(Documents.IndexOf(Active!) + direction + Documents.Count) % Documents.Count]; }
    public void Remove(DocumentViewModel doc)
    {
        var index = Documents.IndexOf(doc);
        bool wasActive = Active == doc;
        Documents.Remove(doc);
        // The bound tab selector may clear Active during the collection event.
        if (wasActive) Active = Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
    }
}
