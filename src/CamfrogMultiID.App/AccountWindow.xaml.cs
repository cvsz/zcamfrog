using System.IO;
using System.Windows;
using CamfrogMultiID.Core;

namespace CamfrogMultiID.App;

public partial class AccountWindow : Window
{
    public AccountWindow() => InitializeComponent();

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;
        var displayName = DisplayNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Please enter a username.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            UsernameTextBox.Focus();
            return;
        }
        if (string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Please enter a password.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            PasswordInput.Focus();
            return;
        }
        if (App.Db.UsernameExists(username))
        {
            MessageBox.Show("An account with this username already exists.", "Duplicate Account", MessageBoxButton.OK, MessageBoxImage.Warning);
            UsernameTextBox.Focus();
            return;
        }

        var secret = "account_" + Guid.NewGuid().ToString("N");
        var profile = Path.Combine(App.Paths.Profiles, Guid.NewGuid().ToString("N"));
        var account = new CamfrogAccount
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            Username = username,
            PasswordSecretName = secret,
            ProfileDirectory = profile,
            Enabled = EnabledCheckBox.IsChecked == true
        };

        try
        {
            Directory.CreateDirectory(profile);
            App.Credentials.Save(secret, password);
            account.Id = App.Db.Add(account);
            App.Db.Log("INFO", $"Added account '{account.DisplayName}'.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            try { File.Delete(Path.Combine(App.Paths.Secrets, secret + ".bin")); } catch { }
            try { Directory.Delete(profile, true); } catch { }
            MessageBox.Show($"Unable to save the account.\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
