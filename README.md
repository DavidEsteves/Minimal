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

Open multiple files from the File menu, by dropping them on the window, or as command-line arguments. Opening the same path again selects its existing tab. A dot marks unsaved changes. Closing a modified tab or exiting asks whether to save, discard, or cancel. Undoing back to the saved position clears the dot.

| Shortcut | Action |
| --- | --- |
| Ctrl+N / Ctrl+O | New tab / open |
| Ctrl+S / Ctrl+Shift+S | Save / Save As |
| Ctrl+W | Close tab |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous tab |
| Ctrl+F / Ctrl+H | Find / replace |
| Enter / Shift+Enter in Find | Next / previous match, wrapping |
| Esc | Close find/replace |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+X / Ctrl+C / Ctrl+V / Ctrl+A | Cut / copy / paste / select all |
| Ctrl+Shift+V | Toggle Markdown source and preview |

Find and replace use literal text and optional case matching. Replace All is one undo step. The compact title bar contains File, Edit, Selection, and View menus. Selection includes Select All and Select Line (Ctrl+L); View offers Markdown preview and word wrap. The overflow menu (···) offers System, Light, and Dark themes. System follows the Windows app theme, including changes while the app is running. Theme and word wrap are session preferences. Drag the unused title-bar area to move the window, double-click it to maximize/restore, or right-click it for the Windows system menu.

The main window uses Windows 11's native rounded corners and themed frame when available, with a square-frame fallback on Windows 10. Windows controls the corner shape when maximized or snapped.

Highlighting covers Markdown, JSON, XML, HTML, CSS, JavaScript, TypeScript, C#, Python, PowerShell, SQL, YAML, and Bash. Click the language name at the bottom right to choose a language for the current tab, force Plain text, or return to Automatic (file extension). Automatic is the default; unknown extensions remain plain text. A manual selection stays with its tab when switching tabs or saving to a new filename, and does not modify the text or mark it as unsaved. Language choices are session preferences. Custom lightweight XSHD definitions complement AvalonEdit's built-in definitions; highlighting is lexical, with no language servers or code analysis.

View → Font and line spacing offers installed Windows font families, a size from 6 to 72 pt, and line spacing from 1× to 3× the font’s normal height. Both the sample and your document update live; Cancel restores the previous appearance, Apply saves the choices, and Reset restores the defaults. These settings apply to source editing in all tabs and persist in `%LOCALAPPDATA%\Minimal\editor-settings.json`. Markdown preview retains its separate reading stylesheet. Font and spacing choices never alter the document text.

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

`Minimal` is the single executable project. `DocumentViewModel.Buffer` is the only editable content buffer for a tab. View models hold tab/document state; services handle file I/O, syntax, themes, Markdown conversion, and resource policy. The main window contains UI coordination and dialogs; `Views/MarkdownPreview.cs` owns the one reusable WebView2 control.

`Minimal.Tests` contains focused xUnit tests for encoding/BOM/newline round trips, Unicode paths, save conflicts, deleted/read-only files, encoding loss, undo/save tracking, tab selection, per-tab preview transitions, Markdown/GFM rendering, syntax availability, HTML escaping, and resource restrictions. The tests require Windows because the app references WPF. Native browser rendering and keyboard focus also need a manual UI smoke test.

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency licenses.

The executable and window use the bundled multi-resolution `Minimal/Assets/Minimal.ico`. Its vector source is `Minimal/Assets/MinimalIcon.xaml`, shared with the title-bar logo. After editing that source, regenerate the icon with `powershell.exe -NoProfile -STA -File tools/Generate-Icon.ps1` and rebuild. If the local execution policy blocks unsigned scripts, add `-ExecutionPolicy Bypass` to that command to allow this invocation only.
