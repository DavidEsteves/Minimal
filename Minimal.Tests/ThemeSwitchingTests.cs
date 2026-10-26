using System.Runtime.ExceptionServices;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Minimal.Services;
using Xunit;

namespace Minimal.Tests;

[CollectionDefinition("Application theme", DisableParallelization = true)]
public sealed class ApplicationThemeCollection;

[Collection("Application theme")]
public sealed class ThemeSwitchingTests
{
    [Fact]
    public void ExistingScrollbarsUpdateTheirResolvedColorsOnEveryThemeChange()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Window? host = null;
            try
            {
                // Load the production resources without running Minimal.App.OnStartup.
                using var stream = typeof(ThemeSwitchingTests).Assembly.GetManifestResourceStream("Minimal.Tests.AppResources.xaml")!;
                var xml = XDocument.Load(stream);
                XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                var resources = xml.Root!.Element(presentation + "Application.Resources")!.Elements().Single();
                resources.SetAttributeValue(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml");
                foreach (var source in resources.Descendants().Attributes("Source")) source.Value = "/Minimal;component/" + source.Value;
                app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString());
                var vertical = CreateScrollbar(app, Orientation.Vertical);
                var horizontal = CreateScrollbar(app, Orientation.Horizontal);
                var corner = (Border)XamlReader.Parse("""
                    <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            Background="{DynamicResource {x:Static SystemColors.ControlBrushKey}}">
                      <Border.Resources><SolidColorBrush x:Key="{x:Static SystemColors.ControlBrushKey}" Color="{DynamicResource ScrollTrackColor}"/></Border.Resources>
                    </Border>
                    """);
                var panel = new StackPanel();
                panel.Children.Add(vertical); panel.Children.Add(horizontal); panel.Children.Add(corner);
                host = new Window { Content = panel, Width = 400, Height = 400, Left = -32000, Top = -32000, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
                host.Show();
                foreach (var theme in new[] { "Dark", "Light", "Dark", "Light" })
                {
                    ThemeService.Apply(theme);
                    app.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var trackColor = theme == "Dark" ? Color.FromRgb(32, 32, 32) : Colors.White;
                    var thumbColor = theme == "Dark" ? Color.FromRgb(85, 85, 85) : Color.FromRgb(194, 194, 194);
                    foreach (var bar in new[] { vertical, horizontal })
                    {
                        Assert.Equal(trackColor, ((SolidColorBrush)bar.Background).Color);
                        Assert.Equal(trackColor, ((SolidColorBrush)((Grid)VisualTreeHelper.GetChild(bar, 0)).Background).Color);
                        var track = (Track)bar.Template.FindName("PART_Track", bar);
                        track.Thumb.ApplyTemplate();
                        var grip = (Border)track.Thumb.Template.FindName("Grip", track.Thumb);
                        Assert.Equal(thumbColor, ((SolidColorBrush)grip.Background).Color);
                    }
                    Assert.Equal(trackColor, ((SolidColorBrush)corner.Background).Color);
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { host?.Close(); app.Shutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Theme switching verification timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static ScrollBar CreateScrollbar(Application app, Orientation orientation)
    {
        var bar = new ScrollBar
        {
            Style = (Style)app.FindResource(typeof(ScrollBar)), Orientation = orientation,
            Maximum = 100, ViewportSize = 20, Value = 30
        };
        bar.ApplyTemplate();
        var size = orientation == Orientation.Vertical ? new Size(18, 300) : new Size(300, 18);
        bar.Measure(size);
        bar.Arrange(new Rect(size));
        return bar;
    }

}
