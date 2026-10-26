using System.Collections.ObjectModel;
using System.IO;

namespace Minimal.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private int counter;
    private DocumentViewModel? active;
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];
    public DocumentViewModel? Active { get => active; set { if (active == value) return; active = value; Notify(); } }
    public DocumentViewModel NewDocument() { var doc = new DocumentViewModel($"Untitled {++counter}"); Documents.Add(doc); Active = doc; return doc; }
    public DocumentViewModel? FindPath(string path) => Documents.FirstOrDefault(d => string.Equals(d.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
    public void NextTab(int direction) { if (Documents.Count > 0) Active = Documents[(Documents.IndexOf(Active!) + direction + Documents.Count) % Documents.Count]; }
    public void Remove(DocumentViewModel doc)
    {
        var index = Documents.IndexOf(doc);
        Documents.Remove(doc);
        if (Active == doc) Active = Documents.Count == 0 ? null : Documents[Math.Min(index, Documents.Count - 1)];
    }
}
