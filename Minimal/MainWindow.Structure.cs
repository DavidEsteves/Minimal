using System.Windows.Threading;
using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;
using Minimal.Services;
using Minimal.Views;
using Minimal.ViewModels;
using Minimal.Models;

namespace Minimal;

public partial class MainWindow
{
    private readonly BracketRenderer brackets = new();
    private readonly DispatcherTimer structureTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? structureCancellation;
    private CodeStructure structure = CodeStructure.Empty;
    private int structureRevision;
    private FoldingManager? foldingManager;
    private readonly Dictionary<DocumentViewModel, List<TextAnchor>> foldedTabs = new();
    private bool restoreFoldedState;

    private void DetachFolding()
    {
        if (foldingManager is null) return;
        if (displayed is not null)
            foldedTabs[displayed] = foldingManager.AllFoldings.Where(x => x.IsFolded).Select(x =>
            {
                var anchor = displayed.Buffer.CreateAnchor(x.StartOffset);
                anchor.MovementType = AnchorMovementType.AfterInsertion;
                return anchor;
            }).ToList();
        FoldingManager.Uninstall(foldingManager);
        foldingManager = null;
    }
    private void ConfigureFolding()
    {
        bool supported = displayed is { IsLarge: false } doc && CodeStructureService.Resolve(doc.FilePath, doc.Language) is
            SyntaxLanguage.CSharp or SyntaxLanguage.PowerShell or SyntaxLanguage.JavaScript or SyntaxLanguage.TypeScript or SyntaxLanguage.Json or SyntaxLanguage.Xml or SyntaxLanguage.Html or SyntaxLanguage.Markdown or SyntaxLanguage.Css;
        if (!editorSettings.CodeFolding || !supported) { DetachFolding(); return; }
        if (foldingManager is not null) return;
        foldingManager = FoldingManager.Install(Editor.TextArea);
        foreach (var margin in Editor.TextArea.LeftMargins.OfType<FoldingMargin>())
        {
            margin.SetResourceReference(FoldingMargin.FoldingMarkerBrushProperty, "CaptionInk");
            margin.SetResourceReference(FoldingMargin.FoldingMarkerBackgroundBrushProperty, "Surface");
            margin.SetResourceReference(FoldingMargin.SelectedFoldingMarkerBrushProperty, "Ink");
            margin.SetResourceReference(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, "CaptionHover");
        }
        restoreFoldedState = true;
    }
    private void UpdateFolding()
    {
        if (foldingManager is null || displayed is null) return;
        foldingManager.UpdateFoldings(structure.Regions.Select(x => new NewFolding(x.Start, x.End) { Name = x.Name }), -1);
        if (restoreFoldedState && foldedTabs.TryGetValue(displayed, out var saved))
        {
            var starts = saved.Where(x => !x.IsDeleted).Select(x => x.Offset).ToHashSet();
            foreach (var fold in foldingManager.AllFoldings) fold.IsFolded = starts.Contains(fold.StartOffset);
        }
        restoreFoldedState = false;
    }
    private void FoldCurrent(bool collapse)
    {
        if (foldingManager is null || model.Active?.IsPreview != false) return;
        int offset = Editor.CaretOffset;
        var region = foldingManager.AllFoldings.Where(x => x.StartOffset <= offset && x.EndOffset >= offset && (collapse ? !x.IsFolded : x.IsFolded))
            .OrderBy(x => x.Length).FirstOrDefault();
        // A caret anywhere on the opening line can control that line's fold.
        region ??= foldingManager.AllFoldings.FirstOrDefault(x => Editor.Document.GetLineByOffset(x.StartOffset).LineNumber == Editor.TextArea.Caret.Line && x.IsFolded != collapse);
        if (region is not null) region.IsFolded = collapse;
    }
    private async void FoldingClick(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var settings = editorSettings with { CodeFolding = FoldingMenu.IsChecked };
            await preferences.SaveAsync(settings);
            ApplyEditorSettings(settings);
            ConfigureFolding(); ScheduleStructure();
        });
    }

    private void InitializeStructure()
    {
        Editor.TextArea.TextView.BackgroundRenderers.Add(brackets);
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateBracketPair();
        Editor.TextChanged += (_, _) => ScheduleStructure();
        structureTimer.Tick += async (_, _) => { structureTimer.Stop(); await AnalyzeStructureAsync(); };
    }
    private void ScheduleStructure()
    {
        structureRevision++;
        structureCancellation?.Cancel();
        structure = CodeStructure.Empty;
        UpdateBracketPair();
        structureTimer.Stop();
        if (displayed is { IsLarge: false }) structureTimer.Start();
    }
    private async Task AnalyzeStructureAsync()
    {
        if (displayed is not { IsLarge: false } doc) return;
        int revision = structureRevision;
        var snapshot = doc.Buffer.CreateSnapshot();
        var language = CodeStructureService.Resolve(doc.FilePath, doc.Language);
        structureCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        structureCancellation = cancellation;
        try
        {
            var analyzed = await Task.Run(() => CodeStructureService.Analyze(snapshot.Text, language, cancellation.Token));
            if (revision != structureRevision || displayed != doc) return;
            structure = analyzed;
            UpdateBracketPair();
            UpdateFolding();
        }
        catch (OperationCanceledException) { }
        finally { if (structureCancellation == cancellation) structureCancellation = null; }
    }
    private void UpdateBracketPair()
    {
        brackets.Dark = dark;
        brackets.Pair = displayed?.IsPreview == false ? structure.PairAt(Editor.CaretOffset) : null;
        Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
    }
}
