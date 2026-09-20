using System.IO;
using System.Windows;
using Microsoft.Win32;
using CamfrogMultiID.Core;

namespace CamfrogMultiID.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var settings = App.Settings.Load();
        Exe.Text = settings.ClientExecutable;
        Args.Text = settings.ClientArgumentsTemplate;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) Exe.Text = dialog.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var executable = Exe.Text.Trim();
        if (!string.IsNullOrWhiteSpace(executable) && !File.Exists(executable))
        {
            MessageBox.Show("The selected executable does not exist.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            App.Settings.Save(new AppSettings { ClientExecutable = executable, ClientArgumentsTemplate = Args.Text.Trim() });
            App.Db.Log("INFO", "Settings saved.");
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to save settings.\n\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
