using System.Windows;
using System.Windows.Input;
using Minimal.Services;

namespace Minimal.Views;

public partial class SaveChangesDialog : Window
{
    public SaveChangesChoice Choice { get; private set; } = SaveChangesChoice.Cancel;

    private SaveChangesDialog(string name)
    {
        InitializeComponent();
        DocumentName.Text = $"Save changes to {name}?";
        Loaded += (_, _) => SaveButton.Focus();
    }

    public static SaveChangesChoice Ask(Window owner, string name)
    {
        var dialog = new SaveChangesDialog(name) { Owner = owner, Icon = owner.Icon };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void SaveClick(object sender, RoutedEventArgs e) { Choice = SaveChangesChoice.Save; DialogResult = true; }
    private void DiscardClick(object sender, RoutedEventArgs e) { Choice = SaveChangesChoice.Discard; DialogResult = true; }
    private void CancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void DragTitleBar(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
