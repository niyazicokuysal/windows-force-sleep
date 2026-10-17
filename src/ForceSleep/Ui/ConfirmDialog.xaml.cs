using System.Windows;

namespace ForceSleep.Ui;

/// <summary>A Yes/Cancel question in the app's own style (the system MessageBox ignores the theme).</summary>
internal partial class ConfirmDialog : Window
{
    private ConfirmDialog(string message)
    {
        InitializeComponent();
        WindowStyling.RoundCorners(this);
        Message.Text = message;
    }

    public static bool Ask(Window? owner, string message)
    {
        var dialog = new ConfirmDialog(message) { Owner = owner };
        if (owner is null)
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dialog.ShowDialog() == true;
    }

    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;
}
