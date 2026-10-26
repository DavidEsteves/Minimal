using Minimal.ViewModels;

namespace Minimal.Services;

public enum SaveChangesChoice { Cancel, Save, Discard }

public static class DocumentCloseService
{
    public static Task<bool> CanCloseAsync(DocumentViewModel document,
        Func<DocumentViewModel, SaveChangesChoice> ask, Func<DocumentViewModel, Task<bool>> save)
    {
        if (!document.IsModified) return Task.FromResult(true);
        return ask(document) switch
        {
            SaveChangesChoice.Save => save(document),
            SaveChangesChoice.Discard => Task.FromResult(true),
            _ => Task.FromResult(false)
        };
    }
}
