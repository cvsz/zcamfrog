using System.Windows;

namespace CamfrogMultiID.App;

public partial class ChangePasswordWindow : Window
{
    public string NewPassword { get; private set; } = string.Empty;

    public ChangePasswordWindow(string displayName)
    {
        InitializeComponent();
        AccountLabel.Text = $"Account: {displayName}";
        NewPasswordBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(NewPasswordBox.Password))
        {
            MessageBox.Show("Please enter a new password.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            NewPasswordBox.Focus();
            return;
        }
        NewPassword = NewPasswordBox.Password;
        DialogResult = true;
    }
}
