using System.IO;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class AccountManagementTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly DatabaseService _db;
    private readonly CredentialService _creds;

    public AccountManagementTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-mgmt-" + Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_tempRoot);
        _db = new DatabaseService(_paths);
        _db.Initialize();
        _creds = new CredentialService(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    [Fact]
    public void GetById_ReturnsAccount_OrNull()
    {
        var id = _db.Add(NewAccount("getbyid"));
        var found = _db.GetById(id);
        Assert.NotNull(found);
        Assert.Equal("getbyid", found.Username);
        Assert.Null(_db.GetById(id + 9999));
    }

    [Fact]
    public void UpdateDetails_ChangesDisplayUsernameEnabled()
    {
        var id = _db.Add(NewAccount("before"));
        _db.UpdateDetails(id, "After Display", "after_user", false);
        var updated = _db.GetById(id)!;
        Assert.Equal("After Display", updated.DisplayName);
        Assert.Equal("after_user", updated.Username);
        Assert.False(updated.Enabled);
    }

    [Fact]
    public void UpdateDetails_DuplicateUsername_Throws()
    {
        _db.Add(NewAccount("alpha"));
        var id2 = _db.Add(NewAccount("beta"));
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _db.UpdateDetails(id2, "Beta", "ALPHA", true));
    }

    [Fact]
    public void UpdateDetails_MissingId_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _db.UpdateDetails(999999, "x", "y", true));
    }

    [Fact]
    public void UsernameExistsExcept_ExcludesSelf()
    {
        var id = _db.Add(NewAccount("selfuser"));
        Assert.False(_db.UsernameExistsExcept("selfuser", id));
        Assert.False(_db.UsernameExistsExcept("SELFUSER", id));
        _db.Add(NewAccount("other"));
        Assert.True(_db.UsernameExistsExcept("other", id));
    }

    [Fact]
    public void SetEnabled_TogglesFlag()
    {
        var id = _db.Add(NewAccount("toggle"));
        _db.SetEnabled(id, false);
        Assert.False(_db.GetById(id)!.Enabled);
        _db.SetEnabled(id, true);
        Assert.True(_db.GetById(id)!.Enabled);
    }

    [Fact]
    public void Delete_RemovesRow()
    {
        var id = _db.Add(NewAccount("todelete"));
        Assert.True(_db.Delete(id));
        Assert.Null(_db.GetById(id));
        Assert.False(_db.Delete(id));
        Assert.Empty(_db.GetAccounts());
    }

    [Fact]
    public void ClearError_ResetsErrorAndStatus()
    {
        var id = _db.Add(NewAccount("err"));
        _db.UpdateRuntime(id, "Error", null, null, "boom");
        _db.ClearError(id);
        var acc = _db.GetById(id)!;
        Assert.Equal(string.Empty, acc.LastError);
        Assert.Equal("Stopped", acc.Status);
    }

    [Fact]
    public void Credential_Delete_RemovesFile()
    {
        _creds.Save("todel", "pwd");
        Assert.True(_creds.Exists("todel"));
        Assert.True(_creds.Delete("todel"));
        Assert.False(_creds.Exists("todel"));
        Assert.False(_creds.Delete("todel"));
        Assert.False(_creds.Exists("   "));
    }

    [Fact]
    public void PreviewCommandLine_ExpandsPlaceholders()
    {
        var acc = new CamfrogAccount { Username = "u1", ProfileDirectory = @"C:\p\1" };
        var cmd = ProcessSessionService.PreviewCommandLine(@"C:\Camfrog\Camfrog.exe", "--user {username} --profile {profile}", acc);
        Assert.Contains("Camfrog.exe", cmd);
        Assert.DoesNotContain("{username}", cmd);
        Assert.Contains("\"u1\"", cmd);
    }

    [Fact]
    public void PreviewCommandLine_EmptyExe_UsesPlaceholder()
    {
        var acc = new CamfrogAccount { Username = "u", ProfileDirectory = "p" };
        var cmd = ProcessSessionService.PreviewCommandLine("", "", acc);
        Assert.Contains("client-exe", cmd);
    }

    [Fact]
    public void ValidateTemplate_DetectsUnsupported()
    {
        Assert.Empty(ProcessSessionService.ValidateArgumentsTemplate(""));
        Assert.Empty(ProcessSessionService.ValidateArgumentsTemplate("--user {username} --profile {profile}"));
        var warnings = ProcessSessionService.ValidateArgumentsTemplate("--login {user} --pass {password}");
        Assert.NotEmpty(warnings);
        var unclosed = ProcessSessionService.ValidateArgumentsTemplate("--user {username");
        Assert.NotEmpty(unclosed);
    }

    [Fact]
    public void NormalizeRoomUrl_AcceptsCamfrogScheme()
    {
        Assert.Equal("camfrog://room/Test", ProcessSessionService.NormalizeRoomUrl("  camfrog://room/Test  "));
        Assert.Throws<ArgumentException>(() => ProcessSessionService.NormalizeRoomUrl("   "));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("https://example.com/room"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("camfrog://room/a b"));
    }

    [Fact]
    public void SanitizeBoxName_KeepsAlphanumerics()
    {
        var acc = new CamfrogAccount { Id = 7, Username = "User-1_2!" };
        Assert.Equal("User12", ProcessSessionService.SanitizeBoxName(acc));
        var empty = new CamfrogAccount { Id = 7, Username = "---" };
        Assert.Equal("account7", ProcessSessionService.SanitizeBoxName(empty));
    }

    [Fact]
    public void SetRoomUrl_Persists()
    {
        var id = _db.Add(NewAccount("roomuser"));
        _db.SetRoomUrl(id, "camfrog://room/Test");
        Assert.Equal("camfrog://room/Test", _db.GetById(id)!.RoomUrl);
        Assert.Throws<InvalidOperationException>(() => _db.SetRoomUrl(id + 9999, "camfrog://room/X"));
    }

    [Fact]
    public void Add_PersistsRoomUrl()
    {
        var acc = NewAccount("roomadd");
        acc.RoomUrl = "camfrog://room/Add";
        var id = _db.Add(acc);
        Assert.Equal("camfrog://room/Add", _db.GetById(id)!.RoomUrl);
    }

    [Fact]
    public void BoxExists_UsesOverrideRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "zcamfrog-boxtest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "SomeBox"));
        var previous = Environment.GetEnvironmentVariable("CAMFROGMULTIID_SANDBOX_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("CAMFROGMULTIID_SANDBOX_ROOT", root);
            Assert.True(ProcessSessionService.BoxExists("SomeBox"));
            Assert.False(ProcessSessionService.BoxExists("Missing"));
            Assert.False(ProcessSessionService.BoxExists(""));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CAMFROGMULTIID_SANDBOX_ROOT", previous);
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void GetSandboxieVersion_ReadsRealFile()
    {
        var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        Assert.NotNull(ProcessSessionService.GetSandboxieVersion(cmd));
        Assert.Null(ProcessSessionService.GetSandboxieVersion(Path.Combine(_tempRoot, "missing.exe")));
        Assert.Null(ProcessSessionService.GetSandboxieVersion(""));
        Assert.Null(ProcessSessionService.GetSandboxieVersion(null));
    }

    [Fact]
    public void BuildCreateBoxArguments_QuotesBox()
    {
        var args = ProcessSessionService.BuildCreateBoxArguments("My Box");
        Assert.Contains("/Box:", args);
        Assert.Contains("cmd.exe", args);
    }

    [Fact]
    public void SetAutoRestart_Persists()
    {
        var id = _db.Add(NewAccount("autorestart"));
        Assert.False(_db.GetById(id)!.AutoRestart);
        _db.SetAutoRestart(id, true);
        Assert.True(_db.GetById(id)!.AutoRestart);
        _db.SetAutoRestart(id, false);
        Assert.False(_db.GetById(id)!.AutoRestart);
        Assert.Throws<InvalidOperationException>(() => _db.SetAutoRestart(id + 9999, true));
    }

    [Fact]
    public void RestartPolicy_AllowsThreeThenBlocks()
    {
        var policy = new RestartPolicy();
        var now = DateTime.UtcNow;
        Assert.True(policy.ShouldRestart(1, now));
        Assert.True(policy.ShouldRestart(1, now.AddMinutes(1)));
        Assert.True(policy.ShouldRestart(1, now.AddMinutes(2)));
        Assert.False(policy.ShouldRestart(1, now.AddMinutes(3)));
        policy.Reset(1);
        Assert.True(policy.ShouldRestart(1, now.AddMinutes(4)));
    }

    [Fact]
    public void RestartPolicy_WindowExpiryFreesBudget()
    {
        var policy = new RestartPolicy();
        var now = DateTime.UtcNow;
        Assert.True(policy.ShouldRestart(2, now));
        Assert.True(policy.ShouldRestart(2, now));
        Assert.True(policy.ShouldRestart(2, now));
        Assert.False(policy.ShouldRestart(2, now));
        Assert.True(policy.ShouldRestart(2, now.AddMinutes(11)));
    }

    [Fact]
    public void RestartPolicy_CountReflectsWindow()
    {
        var policy = new RestartPolicy();
        var now = DateTime.UtcNow;
        Assert.Equal(0, policy.GetAttemptCount(20, now));
        policy.ShouldRestart(20, now);
        policy.ShouldRestart(20, now);
        Assert.Equal(2, policy.GetAttemptCount(20, now.AddMinutes(1)));
        Assert.Equal(0, policy.GetAttemptCount(20, now.AddMinutes(11)));
    }

    [Fact]
    public void SetPasswordChanged_Persists()
    {
        var id = _db.Add(NewAccount("pwdage"));
        Assert.Null(_db.GetById(id)!.PasswordChangedUtc);
        var stamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        _db.SetPasswordChanged(id, stamp);
        Assert.Equal(stamp, _db.GetById(id)!.PasswordChangedUtc);
        Assert.Throws<InvalidOperationException>(() => _db.SetPasswordChanged(id + 9999, stamp));
    }

    [Fact]
    public void RestartPolicy_TracksAccountsSeparately()
    {
        var policy = new RestartPolicy();
        var now = DateTime.UtcNow;
        Assert.True(policy.ShouldRestart(10, now));
        Assert.True(policy.ShouldRestart(11, now));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.ShouldRestart(0, now));
    }

    [Fact]
    public void PreviewLaunch_IncludesRoomAndSandbox()
    {
        var acc = new CamfrogAccount { Id = 1, Username = "u1", ProfileDirectory = @"C:\p\1", RoomUrl = "camfrog://room/R" };
        var plain = ProcessSessionService.PreviewLaunch(new AppSettings { ClientExecutable = @"C:\c.exe" }, acc);
        Assert.Contains("--url=", plain);
        Assert.Contains("camfrog://room/R", plain);
        var boxed = ProcessSessionService.PreviewLaunch(new AppSettings { ClientExecutable = @"C:\c.exe", UseSandboxie = true, SandboxieStartExe = @"C:\sb\Start.exe" }, acc);
        Assert.Contains("/wait", boxed);
        Assert.Contains("/Box:", boxed);
        Assert.Contains("Start.exe", boxed);
    }

    [Fact]
    public void Migration_OldDatabaseWithoutRoomUrl_StillReads()
    {
        var dbPath = Path.Combine(_paths.Root, "migtest.db");
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE accounts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    display_name TEXT NOT NULL,
                    username TEXT NOT NULL,
                    secret_name TEXT NOT NULL UNIQUE,
                    profile_directory TEXT NOT NULL,
                    enabled INTEGER NOT NULL DEFAULT 1,
                    status TEXT NOT NULL DEFAULT 'Stopped',
                    process_id INTEGER,
                    started_utc TEXT,
                    process_executable_path TEXT NOT NULL DEFAULT '',
                    last_error TEXT NOT NULL DEFAULT ''
                );
                INSERT INTO accounts (display_name,username,secret_name,profile_directory,enabled)
                VALUES('Old','olduser','s1','p1',1);
                """;
            command.ExecuteNonQuery();
        }
        // Point a DatabaseService at this legacy file via a dedicated AppPaths root.
        var legacyRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(legacyRoot);
        try
        {
            File.Copy(dbPath, Path.Combine(legacyRoot, "camfrog.db"));
            var legacyDb = new DatabaseService(new AppPaths(legacyRoot));
            legacyDb.Initialize();
            var acc = legacyDb.GetAccounts().Single();
            Assert.Equal("olduser", acc.Username);
            Assert.Equal(string.Empty, acc.RoomUrl);
        }
        finally
        {
            try { Directory.Delete(legacyRoot, true); } catch { }
        }
    }

    private CamfrogAccount NewAccount(string username)
    {
        return new CamfrogAccount
        {
            DisplayName = username,
            Username = username,
            PasswordSecretName = "secret_" + Guid.NewGuid().ToString("N"),
            ProfileDirectory = Path.Combine(_paths.Profiles, Guid.NewGuid().ToString("N")),
            Enabled = true
        };
    }
}
