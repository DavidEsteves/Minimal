using System.IO;
using System.Reflection;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Minimal.Services;

public sealed class MarkdownService
{
    private readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseTaskLists().UseAutoLinks().UseAutoIdentifiers(Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions.GitHub)
        .UseEmphasisExtras(Markdig.Extensions.EmphasisExtras.EmphasisExtraOptions.Strikethrough)
        .DisableHtml().Build();
    private static readonly Lazy<string> Css = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Minimal.Assets.markdown.css")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public string Render(string source, bool dark)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        pipeline.Setup(renderer);
        var original = renderer.ObjectRenderers.FindExact<CodeBlockRenderer>();
        if (original is not null) renderer.ObjectRenderers.Remove(original);
        renderer.ObjectRenderers.Insert(0, new HighlightedCodeRenderer(new SyntaxService(), dark));
        renderer.Render(Markdown.Parse(source, pipeline));
        return "<!doctype html><html lang=\"en\" class=\"" + (dark ? "dark" : "light") + "\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
            "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src https://minimal.invalid; style-src 'unsafe-inline'; script-src 'none'; base-uri 'none'; form-action 'none'; frame-src 'none'\">" +
            "<style>" + Css.Value + "</style><title>Markdown preview</title></head><body class=\"" + (dark ? "dark" : "light") + "\"><main>" + writer + "</main></body></html>";
    }

    private sealed class HighlightedCodeRenderer(SyntaxService syntax, bool dark) : HtmlObjectRenderer<CodeBlock>
    {
        protected override void Write(HtmlRenderer renderer, CodeBlock block)
        {
            var language = block is FencedCodeBlock fenced ? (fenced.Info ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";
            renderer.EnsureLine();
            renderer.Write("<pre><code>");
            renderer.Write(syntax.CodeToHtml(block.Lines.ToString(), language, dark));
            renderer.WriteLine("</code></pre>");
        }
    }
}
