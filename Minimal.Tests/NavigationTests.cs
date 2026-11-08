using Minimal.Views;
using Minimal.Services;
using System.Windows.Input;
using Xunit;

namespace Minimal.Tests;

public sealed class NavigationTests
{
    [Theory]
    [InlineData("1", 1, true)]
    [InlineData("42", 42, true)]
    [InlineData("43", 42, false)]
    [InlineData("0", 10, false)]
    [InlineData("-1", 10, false)]
    [InlineData("1.5", 10, false)]
    [InlineData("hello", 10, false)]
    [InlineData("999999999999", 10, false)]
    public void ValidatesLineNumbers(string text, int count, bool valid) =>
        Assert.Equal(valid, GoToLineDialog.TryValidate(text, count, out _));
    [Fact]
    public void FormattingChordAcceptsRepressedModifiersAndConsumesOnlyTheRightSecondKey()
    {
        var chord = new FormattingChord();
        Assert.Equal(ChordResult.None, chord.Process(Key.LeftCtrl, ModifierKeys.Control));
        Assert.Equal(ChordResult.Pending, chord.Process(Key.K, ModifierKeys.Control));
        Assert.Equal(ChordResult.None, chord.Process(Key.LeftCtrl, ModifierKeys.Control));
        Assert.Equal(ChordResult.Complete, chord.Process(Key.D, ModifierKeys.Control));
        Assert.Equal(ChordResult.None, chord.Process(Key.D, ModifierKeys.Control));
        Assert.Equal(ChordResult.Pending, chord.Process(Key.K, ModifierKeys.Control));
        Assert.Equal(ChordResult.Cancelled, chord.Process(Key.Escape, ModifierKeys.None));
        Assert.Equal(ChordResult.None, chord.Process(Key.D, ModifierKeys.Control));
        Assert.Equal(ChordResult.Pending, chord.Process(Key.K, ModifierKeys.Control));
        Assert.Equal(ChordResult.Cancelled, chord.Process(Key.D, ModifierKeys.None));
    }
}
