using System.ComponentModel;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Minimal.Services;
using Minimal.Models;
using Minimal.ViewModels;
using Minimal.Views;

namespace Minimal;

public partial class MainWindow : Window
{
    private readonly MainViewModel model = new();
    private readonly FileService files = new();
    private readonly SyntaxService syntax = new();
    private readonly EditorSettingsService preferences = new();
    private EditorSettings editorSettings = new();
    private DocumentViewModel? displayed;
    private MarkdownPreview? preview;
    private bool busy, closing, checkingExternal, dark;
    private string theme = "System";
    private int viewRevision;

    public MainWindow()
    {
        InitializeComponent();
        Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/Minimal.ico", UriKind.Absolute));
        DataContext = model;
        foreach (var language in SyntaxService.Languages)
        {
            var item = new MenuItem { Header = language.Name, Tag = language.Language, IsCheckable = true };
            item.Click += LanguageClick;
            LanguageMenu.Items.Add(item);
        }
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.IndentationSize = 4;
        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatus();
        Editor.TextChanged += (_, _) => UpdateStatus();
        model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(model.Active)) ActivateDocument(); };
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
        dark = ThemeService.Apply(theme);
        ApplyEditorSettings(preferences.Load());
        model.NewDocument();
    }

    private void SystemMenuClick(object sender, RoutedEventArgs e) => SystemCommands.ShowSystemMenu(this, PointToScreen(new Point(0, 36)));
    private void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    private void CloseWindowClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowFrame.Apply(this, dark);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        UpdateWindowInsets();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateWindowInsets();
    }

    private void UpdateWindowInsets()
    {
        if (WindowContent is null) return;
        if (WindowState != WindowState.Maximized) { WindowContent.Margin = new Thickness(0); return; }
        // Maximized HWNDs extend their resize frame beyond the work area. Keep
        // our caption and status bar inside it, including on different-DPI screens.
        var scale = VisualTreeHelper.GetDpi(this);
        uint dpi = (uint)Math.Round(scale.PixelsPerInchX);
        int padding = GetSystemMetricsForDpi(92, dpi); // SM_CXPADDEDBORDER
        double x = (GetSystemMetricsForDpi(32, dpi) + padding) / scale.DpiScaleX;
        double y = (GetSystemMetricsForDpi(33, dpi) + padding) / scale.DpiScaleY;
        WindowContent.Margin = new Thickness(x, y, x, y);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (theme == "System") { dark = ThemeService.Apply(theme); NativeWindowFrame.Apply(this, dark); ApplySyntax(); if (model.Active?.IsPreview == true) _ = RefreshPreviewAsync(); }
    });

    private void RememberPosition()
    {
        if (displayed is null) return;
        displayed.CaretOffset = Editor.CaretOffset;
        displayed.VerticalOffset = Editor.VerticalOffset;
        displayed.HorizontalOffset = Editor.HorizontalOffset;
    }

    private void ActivateDocument()
    {
        RememberPosition();
        if (displayed is not null) displayed.PropertyChanged -= DocumentChanged;
        displayed = model.Active;
        if (displayed is null) { Editor.Document = new(); return; }
        displayed.PropertyChanged += DocumentChanged;
        Editor.Document = displayed.Buffer;
        Editor.CaretOffset = Math.Min(displayed.CaretOffset, displayed.Buffer.TextLength);
        Editor.ScrollToVerticalOffset(displayed.VerticalOffset);
        Editor.ScrollToHorizontalOffset(displayed.HorizontalOffset);
        ApplySyntax();
        UpdateStatus();
        Tabs.ScrollIntoView(displayed);
        _ = RefreshPreviewAsync();
        if (!displayed.IsPreview) Editor.Focus();
    }

    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsPreview)) _ = RefreshPreviewAsync();
        if (e.PropertyName is nameof(DocumentViewModel.Title) or nameof(DocumentViewModel.Encoding)) UpdateStatus();
        if (e.PropertyName is nameof(DocumentViewModel.FilePath) or nameof(DocumentViewModel.IsLarge)) ApplySyntax();
        if (e.PropertyName == nameof(DocumentViewModel.Language)) { ApplySyntax(); UpdateStatus(); }
        if (e.PropertyName == nameof(DocumentViewModel.FilePath) && model.Active?.IsPreview == true) _ = RefreshPreviewAsync();
    }

    private void ApplySyntax()
    {
        Editor.SyntaxHighlighting = displayed?.IsLarge == false ? syntax.ForDocument(displayed.FilePath, displayed.Language, dark) : null;
        Editor.ShowLineNumbers = false;
        if (displayed is not null)
        {
            var name = syntax.LanguageName(displayed.FilePath, displayed.Language);
            LanguageMenu.Header = name + " ▾";
            LanguageMenu.ToolTip = $"Syntax highlighting: {name}" + (displayed.Language == SyntaxLanguage.Automatic ? " (automatic)" : "") + "\nChoose a language for this tab.";
        }
    }

    private void UpdateStatus()
    {
        var doc = model.Active;
        if (doc is null) return;
        Title = $"{doc.Title} — Minimal";
        PreviewMenu.IsEnabled = doc.IsMarkdown;
        SelectionMenu.IsEnabled = !doc.IsPreview;
        PreviewMenu.Header = doc.IsPreview ? "Markdown _source" : "Markdown _preview";
        PositionStatus.Text = doc.IsPreview ? "Markdown preview" : $"Ln {Editor.TextArea.Caret.Line}, Col {Editor.TextArea.Caret.Column}";
        var ending = doc.LineEnding == "\r\n" ? "CRLF" : doc.LineEnding == "\n" ? "LF" : "CR";
        FormatStatus.Text = $"{doc.Encoding.WebName.ToUpperInvariant()}{(doc.Encoding.GetPreamble().Length > 0 ? " BOM" : "")}   ·   {ending}" + (doc.IsLarge ? "   ·   Large file: highlighting off" : "");
    }

    public async Task OpenPathsAsync(IEnumerable<string> paths)
    {
        await RunBusyAsync(async () => { foreach (var path in paths) await OpenOneAsync(path); });
    }

    private async Task OpenOneAsync(string path)
    {
        path = Path.GetFullPath(path);
        if (model.FindPath(path) is { } existing) { model.Active = existing; return; }
        if (new FileInfo(path).Length > 100 * 1024 * 1024 && MessageBox.Show(this, "This file is larger than 100 MB and may take time and memory to open. Continue?", "Large file", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var snapshot = await files.LoadAsync(path);
        var empty = model.Active is { FilePath: null, IsModified: false } active && active.Buffer.TextLength == 0 ? active : null;
        var doc = new DocumentViewModel(Path.GetFileName(path));
        doc.Load(path, snapshot);
        model.Documents.Add(doc);
        model.Active = doc;
        if (empty is not null) model.Remove(empty);
        if (snapshot.UsedFallback) MessageBox.Show(this, "The file is not valid UTF-8. It was opened as Windows-1252. Use File → Reopen with encoding if another encoding is needed.", "File encoding", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task<bool> SaveDocumentAsync(DocumentViewModel doc, bool saveAs = false)
    {
        string? path = doc.FilePath;
        if (saveAs || path is null)
        {
            var dialog = new SaveFileDialog { Title = "Save document", FileName = path is null ? doc.UntitledName + ".txt" : Path.GetFileName(path), InitialDirectory = path is null ? "" : Path.GetDirectoryName(path), Filter = "Text files (*.txt)|*.txt|Markdown (*.md)|*.md|All files (*.*)|*.*", FilterIndex = doc.IsMarkdown ? 2 : 3, AddExtension = true, DefaultExt = doc.IsMarkdown ? ".md" : ".txt", OverwritePrompt = true };
            if (dialog.ShowDialog(this) != true) return false;
            path = Path.GetFullPath(dialog.FileName);
            if (model.FindPath(path) is { } other && other != doc) { MessageBox.Show(this, "This file is already open in another tab. Save to a different name or close that tab first.", "File already open"); return false; }
        }
        var current = await files.FingerprintAsync(path);
        var samePath = string.Equals(path, doc.FilePath, StringComparison.OrdinalIgnoreCase);
        if (samePath && current != doc.Fingerprint)
        {
            var result = MessageBox.Show(this, "The file was changed or removed outside Minimal.\n\nYes: overwrite it with this document.\nNo: save to a different file.\nCancel: keep editing.", "File changed on disk", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel) return false;
            if (result == MessageBoxResult.No) return await SaveDocumentAsync(doc, true);
        }
        var text = doc.Buffer.Text;
        var fingerprint = await files.SaveAsync(path, text, doc.Encoding, current);
        doc.MarkSaved(path, fingerprint);
        ApplySyntax();
        return true;
    }

    private Task<bool> ConfirmCloseAsync(DocumentViewModel doc) => DocumentCloseService.CanCloseAsync(doc,
        document => SaveChangesDialog.Ask(this, document.Title.TrimEnd(' ', '•')),
        document => SaveDocumentAsync(document));

    private async Task CloseDocumentAsync(DocumentViewModel doc)
    {
        if (!await ConfirmCloseAsync(doc)) return;
        model.Remove(doc);
        if (model.Documents.Count == 0) model.NewDocument();
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        // Block input without disabling controls, which invokes light disabled-state
        // templates. Modal dialogs already block the owner while they are open.
        WindowContent.IsHitTestVisible = false;
        try { await action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { MessageBox.Show(this, ex.Message + "\n\nYour open document has been kept.", "Minimal", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { WindowContent.IsHitTestVisible = true; busy = false; if (model.Active?.IsPreview != true) Editor.Focus(); }
    }

    private async Task TogglePreviewAsync()
    {
        var doc = model.Active;
        if (doc?.IsMarkdown != true) return;
        if (!doc.IsPreview) { RememberPosition(); FindBar.Visibility = Visibility.Collapsed; }
        doc.IsPreview = !doc.IsPreview;
        await Task.CompletedTask;
    }

    private async Task RefreshPreviewAsync()
    {
        int revision = ++viewRevision;
        var doc = model.Active;
        var show = doc?.IsPreview == true;
        Editor.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        PreviewHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus();
        if (!show)
        {
            if (doc is not null)
            {
                Editor.CaretOffset = Math.Min(doc.CaretOffset, doc.Buffer.TextLength);
                // The extent changes when switching buffers or restoring the hidden editor.
                // Restore scrolling after layout so it is not clamped to the previous extent.
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    if (revision != viewRevision || doc != model.Active || doc.IsPreview) return;
                    Editor.ScrollToVerticalOffset(doc.VerticalOffset);
                    Editor.ScrollToHorizontalOffset(doc.HorizontalOffset);
                });
                Editor.Focus();
            }
            return;
        }
        if (doc!.Buffer.TextLength > 2 * 1024 * 1024)
        {
            doc.IsPreview = false;
            MessageBox.Show(this, "Markdown preview is limited to 2 MB to keep editing responsive.", "Large Markdown document");
            return;
        }
        try
        {
            if (preview is null)
            {
                preview = new MarkdownPreview();
                preview.OpenMarkdown += path => _ = OpenPathsAsync([path]);
                preview.Failed += message => Dispatcher.BeginInvoke(() =>
                {
                    if (model.Active is { } active) active.IsPreview = false;
                    PreviewHost.Children.Clear(); preview?.Dispose(); preview = null;
                    MessageBox.Show(this, message, "Markdown preview");
                });
                PreviewHost.Children.Add(preview.Control);
            }
            string source = doc.Buffer.Text;
            bool currentDark = dark;
            var html = await Task.Run(() => new MarkdownService().Render(source, currentDark));
            if (revision != viewRevision || model.Active != doc || !doc.IsPreview) return;
            await preview.ShowAsync(html, doc.FilePath, currentDark);
        }
        catch (Exception ex)
        {
            if (revision != viewRevision) return;
            doc.IsPreview = false;
            PreviewHost.Children.Clear(); preview?.Dispose(); preview = null;
            MessageBox.Show(this, "Markdown preview could not start. Install the Microsoft Edge WebView2 Runtime and try again.\n\n" + ex.Message, "Markdown preview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnActivated(object? sender, EventArgs e)
    {
        if (busy || checkingExternal || closing) return;
        checkingExternal = true;
        try
        {
            foreach (var doc in model.Documents.ToArray())
            {
                if (doc.FilePath is null) continue;
                var fingerprint = await files.FingerprintAsync(doc.FilePath);
                var observed = fingerprint ?? "missing";
                if (fingerprint == doc.Fingerprint || observed == doc.ObservedExternalFingerprint) continue;
                doc.ObservedExternalFingerprint = observed;
                if (fingerprint is null)
                {
                    MessageBox.Show(this, $"{doc.FilePath}\n\nThis file was removed or moved. Your document is still open; use Save or Save As to keep it.", "File no longer on disk", MessageBoxButton.OK, MessageBoxImage.Information);
                    continue;
                }
                var message = $"{doc.FilePath}\n\nThis file changed outside Minimal. Reload it?" + (doc.IsModified ? "\n\nReloading discards your unsaved edits. Choose No to keep them." : "");
                if (MessageBox.Show(this, message, "File changed on disk", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await RunBusyAsync(async () =>
                    {
                        RememberPosition();
                        var snapshot = await files.LoadAsync(doc.FilePath);
                        doc.Load(doc.FilePath, snapshot);
                        if (doc == model.Active) { ApplySyntax(); await RefreshPreviewAsync(); }
                    });
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, "Could not check a file for external changes.\n\n" + ex.Message, "Minimal"); }
        finally { checkingExternal = false; }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closing) return;
        e.Cancel = true;
        if (busy) return;
        await RunBusyAsync(async () =>
        {
            foreach (var doc in model.Documents.ToArray()) if (!await ConfirmCloseAsync(doc)) return;
            closing = true;
        });
        if (closing)
        {
            SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
            preview?.Dispose();
            // A discarded or clean document can complete synchronously. Let this
            // Closing event finish before issuing the final Close request.
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (busy) { e.Handled = true; return; }
        var modifiers = Keyboard.Modifiers;
        bool ctrl = (modifiers & ModifierKeys.Control) != 0;
        bool shift = (modifiers & ModifierKeys.Shift) != 0;
        if (!ctrl)
        {
            if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible) { HideFind(); e.Handled = true; }
            return;
        }
        if ((modifiers & ModifierKeys.Alt) != 0) return;
        e.Handled = true;
        switch (e.Key)
        {
            case Key.N: model.NewDocument(); break;
            case Key.O: await OpenDialogAsync(); break;
            case Key.S: if (model.Active is { } save) await RunBusyAsync(async () => { await SaveDocumentAsync(save, shift); }); break;
            case Key.W: if (model.Active is { } close) await RunBusyAsync(() => CloseDocumentAsync(close)); break;
            case Key.Tab: model.NextTab(shift ? -1 : 1); break;
            case Key.F: ShowFind(false); break;
            case Key.H: ShowFind(true); break;
            case Key.V when shift: await TogglePreviewAsync(); break;
            case Key.Z when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false:
                if (shift && Editor.CanRedo) Editor.Redo(); else if (!shift && Editor.CanUndo) Editor.Undo(); break;
            case Key.Y when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false:
                if (Editor.CanRedo) Editor.Redo(); break;
            case Key.A when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false:
                Editor.Focus(); Editor.SelectAll(); break;
            case Key.L when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false: SelectLine(); break;
            case Key.X when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false: Editor.Cut(); break;
            case Key.C when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false: Editor.Copy(); break;
            case Key.V when Keyboard.FocusedElement is not TextBox && model.Active?.IsPreview == false: Editor.Paste(); break;
            default: e.Handled = false; break;
        }
    }

    private async Task OpenDialogAsync()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Text and source files|*.txt;*.md;*.markdown;*.json;*.xml;*.html;*.css;*.js;*.ts;*.cs;*.py;*.ps1;*.sql;*.yaml;*.yml;*.sh|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) await OpenPathsAsync(dialog.FileNames);
    }

    private void ShowFind(bool replace)
    {
        if (model.Active?.IsPreview == true) model.Active.IsPreview = false;
        FindBar.Visibility = Visibility.Visible;
        ReplaceBar.Visibility = replace ? Visibility.Visible : Visibility.Collapsed;
        if (Editor.SelectionLength > 0 && Editor.SelectionLength < 256) FindText.Text = Editor.SelectedText;
        FindText.Focus(); FindText.SelectAll(); FindMessage.Text = "";
    }
    private void HideFind() { FindBar.Visibility = Visibility.Collapsed; Editor.Focus(); }
    private StringComparison FindComparison => MatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    private void Find(bool backwards = false)
    {
        string term = FindText.Text;
        if (term.Length == 0) return;
        var text = Editor.Document.Text;
        int found;
        if (backwards)
        {
            int start = Math.Min(Editor.SelectionStart - 1, text.Length - 1);
            found = start < 0 ? -1 : text.LastIndexOf(term, start, FindComparison);
            if (found < 0) found = text.LastIndexOf(term, FindComparison);
        }
        else
        {
            int start = Math.Min(Editor.SelectionStart + Math.Max(Editor.SelectionLength, 0), text.Length);
            found = text.IndexOf(term, start, FindComparison);
            if (found < 0) found = text.IndexOf(term, FindComparison);
        }
        FindMessage.Text = found < 0 ? "No matches" : "";
        if (found >= 0) { Editor.Select(found, term.Length); Editor.TextArea.Caret.BringCaretToView(); }
    }
    private static Encoding EncodingFor(string tag) => tag switch { "utf8bom" => new UTF8Encoding(true, true), "utf16le" => new UnicodeEncoding(false, true, true), "utf16be" => new UnicodeEncoding(true, true, true), "1252" => FileService.LegacyEncoding, _ => new UTF8Encoding(false, true) };
    private void NewClick(object sender, RoutedEventArgs e) => model.NewDocument();
    private async void OpenClick(object sender, RoutedEventArgs e) => await OpenDialogAsync();
    private async void SaveClick(object sender, RoutedEventArgs e) { if (model.Active is { } doc) await RunBusyAsync(async () => { await SaveDocumentAsync(doc); }); }
    private async void SaveAsClick(object sender, RoutedEventArgs e) { if (model.Active is { } doc) await RunBusyAsync(async () => { await SaveDocumentAsync(doc, true); }); }
    private async void CloseActiveClick(object sender, RoutedEventArgs e) { if (model.Active is { } doc) await RunBusyAsync(() => CloseDocumentAsync(doc)); }
    private async void CloseTabClick(object sender, RoutedEventArgs e) { e.Handled = true; if (((FrameworkElement)sender).DataContext is DocumentViewModel doc) await RunBusyAsync(() => CloseDocumentAsync(doc)); }
    private void ExitClick(object sender, RoutedEventArgs e) => Close();
    private async void PreviewClick(object sender, RoutedEventArgs e) => await TogglePreviewAsync();
    private void WrapClick(object sender, RoutedEventArgs e) => Editor.WordWrap = WrapMenu.IsChecked;
    private async void FontSettingsClick(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var original = editorSettings;
            try
            {
                var chosen = EditorSettingsDialog.Show(this, original, ApplyEditorSettings);
                if (chosen is null) { ApplyEditorSettings(original); return; }
                await preferences.SaveAsync(chosen);
            }
            catch { ApplyEditorSettings(original); throw; }
        });
    }

    private void ApplyEditorSettings(EditorSettings settings)
    {
        var view = Editor.TextArea.TextView;
        double previousHeight = view.DefaultLineHeight;
        double previousOffset = Editor.VerticalOffset;
        editorSettings = settings.Normalize();
        Editor.FontFamily = EditorTypography.CreateFont(editorSettings);
        Editor.FontSize = editorSettings.FontSize;
        var applied = editorSettings;
        var document = model.Active;
        double offset = previousOffset * view.DefaultLineHeight / previousHeight;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (ReferenceEquals(applied, editorSettings) && document == model.Active) Editor.ScrollToVerticalOffset(offset);
        });
    }
    private void ThemeClick(object sender, RoutedEventArgs e) { theme = (string)((MenuItem)sender).Tag; dark = ThemeService.Apply(theme); NativeWindowFrame.Apply(this, dark); ApplySyntax(); if (model.Active?.IsPreview == true) _ = RefreshPreviewAsync(); }
    private void EncodingClick(object sender, RoutedEventArgs e) { if (model.Active is { } doc) { doc.Encoding = EncodingFor((string)((MenuItem)sender).Tag); UpdateStatus(); } }
    private void LanguageMenuOpened(object sender, RoutedEventArgs e)
    {
        foreach (MenuItem item in LanguageMenu.Items) item.IsChecked = model.Active?.Language == (SyntaxLanguage)item.Tag;
        if (LanguageMenu.Template.FindName("PART_Popup", LanguageMenu) is Popup popup)
        {
            popup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
            [
                new(new Point(targetSize.Width - popupSize.Width, -popupSize.Height), PopupPrimaryAxis.Horizontal),
                new(new Point(targetSize.Width - popupSize.Width, targetSize.Height), PopupPrimaryAxis.Horizontal)
            ];
            popup.Placement = PlacementMode.Custom;
        }
    }
    private void LanguageClick(object sender, RoutedEventArgs e)
    {
        if (model.Active is not { } doc) return;
        doc.Language = (SyntaxLanguage)((MenuItem)sender).Tag;
        if (!doc.IsPreview) Editor.Focus();
    }
    private async void ReopenEncodingClick(object sender, RoutedEventArgs e)
    {
        if (model.Active is not { FilePath: not null } doc) return;
        if (doc.IsModified && MessageBox.Show(this, "Reopening discards your unsaved edits. Continue?", "Reopen with encoding", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunBusyAsync(async () => { RememberPosition(); doc.Load(doc.FilePath, await files.LoadAsync(doc.FilePath, EncodingFor((string)((MenuItem)sender).Tag))); await RefreshPreviewAsync(); });
    }
    private void UndoClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false && Editor.CanUndo) Editor.Undo(); }
    private void RedoClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false && Editor.CanRedo) Editor.Redo(); }
    private void CutClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false) Editor.Cut(); }
    private void CopyClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false) Editor.Copy(); }
    private void PasteClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false) Editor.Paste(); }
    private void SelectAllClick(object sender, RoutedEventArgs e) { if (model.Active?.IsPreview == false) { Editor.Focus(); Editor.SelectAll(); } }
    private void SelectLineClick(object sender, RoutedEventArgs e) => SelectLine();
    private void SelectLine()
    {
        if (model.Active?.IsPreview != false) return;
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        Editor.Focus();
        Editor.Select(line.Offset, line.TotalLength);
    }
    private void EditOpened(object sender, RoutedEventArgs e) { UndoItem.IsEnabled = model.Active?.IsPreview == false && Editor.CanUndo; RedoItem.IsEnabled = model.Active?.IsPreview == false && Editor.CanRedo; }
    private void FindClick(object sender, RoutedEventArgs e) => ShowFind(false);
    private void ReplaceClick(object sender, RoutedEventArgs e) => ShowFind(true);
    private void HideFindClick(object sender, RoutedEventArgs e) => HideFind();
    private void FindNextClick(object sender, RoutedEventArgs e) => Find();
    private void FindPreviousClick(object sender, RoutedEventArgs e) => Find(true);
    private void FindKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { Find(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled = true; } }
    private void ReplaceOneClick(object sender, RoutedEventArgs e)
    {
        if (FindText.Text.Length == 0) return;
        if (Editor.SelectedText.Equals(FindText.Text, FindComparison)) Editor.Document.Replace(Editor.SelectionStart, Editor.SelectionLength, ReplaceText.Text);
        Find();
    }
    private void ReplaceAllClick(object sender, RoutedEventArgs e)
    {
        string term = FindText.Text;
        if (term.Length == 0) return;
        var text = Editor.Document.Text;
        var matches = new List<int>();
        for (int offset = 0; offset <= text.Length - term.Length;)
        {
            int index = text.IndexOf(term, offset, FindComparison);
            if (index < 0) break;
            matches.Add(index); offset = index + term.Length;
        }
        using (Editor.Document.RunUpdate()) for (int i = matches.Count - 1; i >= 0; i--) Editor.Document.Replace(matches[i], term.Length, ReplaceText.Text);
        FindMessage.Text = $"{matches.Count} replaced";
    }
    private void OnDragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private async void OnDrop(object sender, DragEventArgs e) { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) { e.Handled = true; await OpenPathsAsync(paths); } }
}
