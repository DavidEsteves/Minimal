# Minimal

A small native Windows text editor: tabs, syntax highlighting, and an offline Markdown preview. Built with C#, WPF, AvalonEdit, Markdig, and WebView2. No accounts, telemetry, extension system, or network services.

## Build and run

Windows 10/11 x64 and a .NET 8 SDK (or a newer SDK that can target .NET 8) are required to build. The app uses the .NET 8 **Desktop Runtime**. Markdown preview additionally needs the [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), normally present on current Windows installations. Ordinary editing works without WebView2.

From the repository directory:

```powershell
dotnet restore Minimal.sln
dotnet build Minimal.sln -c Release
dotnet test Minimal.sln -c Release
dotnet run --project Minimal
# Optionally open one or more files:
dotnet run --project Minimal -- "C:\notes\hello.md" "C:\notes\todo.txt"
```

The executable is `Minimal\bin\Release\net8.0-windows\Minimal.exe` after a Release build. Open `Minimal.sln` in Visual Studio with the .NET desktop development workload if preferred.

## Publish for Windows x64

```powershell
dotnet publish Minimal/Minimal.csproj -p:PublishProfile=WinX64
```

The framework-dependent, single-file executable is written to `artifacts\win-x64\Minimal.exe`. Copy the entire publish directory when distributing; keep its license notices alongside the executable. This build deliberately does not bundle the .NET Desktop or WebView2 runtimes. Native WebView2 loader extraction uses the normal .NET single-file extraction directory. No installer or automatic updater is included.

## Editing

Open multiple files from the File menu, by dropping them on the window, or as command-line arguments. Opening the same path again selects its existing tab. A dot marks unsaved changes. Closing a modified tab asks whether to save, discard, or cancel. With session restoration enabled, exiting also offers **Keep for next time**. **Don't save** discards changes, including their recovery copy. Undoing back to the saved position clears the dot.

The footer shows the active document's character count beside the caret position, updating as you edit and switch tabs. It includes spaces and line breaks and shows source length while previewing Markdown.

| Shortcut | Action |
| --- | --- |
| Ctrl+N / Ctrl+O | New tab / open |
| Ctrl+S / Ctrl+Shift+S | Save / Save As |
| Ctrl+W | Close tab |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous tab |
| Ctrl+F / Ctrl+H | Find / replace |
| Ctrl+G | Go to line |
| Ctrl+Shift+[ / Ctrl+Shift+] | Collapse / expand current region |
| Ctrl+K, then Ctrl+D | Format document (JSON/XML) |
| Enter / Shift+Enter in Find | Next / previous match, wrapping |
| Esc | Close find/replace |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+X / Ctrl+C / Ctrl+V / Ctrl+A | Cut / copy / paste / select all |
| Ctrl+Shift+V | Toggle Markdown source and preview |

Find and replace support literal text, match case, whole words, and regular expressions. The dismissible panel lists all matches with line/column numbers and short previews; clicking a result reveals it even inside a collapsed section. Arrow keys navigate the result list and Enter returns focus to the selected match. Regex replacements support .NET capture substitutions such as `$1`; literal replacements keep dollar signs literal. Searches are debounced, cancellable, and run in the background with regex timeouts and a three-second total budget. A timed-out query displays an error instead of an incomplete match count. Replace All is one undo step.

The compact title bar contains File, Edit, Selection, and View menus. Selection includes Select All and Select Line (Ctrl+L); Edit includes Go to Line and Format Document. View offers Markdown preview, word wrap, line numbers, code folding, and font settings. View → Line numbers toggles the source editor's gutter for all tabs and remembers the choice across restarts; it is off by default. The overflow menu (···) offers Settings and System, Light, and Dark themes. System follows the Windows app theme, including changes while the app is running. Theme and word wrap last for the current run. Drag the unused title-bar area to move the window, double-click it to maximize/restore, or right-click it for the Windows system menu.

The main window uses Windows 11's native rounded corners and themed frame when available, with a square-frame fallback on Windows 10. Windows controls the corner shape when maximized or snapped.

Highlighting covers Markdown, JSON, XML, HTML, CSS, JavaScript, TypeScript, C#, Python, PowerShell, SQL, YAML, and Bash. Click the language name at the bottom right to choose a language for the current tab, force Plain text, or return to Automatic (file extension). Automatic is the default; unknown extensions remain plain text. A manual selection stays with its tab when switching tabs, saving to a new filename, and restoring a session, without modifying the text. Custom lightweight XSHD definitions complement AvalonEdit's built-in definitions; highlighting is lexical, with no language servers.

View → Font and line spacing offers installed Windows font families, a size from 6 to 72 pt, and line spacing from 1× to 3× the font’s normal height. Both the sample and your document update live; Cancel restores the previous appearance, Apply saves the choices, and Reset restores the defaults. These settings apply to source editing in all tabs and persist in `%LOCALAPPDATA%\Minimal\editor-settings.json`. Markdown preview retains its separate reading stylesheet. Font and spacing choices never alter the document text.

The same dialog offers tabs versus spaces and an indentation size of 1–8 columns. Enter preserves existing indentation; C# uses AvalonEdit's native strategy, with lightweight lexical indentation for brace languages, Python/YAML constructs and XML/HTML tags. Other languages retain the preceding line's indentation. Above 200,000 characters the lightweight strategy retains indentation without rescanning the document. Formatting runs only on request, supports strict JSON and XML, preserves raw string/number/attribute/comment tokens, uses the predominant line ending and configured indentation, and is undoable in one step. XML mixed content and `xml:space="preserve"` are retained. Malformed input, XML DTDs, nesting beyond 256 levels, and documents above 5 MiB are refused without changing the buffer. Full C# formatting is intentionally unavailable: no Roslyn dependency has been added.

Adjacent matching `()`, `[]`, and `{}` brackets receive a subtle theme-aware background without changing the buffer or syntax colors. A debounced background lexical pass ignores common string/comment forms, including C# verbatim/raw strings, PowerShell here-strings and nested block comments, and JavaScript templates/regex literals. Interpolation contents are conservatively treated as part of a string. This is lexical assistance, not a complete language parser; unusual or incomplete syntax can omit matches/folds.

View → Code folding persists its setting. AvalonEdit's native gutter controls support nested C#/PowerShell/JavaScript/TypeScript/CSS blocks, JSON objects/arrays, XML/HTML elements, and Markdown ATX heading sections outside fences. Folded sections survive ordinary edits and tab switches where their start positions survive; folding state is independent per tab during a run. Bracket analysis and folding are disabled above 5 MiB. Markdown Setext headings and malformed/implicitly closed HTML elements do not currently create folds.

## Session restoration

**··· → Settings → Restore previous session** is enabled by default. Minimal checkpoints the open tabs, order, active tab, paths, language choices, encodings, caret and source scroll positions under `%LOCALAPPDATA%\Minimal\Sessions`. Changes are checkpointed every five seconds while editing, and immediately after a tab closes or on a successful exit. Choose **Keep for next time** in the exit prompt to retain an unsaved document. Save/Don't save/Cancel retain their usual meanings. Disabling restoration clears inactive recovery checkpoints and restores the original startup/exit behavior.

This is recovery, not auto-save: original document files are never written by the session manager. Saved files normally need only metadata; unsaved/untitled contents are stored as immutable content-addressed blobs, deduplicated within a checkpoint. Manifests use atomic replacement and a previous-generation backup; unused blobs are cleaned up only after a durable update. Process leases protect simultaneously running windows. A crash may lose changes since the last completed checkpoint. Corrupt recovery data is reported and retained. Restored edits retain the original disk fingerprint, so saving a file changed externally requires confirmation. Missing saved files retain a tab and display a notice; unchanged saved content is not duplicated in recovery storage. Undo history, preview mode, and folded sections are not restored across application restarts.

## Files and reliability

- UTF-8 without a BOM is the default. BOM-tagged UTF-8, UTF-16 LE/BE, and UTF-32 LE/BE are detected. Invalid BOM-less UTF-8 falls back to Windows-1252 with a visible notice. File → Reopen with encoding handles ambiguous BOM-less UTF-16 or a mistaken fallback. Reopening modified text explicitly asks before discarding edits.
- File → Save encoding selects UTF-8, UTF-8 BOM, UTF-16 LE/BE, or Windows-1252. Unrepresentable characters fail the save and keep the open document; they are never replaced silently.
- Existing CRLF, LF, CR, and mixed line endings are retained byte-for-byte. AvalonEdit uses surrounding line endings when inserting new lines; an empty/new file uses Windows CRLF. The status bar shows the detected predominant ending.
- Files load and save asynchronously. Saves write a temporary sibling file and replace the destination. Read-only files and permission failures leave the document open; use Save As to write elsewhere.
- On window activation, content hashes detect changed, moved, or deleted files. Reloading edited content requires confirmation. Every save checks the destination hash again before replacement and refuses an intervening change. Changes made while Minimal stays in the foreground are caught on save or the next activation. As with ordinary filesystem saves, the final check and rename are not a distributed lock against other programs writing at the same instant.
- Highlighting is disabled above 5 MiB of text. Opening files larger than 100 MiB requires confirmation. There is no streaming or virtual large-file mode: the text and undo history remain in memory. Preview is limited to 2 MiB; individual code blocks above 200,000 characters use plain formatting.

## Markdown preview

Save a file with `.md`, `.markdown`, or `.mdown` in Automatic language mode, or select Markdown from the bottom-right language picker, to enable preview. Selecting Markdown also works for untitled text. Selecting another language returns to the source. Ctrl+Shift+V replaces the editor completely and renders the latest in-memory content. Each tab remembers its source/preview mode, caret, and source scroll offsets. The shared browser is created only on the first preview request; it may remain alive for reuse until the app closes.

CommonMark plus GFM tables, task lists, strikethrough, and autolinks are parsed by Markdig. Fenced code is highlighted in C# using AvalonEdit, and all CSS is embedded locally. Preview works offline and never modifies the source.

Preview security choices:

- Raw HTML in Markdown is displayed as escaped text. Document scripts, host objects, web messaging, browser developer tools, default context menus, downloads, and browser permission requests are disabled.
- All resource requests are intercepted; remote resources are blocked. Raster images (PNG, JPEG, GIF, WebP, BMP, ICO, AVIF) up to 20 MiB are served only from the document's directory and its descendants. Traversal, symlinks/junctions, SVG, arbitrary file URLs, and unrelated filesystem paths are rejected. Use relative paths such as `images/photo.png`; remote images and data URLs are intentionally unavailable.
- HTTP, HTTPS, and mail links open in the user's default external application. Relative Markdown links inside the allowed directory open in a Minimal tab. Anchor links remain in the preview. Other schemes and arbitrary local-file links are blocked.
- WebView2 initialization or process failures return to the source with an error message and allow another attempt.

## Structure and tests

`Minimal` is the single executable project. `DocumentViewModel.Buffer` is the only editable content buffer for a tab. View models hold tab/document state; services handle file I/O, syntax, themes, Markdown conversion, resource policy, search, lexical structure, formatting, indentation and session checkpoints. Focused `MainWindow.*.cs` partials coordinate these features; `Views/MarkdownPreview.cs` owns the one reusable WebView2 control.

`Minimal.Tests` contains focused xUnit tests for encoding/BOM/newline round trips, Unicode paths, save conflicts, deleted/read-only files, encoding loss, undo/save tracking, tab selection, per-tab preview transitions, Markdown/GFM rendering, syntax availability, HTML escaping, resource restrictions, line validation, keyboard chords, bracket analysis, native folding, large searches and regex failures, indentation, token-preserving formatting, and atomic session/crash recovery. The tests require Windows because the app references WPF. Native browser rendering and keyboard focus also need a UI smoke test.

See [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) for dependency licenses.

**··· → About Minimal → Third-party licenses** displays component versions and full license/notice texts in a selectable, scrollable reader. All notices are embedded in the executable and available offline, including when it is copied without its accompanying files. The published distribution also retains its `Licenses` directory and `THIRD-PARTY-NOTICES.md`. Escape closes the dialog.

The executable and window use the bundled multi-resolution `Minimal/Assets/Minimal.ico`. Its vector source is `Minimal/Assets/MinimalIcon.xaml`, shared with the title-bar logo. After editing that source, regenerate the icon with `powershell.exe -NoProfile -STA -File tools/Generate-Icon.ps1` and rebuild. If the local execution policy blocks unsigned scripts, add `-ExecutionPolicy Bypass` to that command to allow this invocation only.
