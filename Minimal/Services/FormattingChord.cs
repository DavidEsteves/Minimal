using System.Windows.Input;

namespace Minimal.Services;

public enum ChordResult { None, Pending, Complete, Cancelled }
public sealed class FormattingChord
{
    private bool pending;
    public ChordResult Process(Key key, ModifierKeys modifiers)
    {
        // Re-pressing Ctrl between strokes is part of a sequential chord.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return ChordResult.None;
        if (pending)
        {
            pending = false;
            return key == Key.D && modifiers == ModifierKeys.Control ? ChordResult.Complete : ChordResult.Cancelled;
        }
        if (key != Key.K || modifiers != ModifierKeys.Control) return ChordResult.None;
        pending = true;
        return ChordResult.Pending;
    }
}
