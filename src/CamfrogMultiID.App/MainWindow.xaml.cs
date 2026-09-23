using CamfrogMultiID.Infrastructure;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CamfrogMultiID.Core;

namespace CamfrogMultiID.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly RestartPolicy _restartPolicy = new();
    private bool _refreshing;
    private bool _initialized;
    private string _logLevelFilter = "All";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _timer.Stop();
        _timer.Tick += (_, _) => RefreshRuntimeState();
        // Screen-reader names for controls whose purpose is not in their text.
        System.Windows.Automation.AutomationProperties.SetName(SearchBox, "Search accounts");
        System.Windows.Automation.AutomationProperties.SetName(AccountsGrid, "Accounts");
        System.Windows.Automation.AutomationProperties.SetName(LogBox, "Application log");
        System.Windows.Automation.AutomationProperties.SetName(DetailsBox, "Selected account details");
        // SelectionChanged/TextChanged fire during InitializeComponent (XAML default
        // selection); ignore them until construction is complete. See startup-error.log
        // NullReferenceException at UpdateLogBox via LogLevelBox_SelectionChanged.
        _initialized = true;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshRuntimeState();
        RunAutoBackupIfDue();
        _timer.Start();
    }

    private static void RunAutoBackupIfDue()
    {
        try
        {
            var settings = App.Settings.Load();
            if (!BackupService.IsBackupDue(settings, DateTime.UtcNow))
                return;
            var dir = BackupService.BackupsDirectory(App.Paths);
            var dest = Path.Combine(dir, BackupService.DefaultBackupName(DateTime.Now));
            BackupService.CreateBackup(App.Paths, dest);
            BackupService.PruneBackups(dir, Math.Max(1, settings.AutoBackupKeepCount));
            settings.LastAutoBackupUtc = DateTime.UtcNow;
            App.Settings.Save(settings);
            App.Db.Log("INFO", $"Automatic backup created: '{dest}'.");
        }
        catch (Exception ex)
        {
            // Backups must never break startup.
            try { App.Db?.Log("ERROR", $"Automatic backup failed: {ex.Message}"); } catch { }
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initialized) return;
        RefreshRuntimeState();
    }

    private void LogLevelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (LogLevelBox.SelectedItem is ComboBoxItem item && item.Content is string level)
        {
            _logLevelFilter = level;
            RefreshRuntimeState();
        }
    }

    private void AccountsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        UpdateDetails();
    }

    private void AccountsGrid_DoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void AccountsGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            RefreshRuntimeState();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            StartSelected();
            e.Handled = true;
        }
    }

    private void RefreshRuntimeState()
    {
        if (!_initialized || _refreshing) return;
        _refreshing = true;

        try
        {
            var selectedId = (AccountsGrid.SelectedItem as CamfrogAccount)?.Id;

            var settings = App.Settings.Load();
            foreach (var account in App.Db.GetAccounts())
            {
                if (account.ProcessId is not int || !account.Status.Equals("Running", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ProcessSessionService.IsTrackedProcessAlive(account, out _))
                    continue;

                var reason = "Process exited.";
                if (account.Enabled && account.AutoRestart)
                {
                    if (_restartPolicy.ShouldRestart(account.Id, DateTime.UtcNow))
                    {
                        App.Db.Log("INFO", $"{account.DisplayName}: {reason} Attempting auto-restart.");
                        if (TryStartAccount(account, settings, interactive: false))
                        {
                            App.Db.Log("INFO", $"{account.DisplayName}: auto-restarted.");
                            continue;
                        }
                        continue;
                    }

                    App.Db.UpdateRuntime(account.Id, "Error", null, null, "Auto-restart paused: too many restarts in a short time.");
                    App.Db.Log("WARN", $"{account.DisplayName}: auto-restart paused (restart loop suspected).");
                    continue;
                }

                App.Db.UpdateRuntime(account.Id, "Stopped", null, null, reason);
                App.Db.Log("INFO", $"{account.DisplayName}: {reason}");
            }

            var all = App.Db.GetAccounts();
            var filter = SearchBox?.Text?.Trim() ?? string.Empty;
            List<CamfrogAccount> view = string.IsNullOrWhiteSpace(filter)
                ? all
                : all.Where(a =>
                    a.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    a.Username.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    a.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            AccountsGrid.ItemsSource = view;
            if (selectedId is long sid)
            {
                var match = view.FirstOrDefault(a => a.Id == sid);
                if (match is not null)
                    AccountsGrid.SelectedItem = match;
            }

            UpdateDetails(view);
            UpdateStatusBar(all);
            UpdateLogBox();
        }
        catch (Exception ex)
        {
            // A corrupt database, locked file, or unexpected OS failure must never
            // kill the UI timer loop. Keep the previous grid state and record the
            // failure for diagnosis.
            try { App.Db?.Log("ERROR", $"Refresh failed: {ex.Message}"); } catch { }
            try { StatusCounts.Text = L10n.Fmt(Strings.StatusRefreshFailed, ex.Message); } catch { }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void UpdateDetails() => UpdateDetails(AccountsGrid.ItemsSource as IEnumerable<CamfrogAccount>);

    private void UpdateDetails(IEnumerable<CamfrogAccount>? view)
    {
        if (AccountsGrid.SelectedItem is not CamfrogAccount account)
        {
            DetailsBox.Text = Strings.DetailsSelectPrompt;
            return;
        }

        var settings = App.Settings.Load();
        var preview = ProcessSessionService.PreviewLaunch(settings, account);
        var secretExists = App.Credentials.Exists(account.PasswordSecretName);
        var profileExists = Directory.Exists(account.ProfileDirectory);
        var startedLocal = account.StartedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture) ?? "โ€”";
        var uptime = account.StartedAtUtc is DateTime started && account.Status.Equals("Running", StringComparison.OrdinalIgnoreCase)
            ? FormatDuration(DateTime.UtcNow - started.ToUniversalTime())
            : "โ€”";
        var restarts = _restartPolicy.GetAttemptCount(account.Id, DateTime.UtcNow);
        var passwordAge = account.PasswordChangedUtc is DateTime changed
            ? $"{(int)(DateTime.UtcNow - changed.ToUniversalTime()).TotalDays}d" + ((DateTime.UtcNow - changed.ToUniversalTime()).TotalDays > 90 ? " (rotation recommended)" : string.Empty)
            : "unknown";
        DetailsBox.Text =
            $"Id: {account.Id}  Display: {account.DisplayName}  User: {account.Username}  Enabled: {account.Enabled}  Status: {account.Status}  AutoRestart: {account.AutoRestart}\n" +
            $"PID: {(account.ProcessId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "โ€”")}  Started UTC: {(account.StartedAtUtc?.ToString("O") ?? "โ€”")}  Local: {startedLocal}  Uptime: {uptime}  Restarts(10m): {restarts}\n" +
            $"Profile: {account.ProfileDirectory} {(profileExists ? "[exists]" : "[missing]")}\n" +
            $"Secret: {account.PasswordSecretName} {(secretExists ? "[DPAPI protected]" : "[missing]")}  Exe: {(string.IsNullOrWhiteSpace(account.ProcessExecutablePath) ? "โ€”" : account.ProcessExecutablePath)}\n" +
            $"Room: {(string.IsNullOrWhiteSpace(account.RoomUrl) ? "โ€”" : account.RoomUrl)}  Password age: {passwordAge}\n" +
            (settings.UseSandboxie
                ? $"Box: {ProcessSessionService.SanitizeBoxName(account)} {(ProcessSessionService.BoxExists(ProcessSessionService.SanitizeBoxName(account)) ? "[created]" : "[not created โ€” use Settings]")}\n"
                : string.Empty) +
            $"Launch: {preview}";
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
            span = TimeSpan.Zero;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes}m"
            : span.TotalMinutes >= 1
                ? $"{(int)span.TotalMinutes}m {span.Seconds}s"
                : $"{(int)span.TotalSeconds}s";
    }

    private void UpdateStatusBar(List<CamfrogAccount> all)
    {
        var running = all.Count(a => a.Status.Equals("Running", StringComparison.OrdinalIgnoreCase));
        var err = all.Count(a => a.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var disabled = all.Count(a => !a.Enabled);
        StatusCounts.Text = L10n.Fmt(Strings.StatusCounts, all.Count, running, err, disabled);

        var settings = App.Settings.Load();
        StatusClient.Text = string.IsNullOrWhiteSpace(settings.ClientExecutable)
            ? Strings.ClientNone
            : File.Exists(settings.ClientExecutable)
                ? L10n.Fmt(Strings.ClientOk, settings.ClientExecutable)
                : L10n.Fmt(Strings.ClientMissing, settings.ClientExecutable);
    }

    private void UpdateLogBox()
    {
        try
        {
            var logFile = Path.Combine(App.Paths.Logs, "app.log");
            if (!File.Exists(logFile))
            {
                LogBox.Text = string.Empty;
                return;
            }

            // Read tail to avoid loading huge logs into the UI.
            var lines = File.ReadLines(logFile);
            IEnumerable<string> filtered = _logLevelFilter.Equals("All", StringComparison.OrdinalIgnoreCase)
                ? lines
                : lines.Where(l => l.Contains($"[{_logLevelFilter}]", StringComparison.OrdinalIgnoreCase));
            var tail = filtered.TakeLast(500);
            LogBox.Text = string.Join(Environment.NewLine, tail);
            if (AutoScrollBox?.IsChecked == true)
                LogBox.ScrollToEnd();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private CamfrogAccount? SelectedAccount() => AccountsGrid.SelectedItem as CamfrogAccount;

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (new AccountWindow { Owner = this }.ShowDialog() == true)
            RefreshRuntimeState();
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void EditSelected()
    {
        var account = SelectedAccount();
        if (account is null)
        {
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleEditAccount,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var fresh = App.Db.GetById(account.Id);
        if (fresh is null)
        {
            MessageBox.Show(Strings.MsgAccountGone, Strings.TitleEditAccount,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshRuntimeState();
            return;
        }
        if (new AccountWindow(fresh) { Owner = this }.ShowDialog() == true)
            RefreshRuntimeState();
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private void DeleteSelected()
    {
        var account = SelectedAccount();
        if (account is null)
        {
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleDeleteAccount,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fresh = App.Db.GetById(account.Id);
        if (fresh is null)
        {
            RefreshRuntimeState();
            return;
        }

        var confirm = MessageBox.Show(
            L10n.Fmt(Strings.MsgConfirmDelete, Environment.NewLine, fresh.DisplayName, fresh.Username),
            Strings.TitleConfirmDelete,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            // Stop first if still tracked alive; fail-closed Stop refuses foreign PIDs.
            if (fresh.ProcessId is int)
            {
                try { App.Sessions.Stop(fresh); } catch { }
                fresh = App.Db.GetById(fresh.Id) ?? fresh;
            }

            var secretName = fresh.PasswordSecretName;
            var profileDir = fresh.ProfileDirectory;
            var name = fresh.DisplayName;

            App.Db.Delete(fresh.Id);
            App.Credentials.Delete(secretName);
            try
            {
                if (Directory.Exists(profileDir))
                    Directory.Delete(profileDir, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            App.Db.Log("INFO", $"Deleted account '{name}' (id {fresh.Id}).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgDeleteFailed, account.DisplayName, Environment.NewLine, ex.Message),
                Strings.TitleDeleteError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshRuntimeState();
        }
    }

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e)
    {
        var account = SelectedAccount();
        if (account is null)
        {
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleEnableDisable,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            App.Db.SetEnabled(account.Id, !account.Enabled);
            App.Db.Log("INFO", $"Account '{account.DisplayName}' {(account.Enabled ? "disabled" : "enabled")}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgUpdateFailed, account.DisplayName, Environment.NewLine, ex.Message),
                Strings.TitleUpdateError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshRuntimeState();
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var account = SelectedAccount();
        if (account is null)
        {
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleChangePassword,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new ChangePasswordWindow(account.DisplayName) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            try
            {
                App.Credentials.Save(account.PasswordSecretName, dialog.NewPassword);
                App.Db.SetPasswordChanged(account.Id, DateTime.UtcNow);
                App.Db.Log("INFO", $"Password updated for '{account.DisplayName}'.");
                MessageBox.Show(Strings.MsgPasswordUpdated, Strings.TitleChangePassword,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(L10n.Fmt(Strings.MsgPasswordUpdateFailed, Environment.NewLine, ex.Message),
                    Strings.TitleChangePassword, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearError_Click(object sender, RoutedEventArgs e)
    {
        var account = SelectedAccount();
        if (account is null)
        {
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleClearError,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        App.Db.ClearError(account.Id);
        RefreshRuntimeState();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();
        RefreshRuntimeState();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshRuntimeState();

    private void ClearLogView_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    private void ExportLog_Click(object sender, RoutedEventArgs e)
    {
        var source = Path.Combine(App.Paths.Logs, "app.log");
        if (!File.Exists(source))
        {
            MessageBox.Show(Strings.MsgNoLogFile, Strings.ExportLog,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Log (*.log)|*.log",
            FileName = $"CamfrogMultiID-{DateTime.Now:yyyyMMdd-HHmmss}.log"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            File.Copy(source, dialog.FileName, overwrite: true);
            App.Db.Log("INFO", $"Log exported to '{dialog.FileName}'.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgExportLogFailed, Environment.NewLine, ex.Message), Strings.ExportLog,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Diagnostics (*.zip)|*.zip",
            FileName = $"CamfrogMultiID-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            DiagnosticsService.ExportBundle(App.Paths, App.Db, dialog.FileName);
            App.Db.Log("INFO", $"Diagnostics bundle exported to '{dialog.FileName}'.");
            MessageBox.Show(
                L10n.Fmt(Strings.MsgDiagnosticsDone, Environment.NewLine, dialog.FileName),
                Strings.Diagnostics,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgDiagnosticsFailed, Environment.NewLine, ex.Message), Strings.TitleDiagnosticsError,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(App.Paths.Logs);
            Process.Start(new ProcessStartInfo { FileName = App.Paths.Logs, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgOpenLogFolderFailed, Environment.NewLine, ex.Message), Strings.TitleLogs,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StartSelected_Click(object sender, RoutedEventArgs e) => StartSelected();

    private void StartSelected()
    {
        if (SelectedAccount() is CamfrogAccount account)
            StartAccount(account);
        else
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleStartAccount,
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StopSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAccount() is CamfrogAccount account)
            StopAccount(account);
        else
            MessageBox.Show(Strings.MsgSelectAccount, Strings.TitleStopAccount,
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void StartAll_Click(object sender, RoutedEventArgs e)
    {
        var enabled = App.Db.GetAccounts().Where(a => a.Enabled).ToList();
        if (enabled.Count == 0)
        {
            MessageBox.Show(Strings.MsgNoEnabledAccounts, Strings.TitleStartAll,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var confirm = MessageBox.Show(
            L10n.Fmt(Strings.MsgConfirmStartAll, enabled.Count, Environment.NewLine),
            Strings.TitleConfirmStartAll,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        var settings = App.Settings.Load();
        foreach (var account in enabled)
            StartAccount(account, settings);

        RefreshRuntimeState();
    }

    private void StopAll_Click(object sender, RoutedEventArgs e)
    {
        var all = App.Db.GetAccounts();
        if (all.Count == 0)
            return;
        var confirm = MessageBox.Show(
            L10n.Fmt(Strings.MsgConfirmStopAll, all.Count),
            Strings.TitleConfirmStopAll,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        foreach (var account in all)
            StopAccount(account);

        RefreshRuntimeState();
    }

    private void StartAccount(CamfrogAccount account, AppSettings? settings = null) =>
        TryStartAccount(account, settings ?? App.Settings.Load(), interactive: true);

    private bool TryStartAccount(CamfrogAccount account, AppSettings settings, bool interactive)
    {
        try
        {
            if (account.ProcessId is int)
            {
                if (ProcessSessionService.IsTrackedProcessAlive(account, out _))
                    return true;

                App.Db.UpdateRuntime(account.Id, "Stopped", null, null);
            }

            if (interactive && !settings.UseSandboxie && !string.IsNullOrWhiteSpace(settings.ClientExecutable))
            {
                var foreign = ProcessSessionService.FindForeignClientProcesses(settings.ClientExecutable, account.ProcessId);
                if (foreign.Count > 0)
                {
                    App.Db.Log("WARN", $"{account.DisplayName}: untracked client already running (PID {foreign[0].ProcessId}).");
                    var answer = MessageBox.Show(
                        L10n.Fmt(Strings.MsgForeignClient, foreign[0].ProcessId, Environment.NewLine),
                        Strings.TitleForeignClient,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (answer != MessageBoxResult.Yes)
                        return false;
                }
            }

            App.Sessions.Start(account, settings);
            _restartPolicy.Reset(account.Id);
            return true;
        }
        catch (Exception ex)
        {
            App.Db.UpdateRuntime(account.Id, "Error", null, null, ex.Message);
            App.Db.Log("ERROR", $"{account.DisplayName}: {ex.Message}");
            if (interactive)
            {
                MessageBox.Show(
                    L10n.Fmt(Strings.MsgStartFailed, account.DisplayName, Environment.NewLine, ex.Message),
                    Strings.TitleStartError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }
    }

    private void StopAccount(CamfrogAccount account)
    {
        try
        {
            App.Sessions.Stop(account);
            _restartPolicy.Reset(account.Id);
        }
        catch (Exception ex)
        {
            App.Db.UpdateRuntime(account.Id, "Error", null, null, ex.Message);
            App.Db.Log("ERROR", $"{account.DisplayName}: {ex.Message}");
            MessageBox.Show(
                L10n.Fmt(Strings.MsgStopFailed, account.DisplayName, Environment.NewLine, ex.Message),
                Strings.TitleStopError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

