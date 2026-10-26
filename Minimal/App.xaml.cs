using System.Windows;

namespace Minimal;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        await window.OpenPathsAsync(e.Args);
    }
}
