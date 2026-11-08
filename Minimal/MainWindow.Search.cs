using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Minimal.Services;

namespace Minimal;

public partial class MainWindow
{
    private readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private CancellationTokenSource? searchCancellation;
    private int searchRevision;
    private int completedSearchRevision = -1;
    private SearchResult searchResult = new([]);
    private bool selectingResult;
    private SearchOptions CurrentSearch => new(FindText.Text, MatchCase.IsChecked == true, WholeWord.IsChecked == true, UseRegex.IsChecked == true);

    private void InitializeSearch()
    {
        searchTimer.Tick += async (_, _) => { searchTimer.Stop(); await UpdateSearchAsync(); };
        Editor.TextChanged += (_, _) => ScheduleSearch();
    }
    private void FindQueryChanged(object sender, TextChangedEventArgs e) => ScheduleSearch();
    private void FindOptionsChanged(object sender, RoutedEventArgs e) => ScheduleSearch();
    private void ScheduleSearch()
    {
        searchRevision++;
        searchCancellation?.Cancel();
        searchTimer.Stop();
        searchResult = new([]);
        if (FindResults is null) return;
        selectingResult = true; FindResults.ItemsSource = null; selectingResult = false;
        if (FindBar.Visibility == Visibility.Visible && displayed is not null) { FindMessage.Text = "Searching…"; searchTimer.Start(); }
    }
    private async Task UpdateSearchAsync()
    {
        if (FindBar.Visibility != Visibility.Visible || displayed is not { } doc) return;
        var options = CurrentSearch;
        var snapshot = doc.Buffer.CreateSnapshot();
        int revision = searchRevision;
        using var cancellation = new CancellationTokenSource();
        searchCancellation?.Cancel();
        searchCancellation = cancellation;
        try
        {
            var result = await Task.Run(() => SearchService.Find(snapshot.Text, options, cancellation.Token));
            if (revision != searchRevision || displayed != doc || cancellation.IsCancellationRequested) return;
            searchResult = result; completedSearchRevision = revision;
            selectingResult = true; FindResults.ItemsSource = result.Hits; selectingResult = false;
            FindMessage.Text = result.Error ?? $"{result.Hits.Count:N0} match{(result.Hits.Count == 1 ? "" : "es")}";
        }
        catch (OperationCanceledException) { }
        finally { if (searchCancellation == cancellation) searchCancellation = null; }
    }
    private async void Find(bool backwards = false)
    {
        if (completedSearchRevision != searchRevision) await UpdateSearchAsync();
        if (completedSearchRevision != searchRevision || searchResult.Hits.Count == 0) return;
        var hits = searchResult.Hits;
        int index = -1;
        if (FindResults.SelectedItem is SearchHit selected && selected.Offset == Editor.SelectionStart && selected.Length == Editor.SelectionLength)
            index = FindResults.SelectedIndex + (backwards ? -1 : 1);
        else if (backwards)
        {
            for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].Offset < Editor.SelectionStart) { index = i; break; }
            if (index < 0) index = hits.Count - 1;
        }
        else
        {
            for (int i = 0; i < hits.Count; i++) if (hits[i].Offset >= Editor.SelectionStart + Editor.SelectionLength) { index = i; break; }
            if (index < 0) index = 0;
        }
        index = (index + hits.Count) % hits.Count;
        selectingResult = true; FindResults.SelectedIndex = index; selectingResult = false;
        FindResults.ScrollIntoView(hits[index]);
        NavigateToHit(hits[index], false);
    }
    private void NavigateToHit(SearchHit hit, bool focus)
    {
        if (completedSearchRevision != searchRevision || hit.Offset + hit.Length > Editor.Document.TextLength) return;
        RevealOffset(hit.Offset); RevealOffset(Math.Min(hit.Offset + hit.Length, Editor.Document.TextLength));
        Editor.Select(hit.Offset, hit.Length);
        Editor.TextArea.Caret.BringCaretToView();
        if (focus) Editor.Focus();
    }
    private void FindResultSelected(object sender, SelectionChangedEventArgs e)
    { if (!selectingResult && FindResults.SelectedItem is SearchHit hit) NavigateToHit(hit, false); }
    private void FindResultsKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter && FindResults.SelectedItem is SearchHit hit) { NavigateToHit(hit, true); e.Handled = true; } }
    private async void ReplaceOneClick(object sender, RoutedEventArgs e) => await ReplaceMatchesAsync(false);
    private async void ReplaceAllClick(object sender, RoutedEventArgs e) => await ReplaceMatchesAsync(true);
    private async Task ReplaceMatchesAsync(bool all)
    {
        if (displayed is not { IsPreview: false } doc || FindText.Text.Length == 0) return;
        if (completedSearchRevision != searchRevision) await UpdateSearchAsync();
        if (completedSearchRevision != searchRevision || searchResult.Error is not null) return;
        if (!all && !searchResult.Hits.Any(x => x.Offset == Editor.SelectionStart && x.Length == Editor.SelectionLength)) { Find(); return; }
        var options = CurrentSearch; string replacement = ReplaceText.Text;
        int? offset = all ? null : Editor.SelectionStart;
        var version = doc.Buffer.Version;
        var snapshot = doc.Buffer.CreateSnapshot();
        try
        {
            string formatted = await Task.Run(() => SearchService.Replace(snapshot.Text, options, replacement, offset));
            if (doc != displayed || doc.Buffer.Version.CompareAge(version) != 0) { ScheduleSearch(); return; }
            int caret = Editor.CaretOffset;
            using (doc.Buffer.RunUpdate()) doc.Buffer.Replace(0, doc.Buffer.TextLength, formatted);
            Editor.CaretOffset = Math.Min(caret, doc.Buffer.TextLength);
            await UpdateSearchAsync();
            if (!all) Find();
        }
        catch (ArgumentException ex) { FindMessage.Text = "Invalid replacement: " + ex.Message; }
        catch (RegexMatchTimeoutException) { FindMessage.Text = "Replacement took too long. Simplify the query."; }
    }
}
