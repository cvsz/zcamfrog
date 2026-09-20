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
        if (dialog.ShowDialog(this) == true) SandboxExe.Text = dialog.FileName;
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

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var executable = Exe.Text.Trim();
        var template = Args.Text.Trim();
        var useSandboxie = SandboxBox.IsChecked == true;
        var sandboxExe = SandboxExe.Text.Trim();
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
            App.Settings.Save(new AppSettings { ClientExecutable = executable, ClientArgumentsTemplate = template, UseSandboxie = useSandboxie, SandboxieStartExe = sandboxExe });
            App.Db.Log("INFO", "Settings saved.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to save settings.\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
