<div align="center">
  <img src="docs/minimal-icon.png" alt="Minimal" width="80" />
  <h1>Minimal</h1>
  <p>A little room for your words, notes, and code.</p>
  <p>A lightweight native Windows text editor with tabs, syntax highlighting, and offline Markdown preview.</p>
  <p>
    <a href="https://github.com/DavidEsteves/Minimal/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/DavidEsteves/Minimal?color=27a9e1" /></a>
    <a href="https://github.com/DavidEsteves/Minimal/actions/workflows/build.yml"><img alt="Build and test" src="https://github.com/DavidEsteves/Minimal/actions/workflows/build.yml/badge.svg" /></a>
    <a href="LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-27a9e1" /></a>
    <img alt="Platform: Windows 10 / 11" src="https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-444" />
  </p>
  <p><a href="https://github.com/DavidEsteves/Minimal/releases/latest"><strong>Download for Windows x64</strong></a> · <a href="docs/USER-GUIDE.md">User guide</a> · <a href="CHANGELOG.md">What's new</a></p>
</div>

## Made for getting things down

Minimal keeps everyday editing close at hand: open a few files, find what you need, and carry on. There are no accounts, telemetry, extensions, or network services. Your documents stay on your computer.

| A focused workspace | Useful tools, ready when needed |
| --- | --- |
| **Tabs that remember** | Restore open files and recover unsaved notes after a restart. |
| **Readable code** | Highlighting for 13 languages, matching brackets, code folding, and automatic indentation. |
| **Fast navigation** | Find and replace, regex, whole words, clickable results, and Go to Line. |
| **Markdown, offline** | Switch between source and a reading view with tables, task lists, and highlighted code. |
| **Careful file handling** | Encoding choices, preserved line endings, atomic saves, and external-change checks. |
| **Your preferred view** | System, Light, and Dark themes; adjustable font, spacing, indentation, and line numbers. |

## Download and run

1. Download `Minimal-1.0.0-win-x64.zip` from the [latest release](https://github.com/DavidEsteves/Minimal/releases/latest).
2. Extract the entire ZIP to a folder of your choice.
3. Run `Minimal.exe`. Keep the accompanying license files in the same folder.

**Requirements:** Windows 10 or 11, x64, and the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (choose **Desktop Runtime → Windows → x64**). Markdown preview also needs the [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/), normally already installed on Windows. Ordinary text editing works without WebView2.

This is a portable, framework-dependent release with no installer or automatic updater. The executable is currently unsigned; Windows may show a reputation warning when it is downloaded. A `SHA256SUMS.txt` file accompanies the release so you can verify the ZIP:

```powershell
Get-FileHash .\Minimal-1.0.0-win-x64.zip -Algorithm SHA256
```

## A few shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+N / Ctrl+O | New tab / open files |
| Ctrl+S / Ctrl+Shift+S | Save / Save As |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous tab |
| Ctrl+F / Ctrl+H | Find / replace |
| Ctrl+G | Go to line |
| Ctrl+K, then Ctrl+D | Format JSON or XML |
| Ctrl+Shift+[ / Ctrl+Shift+] | Collapse / expand a region |
| Ctrl+Shift+V | Toggle Markdown source / preview |

Drop files onto the window to open them. Click the language name in the status bar to change highlighting. Use **··· → Settings** for session restoration and **View → Font and line spacing** to make the editor your own.

## Build from source

Install a .NET 8 SDK, or a newer SDK that can target .NET 8, on Windows. Visual Studio with the .NET desktop development workload is optional.

```powershell
git clone https://github.com/DavidEsteves/Minimal.git
cd Minimal
dotnet restore Minimal.sln
dotnet test Minimal.sln -c Release
dotnet run --project Minimal
```

To create the same portable ZIP used for releases:

```powershell
pwsh -File tools/Package-Release.ps1
```

The ZIP and checksum are written to `artifacts/release/`. The script takes its version from `Minimal/Minimal.csproj`, publishes into a fresh staging directory, and includes the license notices. GitHub Actions builds, tests, and packages the app on Windows.

## Under the surface

Built with **C# · .NET 8 · WPF · AvalonEdit · Markdig · WebView2**.

- `Minimal/` — the app, document state, file services, and native UI.
- `Minimal.Tests/` — xUnit tests for editing, file integrity, Markdown, search, formatting, folding, and recovery.
- `docs/USER-GUIDE.md` — full behavior, shortcuts, architecture, and limits.
- `tools/` — icon generation and release packaging.

Markdown preview blocks remote resources and raw HTML; supported local images stay within the document's directory. Session recovery checkpoints local copies of unsaved text and never writes your original files. Recovery can lose edits since the last completed checkpoint. Highlighting and structural analysis have size limits; Minimal is an in-memory editor. See the [user guide](docs/USER-GUIDE.md) for the details.

## Feedback and contributions

Found a bug or have a focused improvement in mind? [Open an issue](https://github.com/DavidEsteves/Minimal/issues) with the steps to reproduce it and your Windows version. Pull requests are welcome; run the Release tests before submitting.

## License

Minimal is released under the [MIT License](LICENSE). Bundled dependencies retain their own licenses; see [Third-party notices](THIRD-PARTY-NOTICES.md). Their full license texts are also available offline in **··· → About Minimal → Third-party licenses**.
