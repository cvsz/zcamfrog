using System.IO;
using System.Windows;
using System.Windows.Threading;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => RefreshRuntimeState();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRuntimeState();
        _timer.Start();
    }

    private void RefreshRuntimeState()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            foreach (var account in App.Db.GetAccounts())
            {
                if (account.ProcessId is not int || !account.Status.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ProcessSessionService.IsTrackedProcessAlive(account, out var reason))
                {
                    if (IsDefinitivelyStopped(reason))
                    {
                        App.Db.UpdateRuntime(account.Id, "Stopped", null, null, reason ?? "Process exited.");
                        App.Db.Log("INFO", $"{account.DisplayName}: {reason ?? "Process exited."}");
                    }
                    else
                    {
                        App.Db.UpdateRuntime(
                            account.Id,
                            "Error",
                            account.ProcessId,
                            account.StartedAtUtc,
                            reason ?? "Unable to verify process identity.",
                            account.ProcessExecutablePath);
                        App.Db.Log("WARN", $"{account.DisplayName}: {reason ?? "Unable to verify process identity."}");
                    }
                }
            }

            AccountsGrid.ItemsSource = App.Db.GetAccounts();

            try
            {
                var logFile = Path.Combine(App.Paths.Logs, "app.log");
                LogBox.Text = File.Exists(logFile) ? File.ReadAllText(logFile) : string.Empty;
                LogBox.ScrollToEnd();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static bool IsDefinitivelyStopped(string? reason) =>
        reason is not null &&
        (reason.Equals("Process exited.", StringComparison.Ordinal) ||
         reason.Equals("Process no longer exists.", StringComparison.Ordinal) ||
         reason.Equals("PID was reused by a different process.", StringComparison.Ordinal) ||
         reason.Equals("Process executable path does not match the tracked executable.", StringComparison.Ordinal));

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (new AccountWindow { Owner = this }.ShowDialog() == true)
            RefreshRuntimeState();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        RefreshRuntimeState();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRuntimeState();

    private void StartSelected_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is CamfrogAccount account)
            StartAccount(account);
        else
            MessageBox.Show("Please select an account first.", "Start Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StopSelected_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is CamfrogAccount account)
            StopAccount(account);
        else
            MessageBox.Show("Please select an account first.", "Stop Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (AccountsGrid.SelectedItem is not CamfrogAccount account)
        {
            MessageBox.Show("Please select an account first.", "Remove Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (ProcessSessionService.IsTrackedProcessAlive(account, out var reason))
        {
            MessageBox.Show(
                "Stop the account before removing it.",
                "Remove Account",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!IsDefinitivelyStopped(reason) && account.ProcessId is not null)
        {
            MessageBox.Show(
                $"The manager cannot safely verify the process state. Removal is blocked.\n\n{reason}",
                "Remove Account",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var answer = MessageBox.Show(
            $"Remove '{account.DisplayName}'?\n\nThis deletes its stored credential and profile directory.",
            "Remove Account",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
            return;

        try
        {
            App.Db.Delete(account.Id);
            App.Credentials.Delete(account.PasswordSecretName);

            try
            {
                if (Directory.Exists(account.ProfileDirectory))
                    Directory.Delete(account.ProfileDirectory, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                App.Db.Log("WARN", $"Account '{account.DisplayName}' removed but profile cleanup failed: {ex.Message}");
            }

            App.Db.Log("INFO", $"Removed account '{account.DisplayName}'.");
            RefreshRuntimeState();
        }
        catch (Exception ex)
        {
            App.Db.Log("ERROR", $"Failed to remove account '{account.DisplayName}': {ex.Message}");
            MessageBox.Show(
                $"Unable to remove '{account.DisplayName}'.\n\n{ex.Message}",
                "Remove Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StartAll_Click(object sender, RoutedEventArgs e)
    {
        var settings = App.Settings.Load();
        foreach (var account in App.Db.GetAccounts().Where(a => a.Enabled))
            StartAccount(account, settings);

        RefreshRuntimeState();
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var account in App.Db.GetAccounts())
            StopAccount(account);

        RefreshRuntimeState();
    }

    private static void StartAccount(CamfrogAccount account, AppSettings? settings = null)
    {
        settings ??= App.Settings.Load();

        try
        {
            if (account.ProcessId is int)
            {
                if (ProcessSessionService.IsTrackedProcessAlive(account, out var reason))
                    return;

                if (!IsDefinitivelyStopped(reason))
                {
                    throw new InvalidOperationException(
                        reason ?? "Unable to safely verify the existing process.");
                }

                App.Db.UpdateRuntime(account.Id, "Stopped", null, null, reason ?? string.Empty);
            }

            App.Sessions.Start(account, settings);
        }
        catch (Exception ex)
        {
            App.Db.UpdateRuntime(account.Id, "Error", account.ProcessId, account.StartedAtUtc, ex.Message, account.ProcessExecutablePath);
            App.Db.Log("ERROR", $"{account.DisplayName}: {ex.Message}");
            MessageBox.Show(
                $"Unable to start '{account.DisplayName}'.\n\n{ex.Message}",
                "Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void StopAccount(CamfrogAccount account)
    {
        try
        {
            App.Sessions.Stop(account);
        }
        catch (Exception ex)
        {
            App.Db.UpdateRuntime(account.Id, "Error", account.ProcessId, account.StartedAtUtc, ex.Message, account.ProcessExecutablePath);
            App.Db.Log("ERROR", $"{account.DisplayName}: {ex.Message}");
            MessageBox.Show(
                $"Unable to stop '{account.DisplayName}'.\n\n{ex.Message}",
                "Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
