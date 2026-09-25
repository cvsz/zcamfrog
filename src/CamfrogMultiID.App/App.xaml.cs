using System.Reflection;
using System.Threading;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.App;

public partial class App : Application
{
  public static AppPaths Paths { get; private set; } = null!;
  public static DatabaseService Db { get; private set; } = null!;
  public static CredentialService Credentials { get; private set; } = null!;
  public static SettingsService Settings { get; private set; } = null!;
  public static ProcessSessionService Sessions { get; private set; } = null!;

  private static readonly Mutex SingleInstanceMutex =
      new(false, "Local\\CamfrogMultiID.Manager");

  private static bool _ownsMutex;

  public App()
  {
    DispatcherUnhandledException += OnDispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
  }

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    try
    {
      _ownsMutex = SingleInstanceMutex.WaitOne(0, false);
    }
    catch (AbandonedMutexException)
    {
      _ownsMutex = true;
    }

    if (!_ownsMutex)
    {
      MessageBox.Show(
          "Camfrog Multi-ID Manager is already running.",
          "Already Running",
          MessageBoxButton.OK,
          MessageBoxImage.Information);
      Shutdown();
      return;
    }

    try
    {
      // Initialize dependencies inside the guarded startup path.
      // Static field initialization previously happened before OnStartup,
      // which could terminate a WinExe without any visible diagnostic.
      Paths = new AppPaths();
      Db = new DatabaseService(Paths);
      Credentials = new CredentialService(Paths);
      Settings = new SettingsService(Paths);
      Sessions = new ProcessSessionService(Db);
      ApplyLanguage(Settings.Load().Language);

      Db.Initialize();
      Db.Log("INFO", $"CamfrogMultiID v{AppVersion} starting. PID={Environment.ProcessId}");
      Db.Log("INFO", Paths.SecretsAclRestricted
          ? "Secrets: DPAPI protected, directory ACL restricted to current user."
          : "Secrets: DPAPI protected, directory ACL hardening unavailable.");

      var window = new MainWindow();
      MainWindow = window;
      window.Show();
      window.Activate();
      window.Focus();
    }
    catch (Exception ex)
    {
      WriteEmergencyLog(ex);

      try { Db?.Log("ERROR", $"Application startup failed: {ex}"); } catch { }

      MessageBox.Show(
          $"Application startup failed.\n\n{ex.Message}\n\nA diagnostic log was written to:\n{EmergencyLogPath()}",
          "Startup Error",
          MessageBoxButton.OK,
          MessageBoxImage.Error);

      Shutdown(-1);
    }
  }

  protected override void OnExit(ExitEventArgs e)
  {
    try
    {
      if (_ownsMutex)
      {
        SingleInstanceMutex.ReleaseMutex();
        _ownsMutex = false;
      }
    }
    catch (ApplicationException) { }
    finally
    {
      SingleInstanceMutex.Dispose();
    }

    base.OnExit(e);
  }

  public static string AppVersion =>
      Assembly.GetEntryAssembly()
          ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
          ?.InformationalVersion.Split('+')[0] ?? "unknown";

  public static void ApplyLanguage(string? language)
  {
    var culture = new System.Globalization.CultureInfo(
        string.Equals(language, "th", StringComparison.OrdinalIgnoreCase) ? "th" : "en");
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
    Thread.CurrentThread.CurrentUICulture = culture;
  }

  private static string EmergencyLogPath()
  {
    var root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CamfrogMultiID");
    Directory.CreateDirectory(root);
    return Path.Combine(root, "startup-error.log");
  }

  private static void WriteEmergencyLog(Exception ex)
  {
    try
    {
      File.AppendAllText(
          EmergencyLogPath(),
          $"[{DateTime.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
    }
    catch
    {
      // Never throw from the emergency logger.
    }
  }

  private static void OnDispatcherUnhandledException(
      object sender,
      DispatcherUnhandledExceptionEventArgs e)
  {
    WriteEmergencyLog(e.Exception);
    try { Db?.Log("ERROR", $"Unhandled UI exception: {e.Exception}"); } catch { }
    e.Handled = false;
  }

  private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
  {
    if (e.ExceptionObject is Exception ex)
      WriteEmergencyLog(ex);
  }

  private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
  {
    WriteEmergencyLog(e.Exception);
    try { Db?.Log("ERROR", $"Unobserved task exception: {e.Exception}"); } catch { }
    e.SetObserved();
  }
}
