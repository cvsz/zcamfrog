using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var settings = App.Settings.Load();
        Exe.Text = settings.ClientExecutable;
        Args.Text = settings.ClientArgumentsTemplate;
        SandboxBox.IsChecked = settings.UseSandboxie;
        SandboxExe.Text = settings.SandboxieStartExe;
        BackupDays.Text = settings.AutoBackupDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
        BackupKeep.Text = settings.AutoBackupKeepCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        UpdateSandboxStatus();
        SandboxExe.TextChanged += (_, _) => UpdateSandboxStatus();
        if (settings.UseSandboxie && string.IsNullOrWhiteSpace(settings.SandboxieStartExe) && ProcessSessionService.FindSandboxieStart() is null)
            ExeStatus.Text = "Sandboxie Start.exe was not auto-detected. Install Sandboxie-Plus or set its path below.";
        DataDirText.Text = $"Data: {App.Paths.Root}";
        UpdateExeStatus();
        Exe.TextChanged += (_, _) => UpdateExeStatus();
        Args.TextChanged += (_, _) => UpdateExeStatus();
    }

    private void UpdateExeStatus()
    {
        var exe = Exe.Text.Trim();
        if (string.IsNullOrWhiteSpace(exe))
        {
            ExeStatus.Text = "No client executable configured. Start operations will be disabled until configured.";
            return;
        }
        if (!File.Exists(exe))
        {
            ExeStatus.Text = "Warning: the selected executable does not exist.";
            return;
        }
        var warnings = ProcessSessionService.ValidateArgumentsTemplate(Args.Text.Trim());
        ExeStatus.Text = warnings.Count == 0
            ? $"Executable found. Arguments preview uses {{username}} and {{profile}} only."
            : $"Executable found. Template warning: {string.Join(" ", warnings)}";
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
        var resolved = string.IsNullOrWhiteSpace(configured) ? ProcessSessionService.FindSandboxieStart() : configured;
        SandboxStatus.Text = string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved)
            ? "Sandboxie: not found. Use the download button below, then Browse to Start.exe."
            : $"Sandboxie: found at {resolved}. Boxes live under {ProcessSessionService.GetBoxesRoot()}.";
    }

    private void DownloadSandboxie_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ProcessSessionService.SandboxieReleasesUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to open the download page.\n\n{ex.Message}", "Download", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CreateBoxes_Click(object sender, RoutedEventArgs e)
    {
        var configured = SandboxExe.Text.Trim();
        var startExe = string.IsNullOrWhiteSpace(configured) ? ProcessSessionService.FindSandboxieStart() : configured;
        if (string.IsNullOrWhiteSpace(startExe) || !File.Exists(startExe))
        {
            MessageBox.Show("Sandboxie Start.exe was not found. Install Sandboxie-Plus first.", "Create Boxes", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                ? $"Created {ok} Sandboxie box(es). Accounts can now run simultaneously."
                : $"Created {ok} box(es). Failures:\n\n{string.Join("\n", failures)}",
            "Create Boxes",
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
            MessageBox.Show($"Unable to open data folder.\n\n{ex.Message}", "Open Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"Backup saved.\n\n{dialog.FileName}\n\nIncludes database, secrets, and settings. Profile directories are excluded.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to create backup.\n\n{ex.Message}", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Backup (*.zip)|*.zip", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true)
            return;
        var confirm = MessageBox.Show(
            "Restore replaces the current database, secrets, and settings. Stop all running clients first.\n\nContinue?",
            "Confirm Restore",
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
            MessageBox.Show("Restore complete. The account list will refresh.", "Restore", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to restore backup.\n\n{ex.Message}", "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var executable = Exe.Text.Trim();
        var template = Args.Text.Trim();
        var useSandboxie = SandboxBox.IsChecked == true;
        var sandboxExe = SandboxExe.Text.Trim();
        if (!int.TryParse(BackupDays.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var backupDays) || backupDays < 0 || backupDays > 365)
        {
            MessageBox.Show("Auto-backup interval must be a number from 0 to 365 (0 = off).", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(BackupKeep.Text.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var backupKeep) || backupKeep < 1 || backupKeep > 100)
        {
            MessageBox.Show("Backups to keep must be a number from 1 to 100.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!string.IsNullOrWhiteSpace(executable) && !File.Exists(executable))
        {
            MessageBox.Show("The selected executable does not exist.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (useSandboxie)
        {
            var resolved = string.IsNullOrWhiteSpace(sandboxExe) ? ProcessSessionService.FindSandboxieStart() : sandboxExe;
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
            {
                MessageBox.Show("Sandboxie is enabled but Start.exe was not found. Install Sandboxie-Plus or set its path.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var warnings = ProcessSessionService.ValidateArgumentsTemplate(template);
        if (warnings.Count > 0)
        {
            var result = MessageBox.Show(
                $"Arguments template has warnings:\n\n{string.Join("\n", warnings)}\n\nSave anyway?",
                "Template Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }
        try
        {
            var current = App.Settings.Load();
            App.Settings.Save(new AppSettings { ClientExecutable = executable, ClientArgumentsTemplate = template, UseSandboxie = useSandboxie, SandboxieStartExe = sandboxExe, AutoBackupDays = backupDays, AutoBackupKeepCount = backupKeep, LastAutoBackupUtc = current.LastAutoBackupUtc });
            App.Db.Log("INFO", "Settings saved.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to save settings.\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
