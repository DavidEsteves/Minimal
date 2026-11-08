using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using Minimal.Services;

namespace Minimal.Views;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        VersionText.Text = "Version " + LicenseCatalog.ApplicationVersion;
        ComponentPicker.ItemsSource = LicenseCatalog.Load();
        ComponentPicker.SelectedIndex = 0;
        Loaded += (_, _) => LicensesButton.Focus();
    }
    private void LicensesClick(object sender, RoutedEventArgs e)
    {
        var area = GetWorkArea();
        var center = new Point(Left + ActualWidth / 2, Top + ActualHeight / 2);
        // Switch out of content sizing before setting Height; otherwise WPF
        // retains the compact About height and leaves no room for the reader.
        SizeToContent = SizeToContent.Manual;
        Width = Math.Min(760, area.Width - 32);
        Height = Math.Min(660, area.Height - 32);
        AboutView.Visibility = Visibility.Collapsed;
        LicensesView.Visibility = Visibility.Visible;
        Title = Caption.Text = "Third-party licenses — Minimal";
        Left = center.X - Width / 2; Top = center.Y - Height / 2;
        FitToWorkArea(area);
        ComponentPicker.Focus();
    }
    private void BackClick(object sender, RoutedEventArgs e)
    {
        LicensesView.Visibility = Visibility.Collapsed;
        AboutView.Visibility = Visibility.Visible;
        Width = 480; SizeToContent = SizeToContent.Height;
        Title = Caption.Text = "About Minimal";
        LicensesButton.Focus();
    }
    private void ComponentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LicenseText is null || ComponentPicker.SelectedItem is not LicenseNotice notice) return;
        LicenseText.Text = notice.Text;
        LicenseText.ScrollToHome();
    }
    private void FitToWorkArea(Rect area)
    {
        Left = Math.Clamp(Left, area.Left + 8, Math.Max(area.Left + 8, area.Right - Width - 8));
        Top = Math.Clamp(Top, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - Height - 8));
    }
    private Rect GetWorkArea()
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var monitor = MonitorFromWindow(new WindowInteropHelper(this).Handle, 2);
        if (!GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Rect(info.Work.Left / dpi.DpiScaleX, info.Work.Top / dpi.DpiScaleY,
            (info.Work.Right - info.Work.Left) / dpi.DpiScaleX, (info.Work.Bottom - info.Work.Top) / dpi.DpiScaleY);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void DialogKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { e.Handled = true; Close(); } }
    private void DragTitleBar(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
}
