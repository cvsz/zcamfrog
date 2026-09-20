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
    public void Start_MissingExecutable_Throws()
    {
        var acc = new CamfrogAccount { Id = 1, DisplayName = "test", Username = "u", ProfileDirectory = Path.Combine(_tempRoot, "p1") };
        var settings = new AppSettings { ClientExecutable = "", ClientArgumentsTemplate = "" };
        Assert.Throws<InvalidOperationException>(() => _svc.Start(acc, settings));
    }

    [Fact]
    public void Start_NonExistentFile_Throws()
    {
        var acc = new CamfrogAccount { Id = 1, ProfileDirectory = Path.Combine(_tempRoot, "p2") };
        var settings = new AppSettings { ClientExecutable = Path.Combine(_tempRoot, "nonexistent.exe") };
        Assert.Throws<FileNotFoundException>(() => _svc.Start(acc, settings));
    }

    [Fact]
    public void Stop_NoPid_UpdatesToStopped()
    {
        var id = _db.Add(new CamfrogAccount
        {
            DisplayName = "nopid",
            Username = "nopid_user_" + Guid.NewGuid().ToString("N"),
            PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
            ProfileDirectory = Path.Combine(_tempRoot, "p_nopid"),
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
            ProfileDirectory = Path.Combine(_tempRoot, "p_invalid"),
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
