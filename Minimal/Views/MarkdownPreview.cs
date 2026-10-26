using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Minimal.Services;

namespace Minimal.Views;

public sealed class MarkdownPreview : IDisposable
{
    private readonly WebView2 browser = new();
    private Task? initialization;
    private string? root;
    private string pageUrl = "";
    private byte[] html = [];
    public FrameworkElement Control => browser;
    public event Action<string>? OpenMarkdown;
    public event Action<string>? Failed;

    public async Task ShowAsync(string content, string? filePath, bool dark)
    {
        initialization ??= InitializeAsync();
        await initialization;
        root = filePath is null ? null : Path.GetDirectoryName(filePath);
        html = Encoding.UTF8.GetBytes(content);
        // Root path keeps relative Markdown images relative to the document directory.
        pageUrl = $"https://{LocalResourcePolicy.Host}/?preview={Guid.NewGuid():N}";
        browser.DefaultBackgroundColor = dark ? System.Drawing.Color.FromArgb(32, 32, 32) : System.Drawing.Color.White;
        browser.CoreWebView2.Navigate(pageUrl);
    }

    private async Task InitializeAsync()
    {
        var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Minimal", "WebView2"));
        await browser.EnsureCoreWebView2Async(environment);
        var core = browser.CoreWebView2;
        var settings = core.Settings;
        settings.IsScriptEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnResource;
        core.NavigationStarting += (_, e) =>
        {
            if (e.Uri == pageUrl || e.Uri.StartsWith(pageUrl + "#", StringComparison.Ordinal)) return;
            e.Cancel = true;
            OpenLink(e.Uri);
        };
        core.NewWindowRequested += (_, e) => { e.Handled = true; OpenLink(e.Uri); };
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.ProcessFailed += (_, _) => Failed?.Invoke("The Markdown preview process stopped. Return to the source and try again.");
        core.NavigationCompleted += (_, e) => { if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Failed?.Invoke("Markdown preview could not be displayed: " + e.WebErrorStatus); };
    }

    private async void OnResource(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        using var deferral = e.GetDeferral();
        Stream? content = null;
        string type = "text/plain";
        try
        {
            if (e.Request.Method == "GET" && e.Request.Uri == pageUrl && e.ResourceContext == CoreWebView2WebResourceContext.Document)
            { content = new MemoryStream(html, false); type = "text/html; charset=utf-8"; }
            else if (e.Request.Method == "GET" && e.ResourceContext == CoreWebView2WebResourceContext.Image && Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
            {
                var path = LocalResourcePolicy.Resolve(root, uri);
                if (path is not null && File.Exists(path) && new FileInfo(path).Length <= 20 * 1024 * 1024)
                { content = new MemoryStream(await File.ReadAllBytesAsync(path), false); type = LocalResourcePolicy.MimeType(path); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { content?.Dispose(); content = null; }
        e.Response = browser.CoreWebView2.Environment.CreateWebResourceResponse(content ?? new MemoryStream(), content is null ? 403 : 200, content is null ? "Forbidden" : "OK", $"Content-Type: {type}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff");
    }

    private void OpenLink(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is "http" or "https" && uri.Host != LocalResourcePolicy.Host || uri.Scheme == "mailto")
        {
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { Failed?.Invoke("Could not open the link: " + ex.Message); }
        }
        else
        {
            var path = LocalResourcePolicy.Resolve(root, uri, false);
            if (path is not null && Path.GetExtension(path).ToLowerInvariant() is ".md" or ".markdown" or ".mdown") OpenMarkdown?.Invoke(path);
        }
    }
    public void Dispose() => browser.Dispose();
}
