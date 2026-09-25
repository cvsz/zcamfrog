using System.IO;
using System.IO.Compression;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class DiagnosticsServiceTests : IDisposable
{
  private readonly string _tempRoot;
  private readonly AppPaths _paths;
  private readonly DatabaseService _db;

  public DiagnosticsServiceTests()
  {
    _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-diag-" + Guid.NewGuid().ToString("N"));
    _paths = new AppPaths(_tempRoot);
    _db = new DatabaseService(_paths);
    _db.Initialize();
  }

  public void Dispose()
  {
    try { Directory.Delete(_tempRoot, true); } catch { }
  }

  [Fact]
  public void ExportBundle_ContainsExpectedEntriesWithoutUsernames()
  {
    _db.Add(new CamfrogMultiID.Core.CamfrogAccount
    {
      DisplayName = "SecretDisplay",
      Username = "SecretUser_xyz",
      PasswordSecretName = "s_" + Guid.NewGuid().ToString("N"),
      ProfileDirectory = Path.Combine(_paths.Profiles, Guid.NewGuid().ToString("N")),
      Enabled = true
    });
    _db.Log("INFO", "hello");

    var zip = Path.Combine(_tempRoot, "diag.zip");
    DiagnosticsService.ExportBundle(_paths, _db, zip);

    using var archive = ZipFile.OpenRead(zip);
    var names = archive.Entries.Select(e => e.FullName).ToList();
    Assert.Contains("versions.txt", names);
    Assert.Contains("accounts-summary.txt", names);

    foreach (var entry in archive.Entries)
    {
      using var reader = new StreamReader(entry.Open());
      var text = reader.ReadToEnd();
      Assert.DoesNotContain("SecretUser_xyz", text);
      Assert.DoesNotContain("SecretDisplay", text);
    }

    var summary = ReadEntry(archive, "accounts-summary.txt");
    Assert.Contains("Total: 1", summary);
  }

  private static string ReadEntry(ZipArchive archive, string name)
  {
    var entry = archive.Entries.Single(e => e.FullName == name);
    using var reader = new StreamReader(entry.Open());
    return reader.ReadToEnd();
  }
}
