using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.App;

public partial class SettingsWindow : Window
{
    private bool _initialized;
    private string _initialLanguage = "en";

    public SettingsWindow()
    {
        InitializeComponent();
        var settings = App.Settings.Load();
        Exe.Text = settings.ClientExecutable;
        Args.Text = settings.ClientArgumentsTemplate;
        SandboxBox.IsChecked = settings.UseSandboxie;
        SandboxExe.Text = settings.SandboxieStartExe;
        BackupDays.Text = settings.AutoBackupDays.ToString(CultureInfo.InvariantCulture);
        BackupKeep.Text = settings.AutoBackupKeepCount.ToString(CultureInfo.InvariantCulture);
        AutoStartBox.IsChecked = settings.AutoStartAccounts;
        SelectLanguage(settings.Language);
        _initialLanguage = NormalizeLanguage(settings.Language);
        UpdateSandboxStatus();
        SandboxExe.TextChanged += (_, _) => UpdateSandboxStatus();
        if (settings.UseSandboxie && string.IsNullOrWhiteSpace(settings.SandboxieStartExe) && ProcessSessionService.FindSandboxieStart() is null)
            ExeStatus.Text = Strings.SandboxieAutodetectFail;
        DataDirText.Text = L10n.Fmt(Strings.DataPrefix, App.Paths.Root);
        UpdateExeStatus();
        Exe.TextChanged += (_, _) => UpdateExeStatus();
        Args.TextChanged += (_, _) => UpdateExeStatus();
        _initialized = true;
    }

    private void SelectLanguage(string language)
    {
        var tag = NormalizeLanguage(language);
        foreach (var item in LanguageBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedItem = item;
                return;
            }
        }
        LanguageBox.SelectedIndex = 0;
    }

    private static string NormalizeLanguage(string? language) =>
        string.Equals(language, "th", StringComparison.OrdinalIgnoreCase) ? "th" : "en";

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
    }

    private void UpdateExeStatus()
    {
        var exe = Exe.Text.Trim();
        if (string.IsNullOrWhiteSpace(exe))
        {
            ExeStatus.Text = Strings.ExeStatusNone;
            return;
        }
        if (!File.Exists(exe))
        {
            ExeStatus.Text = Strings.ExeStatusMissing;
            return;
        }
        var warnings = ProcessSessionService.ValidateArgumentsTemplate(Args.Text.Trim());
        ExeStatus.Text = warnings.Count == 0
            ? Strings.ExeStatusOk
            : L10n.Fmt(Strings.ExeStatusWarn, string.Join(" ", warnings));
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            Exe.Text = dialog.FileName;
            UpdateExeStatus();
        }
    }

    private void BrowseSandbox_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Start.exe|Start.exe|Executable (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            SandboxExe.Text = dialog.FileName;
            UpdateSandboxStatus();
        }
    }

    private void UpdateSandboxStatus()
    {
        var configured = SandboxExe.Text.Trim();
        if (!ProcessSessionService.IsSandboxieReady(configured, out var readiness))
        {
            SandboxStatus.Text = string.IsNullOrWhiteSpace(readiness)
                ? Strings.SandboxStatusNone
                : $"Sandboxie: not ready — {readiness}";
            return;
        }
        var resolved = string.IsNullOrWhiteSpace(configured) ? ProcessSessionService.FindSandboxieStart()! : configured;
        var version = ProcessSessionService.GetSandboxieVersion(resolved);
        SandboxStatus.Text = L10n.Fmt(Strings.SandboxStatusOk, resolved, ProcessSessionService.GetBoxesRoot())
            + (string.IsNullOrWhiteSpace(version) ? string.Empty : $" v{version}");
    }

    private void DownloadSandboxie_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ProcessSessionService.SandboxieReleasesUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgDownloadFailed, Environment.NewLine, ex.Message), Strings.DownloadTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateBoxes_Click(object sender, RoutedEventArgs e)
    {
        var configured = SandboxExe.Text.Trim();
        var startExe = string.IsNullOrWhiteSpace(configured) ? ProcessSessionService.FindSandboxieStart() : configured;
        if (string.IsNullOrWhiteSpace(startExe) || !File.Exists(startExe))
        {
            MessageBox.Show(Strings.MsgCreateBoxesNoExe, Strings.TitleCreateBoxes, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ok = 0;
        var failures = new List<string>();
        foreach (var account in App.Db.GetAccounts())
        {
            try
            {
                ProcessSessionService.CreateBox(startExe, ProcessSessionService.SanitizeBoxName(account));
                ok++;
            }
            catch (Exception ex)
            {
                failures.Add($"{account.DisplayName}: {ex.Message}");
            }
        }

        App.Db.Log("INFO", $"Created {ok} Sandboxie box(es).");
        MessageBox.Show(
            failures.Count == 0
                ? L10n.Fmt(Strings.BoxesDone, ok)
                : L10n.Fmt(Strings.BoxesDoneFailures, ok, Environment.NewLine, string.Join(Environment.NewLine, failures)),
            Strings.TitleCreateBoxes,
            MessageBoxButton.OK,
            failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        UpdateSandboxStatus();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(App.Paths.Root);
            Process.Start(new ProcessStartInfo { FileName = App.Paths.Root, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgOpenFolderFailed, Environment.NewLine, ex.Message), Strings.TitleOpenFolder, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Backup (*.zip)|*.zip",
            FileName = $"CamfrogMultiID-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };
        if (dialog.ShowDialog(this) != true)
            return;
        try
        {
            BackupService.CreateBackup(App.Paths, dialog.FileName);
            App.Db.Log("INFO", $"Data backed up to '{dialog.FileName}'.");
            MessageBox.Show(L10n.Fmt(Strings.BackupSavedBody, Environment.NewLine, dialog.FileName), Strings.TitleBackup, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgBackupFailed, Environment.NewLine, ex.Message), Strings.TitleBackupError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Backup (*.zip)|*.zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true)
            return;
        var confirm = MessageBox.Show(
            L10n.Fmt(Strings.MsgConfirmRestore, Environment.NewLine),
            Strings.TitleConfirmRestore,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;
        try
        {
            foreach (var account in App.Db.GetAccounts())
            {
                if (account.ProcessId is int)
                    App.Sessions.Stop(account);
            }
            BackupService.RestoreBackup(App.Paths, dialog.FileName);
            App.Db.Log("INFO", $"Data restored from '{dialog.FileName}'.");
            MessageBox.Show(Strings.RestoreDoneBody, Strings.TitleRestore, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgRestoreFailed, Environment.NewLine, ex.Message), Strings.TitleRestoreError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var executable = Exe.Text.Trim();
        var template = Args.Text.Trim();
        var useSandboxie = SandboxBox.IsChecked == true;
        var sandboxExe = SandboxExe.Text.Trim();
        var language = (LanguageBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "en";
        language = NormalizeLanguage(language);
        if (!int.TryParse(BackupDays.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var backupDays) || backupDays < 0 || backupDays > 365)
        {
            MessageBox.Show(Strings.MsgBackupDaysInvalid, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(BackupKeep.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var backupKeep) || backupKeep < 1 || backupKeep > 100)
        {
            MessageBox.Show(Strings.MsgBackupKeepInvalid, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(executable) && !File.Exists(executable))
        {
            MessageBox.Show(Strings.MsgExeNotFound, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (useSandboxie)
        {
            var resolved = string.IsNullOrWhiteSpace(sandboxExe) ? ProcessSessionService.FindSandboxieStart() : sandboxExe;
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            {
                MessageBox.Show(Strings.MsgSandboxieMissing, Strings.TitleValidation, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var warnings = ProcessSessionService.ValidateArgumentsTemplate(template);
        if (warnings.Count > 0)
        {
            var result = MessageBox.Show(
                L10n.Fmt(Strings.MsgTemplateWarning, Environment.NewLine, string.Join(Environment.NewLine, warnings)),
                Strings.TitleTemplateWarning,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }
        try
        {
            var current = App.Settings.Load();
            App.Settings.Save(new AppSettings { ClientExecutable = executable, ClientArgumentsTemplate = template, UseSandboxie = useSandboxie, SandboxieStartExe = sandboxExe, AutoBackupDays = backupDays, AutoBackupKeepCount = backupKeep, LastAutoBackupUtc = current.LastAutoBackupUtc, Language = language, AutoStartAccounts = AutoStartBox.IsChecked == true });
            App.Db.Log("INFO", "Settings saved.");
            if (!string.Equals(language, _initialLanguage, StringComparison.OrdinalIgnoreCase))
                MessageBox.Show(Strings.MsgRestartRequired, Strings.TitleLanguage, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(L10n.Fmt(Strings.MsgSaveSettingsFailed, Environment.NewLine, ex.Message), Strings.TitleSaveError, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

