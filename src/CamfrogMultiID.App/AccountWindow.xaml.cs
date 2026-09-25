using System.Globalization;
using System.IO;
using System.Windows;
using CamfrogMultiID.Core;

namespace CamfrogMultiID.App;

public partial class AccountWindow : Window
{
  private readonly CamfrogAccount? _editing;

  public AccountWindow() => InitializeComponent();

  public AccountWindow(CamfrogAccount existing)
      : this()
  {
    ArgumentNullException.ThrowIfNull(existing);
    _editing = existing;
    Title = $"{Strings.TitleEditAccount} —€” {existing.DisplayName}";
    DisplayNameTextBox.Text = existing.DisplayName;
    UsernameTextBox.Text = existing.Username;
    RoomUrlTextBox.Text = existing.RoomUrl;
    EnabledCheckBox.IsChecked = existing.Enabled;
    AutoRestartCheckBox.IsChecked = existing.AutoRestart;
    PasswordLabel.Text = Strings.NewPasswordTitle;
    PasswordHint.Text = Strings.PasswordHintEdit;
    EditHint.Text = $"{existing.ProfileDirectory}";
    EditHint.Visibility = Visibility.Visible;
  }

  private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

  private void Save_Click(object sender, RoutedEventArgs e)
  {
    var username = UsernameTextBox.Text.Trim();
    var password = PasswordInput.Password;
    var displayName = DisplayNameTextBox.Text.Trim();
    var isEdit = _editing is not null;

    if (string.IsNullOrWhiteSpace(displayName))
    {
      MessageBox.Show(Strings.MsgEnterDisplayName, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
      DisplayNameTextBox.Focus();
      return;
    }
    if (displayName.Length > 80)
    {
      MessageBox.Show(Strings.MsgDisplayNameTooLong, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
      DisplayNameTextBox.Focus();
      return;
    }
    if (string.IsNullOrWhiteSpace(username))
    {
      MessageBox.Show(Strings.MsgEnterUsername, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
      UsernameTextBox.Focus();
      return;
    }
    if (username.Length > 80)
    {
      MessageBox.Show(Strings.MsgUsernameTooLong, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
      UsernameTextBox.Focus();
      return;
    }
    if (!isEdit && string.IsNullOrEmpty(password))
    {
      MessageBox.Show(Strings.MsgEnterPassword, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
      PasswordInput.Focus();
      return;
    }

    var enabled = EnabledCheckBox.IsChecked == true;
    var autoRestart = AutoRestartCheckBox.IsChecked == true;
    var roomUrl = RoomUrlTextBox.Text.Trim();
    if (!string.IsNullOrEmpty(roomUrl))
    {
      try
      {
        roomUrl = CamfrogMultiID.Infrastructure.ProcessSessionService.NormalizeRoomUrl(roomUrl);
      }
      catch (Exception ex)
      {
        MessageBox.Show(L10n.Fmt(Strings.MsgInvalidRoomUrl, Environment.NewLine, ex.Message), Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
        RoomUrlTextBox.Focus();
        return;
      }
    }

    try
    {
      if (!isEdit)
      {
        if (App.Db.UsernameExists(username))
        {
          MessageBox.Show(Strings.MsgDuplicateAccount, Strings.TitleDuplicateAccount, MessageBoxButton.OK, MessageBoxImage.Warning);
          UsernameTextBox.Focus();
          return;
        }

        var secret = "account_" + Guid.NewGuid().ToString("N");
        var profile = Path.Combine(App.Paths.Profiles, Guid.NewGuid().ToString("N"));
        var account = new CamfrogAccount
        {
          DisplayName = displayName,
          Username = username,
          PasswordSecretName = secret,
          ProfileDirectory = profile,
          Enabled = enabled,
          RoomUrl = roomUrl,
          AutoRestart = autoRestart
        };

        try
        {
          Directory.CreateDirectory(profile);
          App.Credentials.Save(secret, password);
          account.Id = App.Db.Add(account);
          App.Db.SetPasswordChanged(account.Id, DateTime.UtcNow);
          App.Db.Log("INFO", $"Added account '{account.DisplayName}'.");
          DialogResult = true;
        }
        catch (Exception ex)
        {
          try { File.Delete(Path.Combine(App.Paths.Secrets, secret + ".bin")); } catch { }
          try { Directory.Delete(profile, true); } catch { }
          MessageBox.Show(L10n.Fmt(Strings.MsgSaveAccountFailed, Environment.NewLine, ex.Message), Strings.TitleSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
      else
      {
        var existing = _editing!;
        if (App.Db.UsernameExistsExcept(username, existing.Id))
        {
          MessageBox.Show(Strings.MsgDuplicateAccount, Strings.TitleDuplicateAccount, MessageBoxButton.OK, MessageBoxImage.Warning);
          UsernameTextBox.Focus();
          return;
        }

        if (!string.IsNullOrEmpty(password))
        {
          App.Credentials.Save(existing.PasswordSecretName, password);
          App.Db.SetPasswordChanged(existing.Id, DateTime.UtcNow);
        }

        App.Db.UpdateDetails(existing.Id, displayName, username, enabled);
        App.Db.SetRoomUrl(existing.Id, roomUrl);
        App.Db.SetAutoRestart(existing.Id, autoRestart);
        App.Db.Log("INFO", $"Updated account '{displayName}' (id {existing.Id}).");
        DialogResult = true;
      }
    }
    catch (Exception ex)
    {
      MessageBox.Show(L10n.Fmt(Strings.MsgSaveAccountFailed, Environment.NewLine, ex.Message), Strings.TitleSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }
}

