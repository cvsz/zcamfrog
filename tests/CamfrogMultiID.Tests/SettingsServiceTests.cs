using System.IO;
using System.Text.Json;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly AppPaths _paths;
    private readonly SettingsService _svc;

    public SettingsServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-settings-" + Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_tempRoot);
        _svc = new SettingsService(_paths);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, true); } catch { }
    }

    [Fact]
    public void Load_Missing_ReturnsDefaults()
    {
        var s = _svc.Load();
        Assert.NotNull(s);
        Assert.Equal(_paths.Root, s.DataDirectory);
        Assert.Equal(string.Empty, s.ClientExecutable);
    }

    [Fact]
    public void Save_And_Load_Roundtrips()
    {
        var settings = new AppSettings
        {
            ClientExecutable = @"C:\Program Files\Camfrog\Camfrog.exe",
            ClientArgumentsTemplate = "{username} {profile}"
        };
        _svc.Save(settings);
        var loaded = _svc.Load();
        Assert.Equal(settings.ClientExecutable, loaded.ClientExecutable);
        Assert.Equal(settings.ClientArgumentsTemplate, loaded.ClientArgumentsTemplate);
        Assert.Equal(_paths.Root, loaded.DataDirectory);
    }

    [Fact]
    public void Save_SetsDataDirectoryToRoot()
    {
        var s = new AppSettings { ClientExecutable = @"C:\fake.exe", DataDirectory = "should-be-overwritten" };
        _svc.Save(s);
        Assert.Equal(_paths.Root, s.DataDirectory);
        var loaded = _svc.Load();
        Assert.Equal(_paths.Root, loaded.DataDirectory);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        File.WriteAllText(_paths.Settings, "not json {{{");
        var s = _svc.Load();
        Assert.Equal(_paths.Root, s.DataDirectory);
        Assert.Equal(string.Empty, s.ClientExecutable);
    }

    [Fact]
    public void Load_EmptyFile_ReturnsDefaults()
    {
        File.WriteAllText(_paths.Settings, "");
        var s = _svc.Load();
        Assert.Equal(_paths.Root, s.DataDirectory);
    }

    [Fact]
    public void Save_AtomicWrite_DoesNotLeaveTemp()
    {
        _svc.Save(new AppSettings { ClientExecutable = @"C:\a.exe" });
        var temps = Directory.GetFiles(_paths.Root, "*.tmp");
        Assert.Empty(temps);
    }

    [Fact]
    public void Save_Twice_Overwrites()
    {
        _svc.Save(new AppSettings { ClientExecutable = @"C:\first.exe" });
        _svc.Save(new AppSettings { ClientExecutable = @"C:\second.exe" });
        Assert.Equal(@"C:\second.exe", _svc.Load().ClientExecutable);
    }

    [Fact]
    public void Save_WritesValidJson()
    {
        _svc.Save(new AppSettings { ClientExecutable = @"C:\x.exe", ClientArgumentsTemplate = "{username}" });
        var json = File.ReadAllText(_paths.Settings);
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("ClientExecutable", out _));
    }
}
