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
