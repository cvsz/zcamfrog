using CamfrogMultiID.Infrastructure;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CamfrogMultiID.Core;

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
                    App.Db.UpdateRuntime(account.Id, "Stopped", null, null, reason ?? "Process exited.");
                    App.Db.Log("INFO", $"{account.DisplayName}: {reason ?? "Process exited."}");
                }
            }

            AccountsGrid.ItemsSource = null;
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
                if (ProcessSessionService.IsTrackedProcessAlive(account, out _))
                    return;

                App.Db.UpdateRuntime(account.Id, "Stopped", null, null);
            }

            App.Sessions.Start(account, settings);
        }
        catch (Exception ex)
        {
            App.Db.UpdateRuntime(account.Id, "Error", null, null, ex.Message);
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
            App.Db.UpdateRuntime(account.Id, "Error", null, null, ex.Message);
            App.Db.Log("ERROR", $"{account.DisplayName}: {ex.Message}");
            MessageBox.Show(
                $"Unable to stop '{account.DisplayName}'.\n\n{ex.Message}",
                "Stop Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
