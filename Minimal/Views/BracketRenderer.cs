using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace Minimal.Views;

public sealed class BracketRenderer : IBackgroundRenderer
{
    public (int First, int Second)? Pair { get; set; }
    public bool Dark { get; set; }
    public KnownLayer Layer => KnownLayer.Background;
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (Pair is not { } pair || !textView.VisualLinesValid) return;
        var brush = new SolidColorBrush(Dark ? Color.FromArgb(90, 80, 150, 215) : Color.FromArgb(65, 15, 108, 189));
        var pen = new Pen(new SolidColorBrush(Dark ? Color.FromRgb(100, 170, 225) : Color.FromRgb(15, 108, 189)), 1);
        foreach (int offset in new[] { pair.First, pair.Second })
        {
            if (offset < 0 || offset >= textView.Document.TextLength) continue;
            var builder = new BackgroundGeometryBuilder { CornerRadius = 2, AlignToWholePixels = true };
            builder.AddSegment(textView, new TextSegment { StartOffset = offset, Length = 1 });
            if (builder.CreateGeometry() is { } geometry) drawingContext.DrawGeometry(brush, pen, geometry);
        }
    }
}
