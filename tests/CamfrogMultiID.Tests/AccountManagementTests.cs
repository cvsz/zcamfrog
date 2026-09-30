using System.IO;
using System.Text;
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
    public void NormalizeRoomUrl_RejectsHostileInputs()
    {
        Assert.Equal("CAMFROG://join_room/?name=X", ProcessSessionService.NormalizeRoomUrl("CAMFROG://join_room/?name=X"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("camfrog://join_room/?name=My Room"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("camfrog://join_room/?name=a\rb"));
        // Quotes and backslashes are accepted: the canonical Quote() escapes
        // them inside the argument, verified via the preview.
        var tricky = ProcessSessionService.NormalizeRoomUrl("camfrog://join_room/?name=a\"b\\c");
        var preview = ProcessSessionService.PreviewCommandLine(
            @"C:\c.exe", "", new CamfrogAccount { Username = "u", ProfileDirectory = "p", RoomUrl = tricky });
        Assert.Contains("\\\"", preview, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("camfrog://x/" + new string('a', 600)));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("shell:open"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("powershell:-c"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("cmd:/c"));
        Assert.Throws<InvalidOperationException>(() => ProcessSessionService.NormalizeRoomUrl("file:///c:/x"));
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
    Assert.Equal("User1_2", ProcessSessionService.SanitizeBoxName(acc));
    var empty = new CamfrogAccount { Id = 7, Username = "---" };
    Assert.Equal("account7", ProcessSessionService.SanitizeBoxName(empty));
  }

  [Fact]
  public void SanitizeBoxName_PreservesUnderscores()
  {
    // Regression: nicknames like "_oIo_" must keep their underscores
    // (Sandboxie engine allows them); stripping caused mismatches.
    var acc = new CamfrogAccount { Id = 9, Username = "   _oIo_    ".Trim() };
    Assert.Equal("_oIo_", ProcessSessionService.SanitizeBoxName(acc));
    var longName = new CamfrogAccount { Id = 9, Username = new string('a', 20) + "_tail_end_here" };
    var sanitized = ProcessSessionService.SanitizeBoxName(longName);
    Assert.Equal(32, sanitized.Length);
    Assert.DoesNotContain(" ", sanitized, StringComparison.Ordinal);
  }

  [Fact]
  public void SetRoomUrl_EnforcesCanonicalScheme()
  {
    var id = _db.Add(NewAccount("roomcanon"));
    Assert.Throws<InvalidOperationException>(() => _db.SetRoomUrl(id, "https://example.com/r"));
    Assert.Throws<InvalidOperationException>(() => _db.SetRoomUrl(id, "javascript:alert(1)"));
    _db.SetRoomUrl(id, "");
    Assert.Equal(string.Empty, _db.GetById(id)!.RoomUrl);
    _db.SetRoomUrl(id, "  camfrog://join_room/?name=X  ");
    Assert.Equal("camfrog://join_room/?name=X", _db.GetById(id)!.RoomUrl);
  }

  [Fact]
  public void SecretsAcl_RestrictedOnWindows()
  {
    Assert.True(_paths.SecretsAclRestricted);
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

  private static readonly string[] SampleBoxes = ["BoxA", "BoxB", "boxa", "  "];
  private static readonly string[] NoBoxes = [];

  [Fact]
  public void BuildElevatedCreateBoxesCommand_EncodesBoxes()
  {
    var (file, args) = ProcessSessionService.BuildElevatedCreateBoxesCommand(
        @"C:\sb\Start.exe", SampleBoxes);
    Assert.Equal("powershell.exe", file);
    Assert.Contains("-EncodedCommand", args, StringComparison.Ordinal);
    var encoded = args.Substring(args.IndexOf("-EncodedCommand", StringComparison.Ordinal) + "-EncodedCommand".Length).Trim();
    var script = Encoding.Unicode.GetString(Convert.FromBase64String(encoded));
    Assert.Contains("SbieIni.exe", script, StringComparison.Ordinal);
    Assert.Contains("set BoxA Enabled y", script, StringComparison.Ordinal);
    Assert.Contains("set BoxB Enabled y", script, StringComparison.Ordinal);
    Assert.DoesNotContain("runas", script, StringComparison.OrdinalIgnoreCase);
    Assert.Throws<ArgumentException>(() => ProcessSessionService.BuildElevatedCreateBoxesCommand("", SampleBoxes));
    Assert.Throws<ArgumentException>(() => ProcessSessionService.BuildElevatedCreateBoxesCommand(@"C:\sb\Start.exe", NoBoxes));
  }

  [Fact]
  public void ParseRoomName_ExtractsName()
  {
    Assert.Equal("ZeaZDev", ProcessSessionService.ParseRoomName("camfrog://join_room/?name=ZeaZDev"));
    Assert.Equal("My Room", ProcessSessionService.ParseRoomName("camfrog://join_room/?name=My+Room"));
    Assert.Equal(string.Empty, ProcessSessionService.ParseRoomName("camfrog://join_room/"));
    Assert.Equal(string.Empty, ProcessSessionService.ParseRoomName(""));
    Assert.Equal(string.Empty, ProcessSessionService.ParseRoomName(null));
  }

  [Fact]
  public void ResolveRuntimeStates_MatrixIsEvidenceBased()
  {
    const string room = "camfrog://join_room/?name=R";
    static CamfrogAccount Acc(long id, int? pid, string roomUrl = "") =>
        new() { Id = id, ProcessId = pid, RoomUrl = roomUrl, Username = "u" + id };
    static ProcessSessionService.ClientProcessInfo Client(int pid, int ppid, string cmd = "", string title = "") =>
        new(pid, ppid, cmd, DateTime.UtcNow, title);

    // Evidence unavailable -> every account Unknown, never "Offline".
    var noEvidence = ProcessSessionService.ResolveRuntimeStates(
        new[] { Acc(1, 100), Acc(2, null) }, null, new HashSet<int> { 100 });
    Assert.False(noEvidence.EvidenceAvailable);
    Assert.All(noEvidence.Accounts, v => Assert.Equal(AccountRuntimeState.Unknown, v.State));

    var accounts = new[]
    {
        Acc(1, null),                       // never started
        Acc(2, 200, room),                 // wrapper alive, client not yet spawned
        Acc(3, 300),                       // client alive, no window
        Acc(4, 400),                       // client alive with window, no room
        Acc(5, 500, room),                 // client carries the room link
        Acc(6, 600, room),                 // client alive but link absent
        Acc(7, 700, room),                 // client outlived the wrapper
        Acc(8, 800),                       // wrapper recorded but gone, no client
    };
    var clients = new List<ProcessSessionService.ClientProcessInfo>
    {
        Client(301, 300),                                    // 3: no window
        Client(401, 400, title: "Camfrog"),                  // 4: window
        Client(501, 500, $"client.exe --url=\"{room}\""),    // 5: link delivered
        Client(601, 600, title: "Camfrog"),                  // 6: window but no link
        Client(701, 700, title: "Camfrog"),                  // 7: wrapper dead
    };
    var live = new HashSet<int> { 100, 200, 300, 400, 500, 600 };
    var snapshot = new ProcessSessionService.ClientProcessSnapshot(clients, BuildParents(clients));

    var report = ProcessSessionService.ResolveRuntimeStates(accounts, snapshot, live);
    Assert.True(report.EvidenceAvailable);
    var byId = report.Accounts.ToDictionary(v => v.AccountId);
    Assert.Equal(AccountRuntimeState.Offline, byId[1].State);
    Assert.Equal(AccountRuntimeState.Starting, byId[2].State);
    Assert.Equal(AccountRuntimeState.AwaitingLogin, byId[3].State);
    Assert.Equal(AccountRuntimeState.Idle, byId[4].State);
    Assert.Equal(AccountRuntimeState.RoomRequested, byId[5].State);
    Assert.Equal("R", byId[5].RoomName);
    Assert.Equal(AccountRuntimeState.RoomNotObserved, byId[6].State);
    Assert.Equal(AccountRuntimeState.Orphaned, byId[7].State);
    Assert.Equal(AccountRuntimeState.Offline, byId[8].State);
    Assert.Empty(report.UntrackedClients);
  }

  private static Dictionary<int, int> BuildParents(IEnumerable<ProcessSessionService.ClientProcessInfo> clients)
  {
    var map = new Dictionary<int, int>();
    foreach (var c in clients)
      map[c.ProcessId] = c.ParentProcessId;
    return map;
  }

  [Fact]
  public void ResolveRuntimeStates_SameRoomAccountsDoNotStealEachOthersClient()
  {
    // The old logic matched any live client command line against the room
    // URL, so two accounts in one room both looked joined. Attribution must
    // follow the tracked wrapper PID instead.
    const string room = "camfrog://join_room/?name=Shared";
    var accounts = new[]
    {
        new CamfrogAccount { Id = 1, ProcessId = 100, RoomUrl = room },
        new CamfrogAccount { Id = 2, ProcessId = 200, RoomUrl = room },
    };
    var clients = new List<ProcessSessionService.ClientProcessInfo>
    {
        new(901, 200, $"client.exe --url=\"{room}\"", DateTime.UtcNow, "Camfrog"),
    };
    var snapshot = new ProcessSessionService.ClientProcessSnapshot(clients, BuildParents(clients));
    var report = ProcessSessionService.ResolveRuntimeStates(accounts, snapshot, new HashSet<int> { 100, 200 });
    var byId = report.Accounts.ToDictionary(v => v.AccountId);
    Assert.Equal(AccountRuntimeState.RoomRequested, byId[2].State);
    Assert.Equal(AccountRuntimeState.Starting, byId[1].State);
    Assert.Equal(new List<int> { 901 }, byId[2].ClientProcessIds);
    Assert.Empty(byId[1].ClientProcessIds);
  }

  [Fact]
  public void ResolveRuntimeStates_UntrackedClientIsReportedNotAssigned()
  {
    var accounts = new[] { new CamfrogAccount { Id = 1, ProcessId = null } };
    var clients = new List<ProcessSessionService.ClientProcessInfo>
    {
        new(555, 1, "client.exe", DateTime.UtcNow, "Camfrog"), // parent is not a tracked PID
    };
    var snapshot = new ProcessSessionService.ClientProcessSnapshot(clients, BuildParents(clients));
    var report = ProcessSessionService.ResolveRuntimeStates(accounts, snapshot, new HashSet<int>());
    Assert.Equal(AccountRuntimeState.Offline, report.Accounts[0].State);
    Assert.Equal(555, Assert.Single(report.UntrackedClients).ProcessId);
  }

  [Fact]
  public void ResolveRuntimeStates_AncestorChainIsWalked()
  {
    // Wrapper -> helper -> client: the client is two hops down.
    var accounts = new[] { new CamfrogAccount { Id = 1, ProcessId = 100 } };
    var clients = new List<ProcessSessionService.ClientProcessInfo>
    {
        new(300, 200, string.Empty, DateTime.UtcNow, "Camfrog"),
    };
    // PID 200 is an intermediate helper, not a client: it is absent from the
    // client list and only reachable through the parent map.
    var parents = BuildParents(clients);
    parents[200] = 100;
    var snapshot = new ProcessSessionService.ClientProcessSnapshot(clients, parents);
    var report = ProcessSessionService.ResolveRuntimeStates(accounts, snapshot, new HashSet<int> { 100 });
    Assert.Equal(AccountRuntimeState.Idle, report.Accounts[0].State);
    Assert.Equal(300, Assert.Single(report.Accounts[0].ClientProcessIds));
  }

  [Fact]
  public void GetClientProcessSnapshot_UnsetExecutable_ReturnsNull()
  {
    Assert.Null(ProcessSessionService.GetClientProcessSnapshot(string.Empty));
    Assert.Null(ProcessSessionService.GetClientProcessSnapshot("   "));
  }

  [Fact]
  public void BuildTerminateArguments_FormatsTerminate()
  {
    Assert.Equal("/Box:MyBox /terminate", ProcessSessionService.BuildTerminateArguments("MyBox"));
    Assert.Equal("/Box:X /terminate", ProcessSessionService.BuildTerminateArguments("  X  "));
    Assert.Throws<ArgumentException>(() => ProcessSessionService.BuildTerminateArguments("   "));
  }

  [Fact]
  public void IsSandboxedExecutable_DetectsStartExe()
  {
    Assert.True(ProcessSessionService.IsSandboxedExecutable(@"C:\Program Files\Sandboxie-Plus\Start.exe"));
    Assert.True(ProcessSessionService.IsSandboxedExecutable("start.EXE"));
    Assert.False(ProcessSessionService.IsSandboxedExecutable(@"C:\Camfrog\Camfrog.exe"));
    Assert.False(ProcessSessionService.IsSandboxedExecutable(""));
    Assert.False(ProcessSessionService.IsSandboxedExecutable(null));
  }

  [Fact]
  public void BuildSbieIniSetArguments_FormatsSetCommand()
  {
    Assert.Equal("set MyBox Enabled y", ProcessSessionService.BuildSbieIniSetArguments("MyBox"));
    Assert.Equal("set X Enabled y", ProcessSessionService.BuildSbieIniSetArguments("  X  "));
    Assert.Throws<ArgumentException>(() => ProcessSessionService.BuildSbieIniSetArguments("   "));
  }

  [Fact]
  public void FindSbieIni_SitsNextToStartExe()
  {
    Assert.Equal(
        Path.Combine("C:", "sb", "SbieIni.exe"),
        ProcessSessionService.FindSbieIni(Path.Combine("C:", "sb", "Start.exe")));
    Assert.Throws<ArgumentException>(() => ProcessSessionService.FindSbieIni("   "));
  }

  [Fact]
  public void PreviewLaunch_ShowsUnquotedBox()
  {
    var acc = new CamfrogAccount { Id = 1, Username = "u1", ProfileDirectory = @"C:\p\1" };
    var cmd = ProcessSessionService.PreviewLaunch(
        new AppSettings { ClientExecutable = @"C:\c.exe", UseSandboxie = true, SandboxieStartExe = @"C:\sb\Start.exe" }, acc);
    Assert.Contains("/Box:u1 ", cmd);
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
  public void RestartPolicy_BudgetSurvivesSuccessfulStarts()
  {
    // Regression for the auto-restart loop: the UI resets the budget only
    // on manual start/stop. Simulates exit -> allow -> start ok, repeated,
    // with no Reset in between (the automatic path).
    var policy = new RestartPolicy();
    var now = DateTime.UtcNow;
    for (var i = 0; i < 3; i++)
    {
      Assert.True(policy.ShouldRestart(99, now.AddSeconds(i * 2)));
    }
    Assert.False(policy.ShouldRestart(99, now.AddSeconds(8)));
  }

    [Fact]
    public void RestartPolicy_ToleratesClockSkew()
    {
        var policy = new RestartPolicy();
        var now = DateTime.UtcNow;
        Assert.True(policy.ShouldRestart(30, now));
        // Querying with an earlier clock still sees the attempt in-window.
        Assert.True(policy.ShouldRestart(30, now.AddHours(-1)));
        Assert.True(policy.ShouldRestart(30, now));
        Assert.False(policy.ShouldRestart(30, now));
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
