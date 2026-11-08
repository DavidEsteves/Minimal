using System.Windows;
using System.Windows.Input;
using Minimal.Services;

namespace Minimal.Views;

public partial class SaveChangesDialog : Window
{
    public SaveChangesChoice Choice { get; private set; } = SaveChangesChoice.Cancel;

    private SaveChangesDialog(string name, bool allowRecovery)
    {
        InitializeComponent();
        DocumentName.Text = $"Save changes to {name}?";
        if (allowRecovery)
        {
            Width = 600;
            Explanation.Text = "Save now, or keep your changes for the next session. Don’t save discards these changes.";
            KeepButton.Visibility = Visibility.Visible;
            SaveButton.IsDefault = false; KeepButton.IsDefault = true;
        }
        Loaded += (_, _) => { if (allowRecovery) KeepButton.Focus(); else SaveButton.Focus(); };
    }

    public static SaveChangesChoice Ask(Window owner, string name, bool allowRecovery = false)
    {
        var dialog = new SaveChangesDialog(name, allowRecovery) { Owner = owner, Icon = owner.Icon };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void SaveClick(object sender, RoutedEventArgs e) { Choice = SaveChangesChoice.Save; DialogResult = true; }
    private void DiscardClick(object sender, RoutedEventArgs e) { Choice = SaveChangesChoice.Discard; DialogResult = true; }
    private void KeepClick(object sender, RoutedEventArgs e) { Choice = SaveChangesChoice.KeepForSession; DialogResult = true; }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void DragTitleBar(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
