using System.IO;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class DatabaseServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly DatabaseService _db;

    public DatabaseServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-db-" + Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_tempRoot);
        _db = new DatabaseService(_paths);
        _db.Initialize();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    [Fact]
    public void Initialize_CreatesTablesAndIndexes()
    {
        var accounts = _db.GetAccounts();
        Assert.Empty(accounts);
        // second initialize should be idempotent
        _db.Initialize();
        Assert.Empty(_db.GetAccounts());
    }

    [Fact]
    public void Add_And_GetAccounts_PersistsCorrectly()
    {
        var account = NewAccount("UserA");
        var id = _db.Add(account);
        Assert.True(id > 0);
        var list = _db.GetAccounts();
        Assert.Single(list);
        Assert.Equal("UserA", list[0].Username);
        Assert.Equal(account.ProfileDirectory, list[0].ProfileDirectory);
        Assert.Equal("Stopped", list[0].Status);
    }

    [Fact]
    public void UsernameExists_IsCaseInsensitive()
    {
        _db.Add(NewAccount("TestUser"));
        Assert.True(_db.UsernameExists("testuser"));
        Assert.True(_db.UsernameExists("TESTUSER"));
        Assert.True(_db.UsernameExists("TestUser"));
        Assert.False(_db.UsernameExists("other"));
    }

    [Fact]
    public void Add_DuplicateUsername_Fails()
    {
        _db.Add(NewAccount("dup"));
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _db.Add(NewAccount("DUP")));
        Assert.Contains("UNIQUE", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Add_DuplicateSecretName_Fails()
    {
        var a1 = NewAccount("user1", secret: "same-secret");
        var a2 = NewAccount("user2", secret: "same-secret");
        _db.Add(a1);
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _db.Add(a2));
    }

    [Fact]
    public void UpdateRuntime_PersistsState()
    {
        var id = _db.Add(NewAccount("runtimeUser"));
        var now = DateTime.UtcNow;
        _db.UpdateRuntime(id, "Running", 1234, now, "", "C:\\fake\\client.exe");
        var acc = _db.GetAccounts().Single();
        Assert.Equal("Running", acc.Status);
        Assert.Equal(1234, acc.ProcessId);
        Assert.Equal("C:\\fake\\client.exe", acc.ProcessExecutablePath);
        Assert.NotNull(acc.StartedAtUtc);
        // update to stopped
        _db.UpdateRuntime(id, "Stopped", null, null);
        var acc2 = _db.GetAccounts().Single();
        Assert.Equal("Stopped", acc2.Status);
        Assert.Null(acc2.ProcessId);
        Assert.Null(acc2.StartedAtUtc);
    }

    [Fact]
    public void Log_InsertsEventAndFile()
    {
        _db.Log("INFO", "hello world");
        _db.Log("ERROR", "something\r\nwith newline");
        var logFile = Path.Combine(_paths.Logs, "app.log");
        Assert.True(File.Exists(logFile));
        var text = File.ReadAllText(logFile);
        Assert.Contains("hello world", text);
        Assert.Contains("something  with newline", text); // newline sanitized
        // verify no plaintext password in log (manual check: we didn't log password)
    }

    [Fact]
    public void ConcurrentAdd_And_Read_IsSafe()
    {
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(() =>
        {
            var acc = NewAccount($"concurrent{i}_{Guid.NewGuid():N}");
            return _db.Add(acc);
        })).ToArray();
        Task.WaitAll(tasks);
        Assert.Equal(10, _db.GetAccounts().Count);
    }

    [Fact]
    public void GetAccounts_Empty_WhenNoData()
    {
        Assert.Empty(_db.GetAccounts());
    }

    [Fact]
    public void SchemaVersion_MatchesExpected()
    {
        Assert.Equal(DatabaseService.SchemaVersion, _db.GetSchemaVersion());
    }

    [Fact]
    public void UpdateDetails_ValidatesDomain()
    {
        var id = _db.Add(NewAccount("vuser"));
        Assert.Throws<ArgumentException>(() => _db.UpdateDetails(id, "", "x", true));
        Assert.Throws<ArgumentException>(() => _db.UpdateDetails(id, "x", "   ", true));
        Assert.Throws<ArgumentException>(() => _db.UpdateDetails(id, new string('a', 81), "x", true));
        Assert.Throws<ArgumentException>(() => _db.UpdateDetails(id, "ok", "a\tb", true));
        _db.UpdateDetails(id, "  spaced  ", "  spaceduser  ", true);
        var updated = _db.GetById(id)!;
        Assert.Equal("spaced", updated.DisplayName);
        Assert.Equal("spaceduser", updated.Username);
    }

    [Fact]
    public void Add_ValidatesDomain()
    {
        Assert.Throws<ArgumentNullException>(() => _db.Add(null!));
        var bad = NewAccount("x");
        bad.Username = "";
        Assert.Throws<ArgumentException>(() => _db.Add(bad));
    }

    [Fact]
    public void GetRecentEvents_ReturnsNewestFirst()
    {
        _db.Log("INFO", "first");
        _db.Log("WARN", "second");
        var events = _db.GetRecentEvents(10);
        Assert.Equal(2, events.Count);
        Assert.Equal("second", events[0].Message);
        Assert.Equal("WARN", events[0].Level);
        Assert.Equal("first", events[1].Message);
        Assert.Single(_db.GetRecentEvents(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _db.GetRecentEvents(0));
    }

    [Fact]
    public void GetAccounts_CorruptStartedUtc_ReturnsNullInsteadOfThrowing()
    {
        var id = _db.Add(NewAccount("corruptdate"));
        // Corrupt the started_utc value directly, bypassing UpdateRuntime validation.
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={System.IO.Path.Combine(_paths.Root, "camfrog.db")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE accounts SET status='Running', process_id=1234, started_utc='not-a-date' WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();

        var acc = _db.GetAccounts().Single(a => a.Id == id);
        Assert.Null(acc.StartedAtUtc);
        Assert.Equal(1234, acc.ProcessId);
    }

    private CamfrogAccount NewAccount(string username, string? secret = null)
    {
        var s = secret ?? "secret_" + Guid.NewGuid().ToString("N");
        return new CamfrogAccount
        {
            DisplayName = username,
            Username = username,
            PasswordSecretName = s,
            ProfileDirectory = Path.Combine(_paths.Profiles, Guid.NewGuid().ToString("N")),
            Enabled = true
        };
    }
}
