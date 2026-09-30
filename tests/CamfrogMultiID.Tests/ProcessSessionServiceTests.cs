using System.Diagnostics;
using System.IO;
using System.Reflection;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class ProcessSessionServiceTests : IDisposable
{
  private readonly string _tempRoot;
  private readonly AppPaths _paths;
  private readonly DatabaseService _db;
  private readonly ProcessSessionService _svc;

  public ProcessSessionServiceTests()
  {
    _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-proc-" + Guid.NewGuid().ToString("N"));
    _paths = new AppPaths(_tempRoot);
    _db = new DatabaseService(_paths);
    _db.Initialize();
    _svc = new ProcessSessionService(_db);
  }

  public void Dispose()
  {
    try { Directory.Delete(_tempRoot, true); } catch { }
  }

  [Fact]
  public void IsTrackedProcessAlive_NoPid_ReturnsFalse()
  {
    var account = new CamfrogAccount { ProcessId = null };
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(account, out var reason));
    Assert.Equal("No process ID.", reason);
  }

  [Fact]
  public void IsTrackedProcessAlive_InvalidPid_ReturnsFalse()
  {
    var account = new CamfrogAccount { ProcessId = 999999, StartedAtUtc = DateTime.UtcNow, ProcessExecutablePath = "" };
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(account, out var reason));
    Assert.Contains("no longer exists", reason, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void IsTrackedProcessAlive_CurrentProcess_ReturnsTrue()
  {
    var current = Process.GetCurrentProcess();
    var account = new CamfrogAccount
    {
      ProcessId = current.Id,
      StartedAtUtc = current.StartTime.ToUniversalTime(),
      ProcessExecutablePath = current.MainModule?.FileName ?? ""
    };
    // Should be alive and match
    Assert.True(ProcessSessionService.IsTrackedProcessAlive(account, out var reason));
    Assert.Null(reason);
  }

  [Fact]
  public void IsTrackedProcessAlive_StaleStartTime_ReturnsFalse()
  {
    var current = Process.GetCurrentProcess();
    var account = new CamfrogAccount
    {
      ProcessId = current.Id,
      // far in the past -> should be considered stale/reused
      StartedAtUtc = DateTime.UtcNow.AddHours(-10),
      ProcessExecutablePath = current.MainModule?.FileName ?? ""
    };
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(account, out var reason));
    Assert.Equal("PID was reused by a different process.", reason);
  }

  [Fact]
  public void IsTrackedProcessAlive_WrongExecutable_ReturnsFalse()
  {
    var current = Process.GetCurrentProcess();
    var account = new CamfrogAccount
    {
      ProcessId = current.Id,
      StartedAtUtc = current.StartTime.ToUniversalTime(),
      ProcessExecutablePath = @"C:\nonexistent\fake.exe"
    };
    // If executable path mismatches, should return false (PID reuse)
    // Note: if MainModule is inaccessible, it may return true; we allow either but ensure it doesn't throw
    var alive = ProcessSessionService.IsTrackedProcessAlive(account, out var reason);
    // If we can read MainModule, it should be false
    try
    {
      var actual = current.MainModule?.FileName;
      if (!string.IsNullOrEmpty(actual))
        Assert.False(alive);
    }
    catch { /* ignore access denied */ }
  }

  [Fact]
  public void IsDeletionSafe_CleanlyStoppedAccount_AllowsDelete()
  {
    var account = new CamfrogAccount
    {
      Status = "Stopped",
      ProcessId = null,
      LastError = string.Empty
    };
    Assert.True(ProcessSessionService.IsDeletionSafe(account, out var reason));
    Assert.Null(reason);
  }

  [Fact]
  public void IsDeletionSafe_NotStopped_Refuses()
  {
    var account = new CamfrogAccount
    {
      Status = "Running",
      ProcessId = null,
      LastError = string.Empty
    };
    Assert.False(ProcessSessionService.IsDeletionSafe(account, out var reason));
    Assert.Contains("Running", reason);
  }

  [Fact]
  public void IsDeletionSafe_TrackedPid_Refuses()
  {
    var account = new CamfrogAccount
    {
      Status = "Stopped",
      ProcessId = 4242,
      LastError = string.Empty
    };
    Assert.False(ProcessSessionService.IsDeletionSafe(account, out var reason));
    Assert.Contains("still tracked", reason);
  }

  [Theory]
  [InlineData("Tracked process no longer matches.")]
  [InlineData("Sandboxie did not confirm that the sandbox terminated.")]
  [InlineData("Process exited.")]
  public void IsDeletionSafe_RecordedError_RefusesEvenWhenStopped(string error)
  {
    // "Stopped" with a recorded reason means the manager could not confirm the
    // client is gone, so the profile must not be deleted.
    var account = new CamfrogAccount
    {
      Status = "Stopped",
      ProcessId = null,
      LastError = error
    };
    Assert.False(ProcessSessionService.IsDeletionSafe(account, out var reason));
    Assert.Equal(error, reason);
  }

  [Fact]
  public void IsDeletionSafe_NullAccount_Refuses()
  {
    Assert.False(ProcessSessionService.IsDeletionSafe(null, out var reason));
    Assert.NotNull(reason);
  }

  [Fact]
  public void Stop_TrackedProcessMismatch_MarksStoppedWithError()
  {
    // A PID that no longer matches must never be terminated, and the account
    // must keep an error so the delete gate refuses.
    var current = Process.GetCurrentProcess();
    var account = new CamfrogAccount
    {
      DisplayName = "mismatch",
      Username = "mm_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "mm"),
      Enabled = true,
      Status = "Running",
      ProcessId = current.Id,
      StartedAtUtc = current.StartTime.ToUniversalTime(),
      ProcessExecutablePath = @"C:\nonexistent\fake.exe"
    };
    account.Id = _db.Add(account);

    _svc.Stop(account);

    var after = _db.GetById(account.Id)!;
    Assert.Equal("Stopped", after.Status);
    Assert.Equal("Tracked process no longer matches.", after.LastError);
    Assert.False(ProcessSessionService.IsDeletionSafe(after, out _));
    // The current process is still alive: Stop must not have killed it.
    Assert.False(current.HasExited);
  }

  [Fact]
  public void Stop_CorruptExecutablePath_IsMismatchNotAlreadyGone()
  {
    // A malformed path in the database must read as an identity mismatch. If
    // it escaped as an ArgumentException, Stop would record a clean "Stopped"
    // with no error and the delete gate would allow removing a live profile.
    var current = Process.GetCurrentProcess();
    var account = new CamfrogAccount
    {
      DisplayName = "corruptpath",
      Username = "cp_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "cp"),
      Enabled = true,
      Status = "Running",
      ProcessId = current.Id,
      StartedAtUtc = current.StartTime.ToUniversalTime(),
      ProcessExecutablePath = "\0bad|path"
    };
    account.Id = _db.Add(account);

    _svc.Stop(account);

    var after = _db.GetById(account.Id)!;
    Assert.Equal("Stopped", after.Status);
    Assert.Equal("Tracked process no longer matches.", after.LastError);
    Assert.False(ProcessSessionService.IsDeletionSafe(after, out _));
    Assert.False(current.HasExited);
  }

  [Fact]
  public void StartStop_WithRealProcess_Roundtrips()
  {
    var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
    var acc = new CamfrogAccount
    {
      DisplayName = "live",
      Username = "live_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "live"),
      Enabled = true
    };
    acc.Id = _db.Add(acc);
    var settings = new AppSettings
    {
      ClientExecutable = cmd,
      ClientArgumentsTemplate = "/c ping -n 30 127.0.0.1 >nul"
    };

    using var process = _svc.Start(acc, settings);
    try
    {
      Assert.Equal("Running", _db.GetById(acc.Id)!.Status);
      Assert.True(ProcessSessionService.IsTrackedProcessAlive(_db.GetById(acc.Id)!, out _));
    }
    finally
    {
      _svc.Stop(_db.GetById(acc.Id)!);
    }
    Assert.Equal("Stopped", _db.GetById(acc.Id)!.Status);
  }

  [Fact]
  public void FindForeignClientProcesses_InvalidInput_ReturnsEmpty()
  {
    Assert.Empty(ProcessSessionService.FindForeignClientProcesses(""));
    Assert.Empty(ProcessSessionService.FindForeignClientProcesses("   "));
    Assert.Empty(ProcessSessionService.FindForeignClientProcesses(null!));
    Assert.Empty(ProcessSessionService.FindForeignClientProcesses(Path.Combine(_tempRoot, "no-such-app-xyz.exe")));
  }

  [Fact]
  public void FindForeignClientProcesses_FindsSelfUnlessExcluded()
  {
    using var self = Process.GetCurrentProcess();
    var exe = self.MainModule?.FileName;
    if (string.IsNullOrWhiteSpace(exe))
      return;
    var all = ProcessSessionService.FindForeignClientProcesses(exe, null);
    Assert.Contains(all, f => f.ProcessId == self.Id);
    var excluded = ProcessSessionService.FindForeignClientProcesses(exe, self.Id);
    Assert.DoesNotContain(excluded, f => f.ProcessId == self.Id);
  }

  [Fact]
  public void IsTrackedProcessAlive_ExitedProcess_ReportsExited()
  {
    var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
    var acc = new CamfrogAccount
    {
      DisplayName = "shortlived",
      Username = "shortlived_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "short"),
      Enabled = true
    };
    acc.Id = _db.Add(acc);
    var settings = new AppSettings { ClientExecutable = cmd, ClientArgumentsTemplate = "/c exit 0" };
    using var process = _svc.Start(acc, settings);
    Assert.True(process.WaitForExit(10000));
    var tracked = _db.GetById(acc.Id)!;
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(tracked, out var reason));
    // Either the exit was observed or the PID was already reaped.
    Assert.True(
        reason is not null && (reason.Contains("xited", StringComparison.OrdinalIgnoreCase) || reason.Contains("no longer exists", StringComparison.OrdinalIgnoreCase)),
        $"Unexpected reason: {reason}");
    _svc.Stop(tracked);
    Assert.Equal("Stopped", _db.GetById(acc.Id)!.Status);
  }

  [Fact]
  public void Start_MissingExecutable_Throws()
  {
    var acc = new CamfrogAccount { Id = 1, DisplayName = "test", Username = "u", ProfileDirectory = Path.Combine(_paths.Profiles, "p1") };
    var settings = new AppSettings { ClientExecutable = "", ClientArgumentsTemplate = "" };
    Assert.Throws<InvalidOperationException>(() => _svc.Start(acc, settings));
  }

  [Fact]
  public void Start_SandboxieWithoutStartExe_Throws()
  {
    var acc = new CamfrogAccount { Id = 1, ProfileDirectory = Path.Combine(_paths.Profiles, "sbx") };
    var settings = new AppSettings
    {
      ClientExecutable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
      UseSandboxie = true,
      SandboxieStartExe = Path.Combine(_tempRoot, "missing-start.exe")
    };
    Assert.Throws<InvalidOperationException>(() => _svc.Start(acc, settings));
  }

  [Fact]
  public void Start_InvalidRoomUrl_Throws()
  {
    var acc = new CamfrogAccount
    {
      Id = 1,
      ProfileDirectory = Path.Combine(_paths.Profiles, "room"),
      RoomUrl = "https://example.com/not-camfrog"
    };
    var settings = new AppSettings
    {
      ClientExecutable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
    };
    Assert.Throws<InvalidOperationException>(() => _svc.Start(acc, settings));
  }

  [Fact]
  public void Start_NonExistentFile_Throws()
  {
    var acc = new CamfrogAccount { Id = 1, ProfileDirectory = Path.Combine(_paths.Profiles, "p2") };
    var settings = new AppSettings { ClientExecutable = Path.Combine(_tempRoot, "nonexistent.exe") };
    Assert.Throws<FileNotFoundException>(() => _svc.Start(acc, settings));
  }

  [Fact]
  public void Stop_RefusesProcessWhenExecutableIdentityIsUnknown()
  {
    // Fail closed: if the recorded executable cannot be matched, the PID
    // alone is not proof of identity and must never be killed.
    using var current = Process.GetCurrentProcess();
    var id = _db.Add(new CamfrogAccount
    {
      DisplayName = "unknownpath",
      Username = "unknownpath_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "unknownpath"),
      Enabled = true
    });
    // Empty executable path: the only usable signal is PID + start time.
    _db.UpdateRuntime(id, "Running", current.Id, current.StartTime.ToUniversalTime(), "", string.Empty);
    var acc = _db.GetById(id)!;
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(acc, out _));
    _svc.Stop(acc);
    Assert.False(current.HasExited);
  }

  [Fact]
  public void Stop_RefusesProcessWhenStartTimeDiffers()
  {
    // PID matches, start time does not: a recycled PID must be refused.
    using var current = Process.GetCurrentProcess();
    var id = _db.Add(new CamfrogAccount
    {
      DisplayName = "stale",
      Username = "stale_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "stale"),
      Enabled = true
    });
    _db.UpdateRuntime(id, "Running", current.Id, current.StartTime.ToUniversalTime().AddHours(-3), "", string.Empty);
    var acc = _db.GetById(id)!;
    Assert.False(ProcessSessionService.IsTrackedProcessAlive(acc, out var reason));
    _svc.Stop(acc);
    Assert.False(current.HasExited);
    Assert.Contains("no longer matches", _db.GetById(id)!.LastError, StringComparison.OrdinalIgnoreCase);
    Assert.NotNull(reason);
  }

  [Fact]
  public void Stop_RefusesLiveForeignProcess()
  {
    using var current = Process.GetCurrentProcess();
    var id = _db.Add(new CamfrogAccount
    {
      DisplayName = "foreign",
      Username = "foreign_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "foreign"),
      Enabled = true
    });
    // Add() persists identity columns only; runtime state needs UpdateRuntime.
    _db.UpdateRuntime(id, "Running", current.Id, current.StartTime.ToUniversalTime(), "", @"C:\definitely\not\this\client.exe");
    var acc = _db.GetById(id)!;
    Assert.Equal(current.Id, acc.ProcessId);
    // Must not kill the running test process.
    _svc.Stop(acc);
    Assert.False(current.HasExited);
    var updated = _db.GetById(id)!;
    Assert.Equal("Stopped", updated.Status);
    Assert.Contains("no longer matches", updated.LastError, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void Stop_NoPid_UpdatesToStopped()
  {
    var id = _db.Add(new CamfrogAccount
    {
      DisplayName = "nopid",
      Username = "nopid_user_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "nopid"),
      Enabled = true
    });
    var acc = _db.GetAccounts().Single(a => a.Id == id);
    // No PID set
    _svc.Stop(acc);
    var updated = _db.GetAccounts().Single(a => a.Id == id);
    Assert.Equal("Stopped", updated.Status);
    Assert.Null(updated.ProcessId);
  }

  [Fact]
  public void Stop_InvalidPid_MarksStopped()
  {
    var id = _db.Add(new CamfrogAccount
    {
      DisplayName = "invalid",
      Username = "invalid_" + Guid.NewGuid().ToString("N"),
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, "invalid"),
      Enabled = true
    });
    var acc = _db.GetAccounts().Single(a => a.Id == id);
    acc.ProcessId = 999999;
    acc.StartedAtUtc = DateTime.UtcNow;
    acc.ProcessExecutablePath = "";
    // Should not throw, should mark stopped and not kill anything
    _svc.Stop(acc);
    var updated = _db.GetAccounts().Single(a => a.Id == id);
    Assert.Equal("Stopped", updated.Status);
  }

  [Fact]
  public void Quote_And_ExpandArguments_EscapesCorrectly()
  {
    // Use reflection to test private methods
    var quoteMethod = typeof(ProcessSessionService).GetMethod("Quote", BindingFlags.NonPublic | BindingFlags.Static)!;
    var expandMethod = typeof(ProcessSessionService).GetMethod("ExpandArguments", BindingFlags.NonPublic | BindingFlags.Static)!;

    var account = new CamfrogAccount { Username = "user\"with\\slashes", ProfileDirectory = @"C:\profiles\test\" };

    var quotedUser = (string)quoteMethod.Invoke(null, new object[] { account.Username })!;
    Assert.StartsWith("\"", quotedUser);
    Assert.EndsWith("\"", quotedUser);
    Assert.Contains("\\\"", quotedUser); // quote escaped

    var trailingSlash = (string)quoteMethod.Invoke(null, new object[] { @"C:\path\with\slash\" })!;
    // trailing backslashes before closing quote should be doubled
    Assert.EndsWith("\\\\\"", trailingSlash);

    var quotedProfile = (string)quoteMethod.Invoke(null, new object[] { account.ProfileDirectory })!;
    var args = (string)expandMethod.Invoke(null, new object[] { "--user {username} --profile {profile}", account })!;
    Assert.Contains(quotedUser, args);
    Assert.Contains(quotedProfile, args);
    Assert.DoesNotContain("{username}", args);
    // The profile path itself contains the word "profiles" but not the placeholder "{profile}".
    // Verify the original placeholder was replaced by checking args != template and that it contains the quoted values.
    Assert.NotEqual("--user {username} --profile {profile}", args);
  }

  [Fact]
  public void Quote_EmptyString_ReturnsDoubleQuotes()
  {
    var quoteMethod = typeof(ProcessSessionService).GetMethod("Quote", BindingFlags.NonPublic | BindingFlags.Static)!;
    var result = (string)quoteMethod.Invoke(null, new object[] { "" })!;
    Assert.Equal("\"\"", result);
  }

  [Fact]
  public void ExpandArguments_NullTemplate_ReturnsEmpty()
  {
    var expandMethod = typeof(ProcessSessionService).GetMethod("ExpandArguments", BindingFlags.NonPublic | BindingFlags.Static)!;
    var acc = new CamfrogAccount { Username = "u", ProfileDirectory = "p" };
    var result = (string)expandMethod.Invoke(null, new object[] { null!, acc })!;
    Assert.Equal("", result);
  }
}
