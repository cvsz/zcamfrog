using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class InfrastructureTests
{
    [Fact]
    public void Database_EnforcesCaseInsensitiveUsernameUniqueness()
    {
        using var fixture = new TestFixture();
        var first = CreateAccount("Sea");
        var second = CreateAccount("sea");

        fixture.Database.Add(first);

        Assert.True(fixture.Database.UsernameExists("SEA"));
        Assert.ThrowsAny<Exception>(() => fixture.Database.Add(second));
    }

    [Fact]
    public void Credentials_RoundTripAndRejectPathTraversal()
    {
        using var fixture = new TestFixture();

        fixture.Credentials.Save("account_test", "secret-value");
        Assert.Equal("secret-value", fixture.Credentials.Load("account_test"));

        Assert.Throws<ArgumentException>(() => fixture.Credentials.Save("../escape", "x"));
        Assert.Throws<ArgumentException>(() => fixture.Credentials.Load("../escape"));
    }

    [Fact]
    public void Settings_AreNormalizedAndPersisted()
    {
        using var fixture = new TestFixture();
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Test process path unavailable.");

        fixture.Settings.Save(new AppSettings
        {
            ClientExecutable = executable,
            ClientArgumentsTemplate = " --example "
        });

        var loaded = fixture.Settings.Load();

        Assert.Equal(Path.GetFullPath(executable), loaded.ClientExecutable);
        Assert.Equal("--example", loaded.ClientArgumentsTemplate);
        Assert.Equal(fixture.Paths.Root, loaded.DataDirectory);
    }

    [Fact]
    public void Database_DeleteRefusesRunningAccount()
    {
        using var fixture = new TestFixture();
        var account = CreateAccount("running");
        account.Id = fixture.Database.Add(account);
        fixture.Database.UpdateRuntime(account.Id, "Running", Environment.ProcessId, DateTime.UtcNow, "", Environment.ProcessPath ?? "");

        Assert.Throws<InvalidOperationException>(() => fixture.Database.Delete(account.Id));
        Assert.NotNull(fixture.Database.GetAccount(account.Id));
    }

    private static CamfrogAccount CreateAccount(string username) =>
        new()
        {
            DisplayName = username,
            Username = username,
            PasswordSecretName = "account_" + Guid.NewGuid().ToString("N"),
            ProfileDirectory = Path.Combine(Path.GetTempPath(), "CamfrogMultiID-Test", Guid.NewGuid().ToString("N")),
            Enabled = true
        };

    private sealed class TestFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "CamfrogMultiID-Test", Guid.NewGuid().ToString("N"));
        public AppPaths Paths { get; }
        public DatabaseService Database { get; }
        public CredentialService Credentials { get; }
        public SettingsService Settings { get; }

        public TestFixture()
        {
            Paths = new AppPaths(Root);
            Database = new DatabaseService(Paths);
            Database.Initialize();
            Credentials = new CredentialService(Paths);
            Settings = new SettingsService(Paths);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { }
        }
    }
}