# Minimal editor enhancements

Implemented: Go to Line, matching bracket highlighting, native code folding, enhanced Find and Replace, automatic indentation with JSON/XML formatting, and persistent session/crash recovery.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+G | Go to Line; Enter navigates, Escape cancels |
| Ctrl+Shift+[ | Collapse the current region |
| Ctrl+Shift+] | Expand the current region |
| Ctrl+K, then Ctrl+D | Format JSON/XML as a sequential keyboard chord |
| Ctrl+F / Ctrl+H | Existing Find / Replace, now with result navigation and search options |

## Validation

Release build and publish succeeded with warnings treated as errors. All 127 tests passed. Native UI checks covered bracket rendering, nested folding and revealing search results inside folds, JSON formatting through the menu and keyboard chord, single-operation undo, themed controls, and recovery of unsaved text and caret position after restarting.

No new package dependencies were added. Published executable: `artifacts/win-x64/Minimal.exe`.

## Behavior and limits

- Restore Previous Session is enabled by default under the overflow menu's Settings submenu. Exit offers Keep for next time; Don't save discards edits. Recovery never saves original document files.
- Checkpoints run every five seconds when needed; a crash can lose edits since the most recent completed checkpoint. Saved tabs use metadata, while dirty and untitled content is deduplicated in recovery blobs. Corrupt data is reported and retained; simultaneous instances use independent leases.
- Formatting supports strict JSON and XML. Full C# formatting remains unavailable because no Roslyn formatter was added. XML DTDs and malformed documents are refused without modifying the document.
- Lexical bracket/folding analysis conservatively ignores interpolation contents. Unusual/incomplete language syntax can omit matches or folds. Markdown folding currently supports ATX headings outside fences; Setext headings and implicitly closed HTML elements are not folded.
- Bracket/folding analysis and formatting are limited to 5 MiB; lightweight indentation falls back to preserving indentation above 200,000 characters. Search is cancellable with regex timeouts and a total time budget.
- Undo history, preview mode and fold state are not restored across restarts. Fold state is kept independently across tabs during a run.

See README.md for architecture, persistence details and usage.

## Files changed

- `Minimal.Tests/DocumentTests.cs`
- `Minimal.Tests/CodeStructureTests.cs`
- `Minimal.Tests/FoldingTests.cs`
- `Minimal.Tests/FormattingTests.cs`
- `Minimal.Tests/NavigationTests.cs`
- `Minimal.Tests/SearchTests.cs`
- `Minimal.Tests/SessionTests.cs`
- `Minimal/App.xaml.cs`
- `Minimal/Assets/SettingsControls.xaml`
- `Minimal/MainWindow.Formatting.cs`
- `Minimal/MainWindow.Navigation.cs`
- `Minimal/MainWindow.Search.cs`
- `Minimal/MainWindow.Session.cs`
- `Minimal/MainWindow.Structure.cs`
- `Minimal/MainWindow.xaml`
- `Minimal/MainWindow.xaml.cs`
- `Minimal/Models/EditorSettings.cs`
- `Minimal/Models/SessionState.cs`
- `Minimal/Services/CodeStructureService.cs`
- `Minimal/Services/DocumentCloseService.cs`
- `Minimal/Services/EditorIndentationStrategy.cs`
- `Minimal/Services/FormattingChord.cs`
- `Minimal/Services/FormattingService.cs`
- `Minimal/Services/SearchService.cs`
- `Minimal/Services/SessionManager.cs`
- `Minimal/ViewModels/DocumentViewModel.cs`
- `Minimal/ViewModels/MainViewModel.cs`
- `Minimal/Views/BracketRenderer.cs`
- `Minimal/Views/EditorSettingsDialog.xaml`
- `Minimal/Views/EditorSettingsDialog.xaml.cs`
- `Minimal/Views/GoToLineDialog.xaml`
- `Minimal/Views/GoToLineDialog.xaml.cs`
- `Minimal/Views/SaveChangesDialog.xaml`
- `Minimal/Views/SaveChangesDialog.xaml.cs`
- `README.md`
- `IMPLEMENTATION.md` (this report)
