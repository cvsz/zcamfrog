using System.IO;
using System.IO.Compression;
using System.Text;
using CamfrogMultiID.Core;
using CamfrogMultiID.Infrastructure;

namespace CamfrogMultiID.Tests;

public sealed class BackupServiceTests : IDisposable
{
  private readonly string _tempRoot;
  private readonly AppPaths _paths;
  private readonly DatabaseService _db;

  public BackupServiceTests()
  {
    _tempRoot = Path.Combine(Path.GetTempPath(), "zcamfrog-tests-backup-" + Guid.NewGuid().ToString("N"));
    _paths = new AppPaths(_tempRoot);
    _db = new DatabaseService(_paths);
    _db.Initialize();
  }

  public void Dispose()
  {
    try { Directory.Delete(_tempRoot, true); } catch { }
  }

  [Fact]
  public void CreateBackup_ContainsDatabaseSecretsAndSettings()
  {
    var creds = new CredentialService(_paths);
    creds.Save("b1", "pwd");
    new SettingsService(_paths).Save(new CamfrogMultiID.Core.AppSettings { ClientExecutable = @"C:\c.exe" });

    var zip = Path.Combine(_tempRoot, "backup.zip");
    BackupService.CreateBackup(_paths, zip);

    var entries = BackupService.ListEntries(zip);
    Assert.Contains(BackupService.DatabaseEntry, entries);
    Assert.Contains(BackupService.SettingsEntry, entries);
    Assert.Contains(entries, e => e.StartsWith(BackupService.SecretsPrefix, StringComparison.Ordinal));
  }

  [Fact]
  public void RestoreBackup_RoundtripsData()
  {
    var creds = new CredentialService(_paths);
    creds.Save("round", "secret123");
    var zip = Path.Combine(_tempRoot, "backup.zip");
    BackupService.CreateBackup(_paths, zip);

    Assert.True(creds.Delete("round"));
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    File.Delete(_paths.Database);
    Assert.False(File.Exists(_paths.Database));

    BackupService.RestoreBackup(_paths, zip);
    Assert.True(File.Exists(_paths.Database));
    Assert.Equal("secret123", creds.Load("round"));
  }

  [Fact]
  public void IsBackupDue_RespectsSchedule()
  {
    var settings = new CamfrogMultiID.Core.AppSettings { AutoBackupDays = 0 };
    Assert.False(BackupService.IsBackupDue(settings, DateTime.UtcNow));
    settings.AutoBackupDays = 7;
    Assert.True(BackupService.IsBackupDue(settings, DateTime.UtcNow));
    settings.LastAutoBackupUtc = DateTime.UtcNow;
    Assert.False(BackupService.IsBackupDue(settings, DateTime.UtcNow));
    settings.LastAutoBackupUtc = DateTime.UtcNow.AddDays(-8);
    Assert.True(BackupService.IsBackupDue(settings, DateTime.UtcNow));
  }

  [Fact]
  public void PruneBackups_KeepsNewest()
  {
    var dir = Path.Combine(_tempRoot, "prune");
    Directory.CreateDirectory(dir);
    foreach (var name in new[] { "CamfrogMultiID-backup-20260101-000000.zip", "CamfrogMultiID-backup-20260102-000000.zip", "CamfrogMultiID-backup-20260103-000000.zip", "other.txt" })
      File.WriteAllText(Path.Combine(dir, name), "x");
    Assert.Equal(1, BackupService.PruneBackups(dir, 2));
    Assert.True(File.Exists(Path.Combine(dir, "CamfrogMultiID-backup-20260103-000000.zip")));
    Assert.True(File.Exists(Path.Combine(dir, "CamfrogMultiID-backup-20260102-000000.zip")));
    Assert.True(File.Exists(Path.Combine(dir, "other.txt")));
    Assert.Equal(0, BackupService.PruneBackups(Path.Combine(_tempRoot, "missing"), 2));
  }

  [Fact]
  public void ValidateBackup_RejectsMissingDatabase()
  {
    var zip = Path.Combine(_tempRoot, "nodata.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var entry = archive.CreateEntry("settings.json");
      using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
      writer.Write("{}");
    }
    Assert.Throws<InvalidDataException>(() => BackupService.ValidateBackup(zip));
  }

  [Fact]
  public void RestoreBackup_RejectsPathTraversal()
  {
    var zip = Path.Combine(_tempRoot, "evil.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      var evil = archive.CreateEntry("../evil.txt");
      using (var w = new StreamWriter(evil.Open(), Encoding.UTF8)) { w.Write("x"); }
    }
    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
    Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_tempRoot)!, "evil.txt")));
  }

  [Fact]
  public void RestoreBackup_RejectsUncAndNestedTraversal()
  {
    foreach (var evil in new[] { "\\\\server\\share\\evil.bin", "secrets/a/../../evil.bin", "secrets/./x.bin", "/absolute/path.bin", "C:\\evil.bin" })
    {
      var zip = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N") + ".zip");
      using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
      {
        var db = archive.CreateEntry(BackupService.DatabaseEntry);
        using (var s = db.Open()) { }
        var entry = archive.CreateEntry(evil);
        using (var w = new StreamWriter(entry.Open(), Encoding.UTF8)) { w.Write("x"); }
      }
      Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
    }
  }

  [Fact]
  public void RestoreBackup_DuplicateEntries_AreDeterministic()
  {
    var zip = Path.Combine(_tempRoot, "dup.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      for (var i = 0; i < 3; i++)
      {
        var entry = archive.CreateEntry(BackupService.SettingsEntry);
        using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
        w.Write("{}");
      }
    }
    // Must not throw and must not escape: last write wins, same target.
    BackupService.RestoreBackup(_paths, zip);
    Assert.True(File.Exists(_paths.Settings));
  }

  [Fact]
  public void RestoreBackup_RejectsAbsoluteEntry()
  {
    var zip = Path.Combine(_tempRoot, "abs.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      var abs = archive.CreateEntry("/abs.txt");
      using (var w = new StreamWriter(abs.Open(), Encoding.UTF8)) { w.Write("x"); }
    }
    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
  }

  [Fact]
  public void RestoreBackup_RejectsNestedSecretEntry()
  {
    var zip = Path.Combine(_tempRoot, "nested-secret.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      var evil = archive.CreateEntry("secrets/nested/evil.bin");
      using (var w = new StreamWriter(evil.Open(), Encoding.UTF8)) { w.Write("x"); }
    }

    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
    Assert.False(File.Exists(Path.Combine(_paths.Secrets, "evil.bin")));
  }

  [Fact]
  public void RestoreBackup_RejectsDriveQualifiedEntry()
  {
    var zip = Path.Combine(_tempRoot, "drive-entry.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      var evil = archive.CreateEntry("C:/outside.bin");
      using (var w = new StreamWriter(evil.Open(), Encoding.UTF8)) { w.Write("x"); }
    }

    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
  }

  [Fact]
  public void RestoreBackup_RejectsEntryCountBomb()
  {
    var zip = Path.Combine(_tempRoot, "count-bomb.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      for (var i = 0; i < 5000; i++)
      {
        var e = archive.CreateEntry($"secrets/s{i}.bin");
        using (var s = e.Open()) { }
      }
    }
    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
  }

  [Fact]
  public void RestoreBackup_RejectsOversizedEntry()
  {
    var zip = Path.Combine(_tempRoot, "size-bomb.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open()) { }
      // A real entry just over the 64 MiB per-entry limit.
      var bomb = archive.CreateEntry("secrets/huge.bin", CompressionLevel.NoCompression);
      using (var stream = bomb.Open())
      {
        var chunk = new byte[1024 * 1024];
        for (var i = 0; i <= 64; i++)
          stream.Write(chunk, 0, chunk.Length);
      }
    }
    Assert.Throws<InvalidDataException>(() => BackupService.RestoreBackup(_paths, zip));
  }

  [Fact]
  public void RestoreBackup_FailureLeavesExistingDataIntact()
  {
    var creds = new CredentialService(_paths);
    creds.Save("keepme", "original-secret");
    File.WriteAllText(_paths.Settings, "{\"marker\":\"original\"}");
    _db.Add(new CamfrogAccount
    {
      DisplayName = "rollback",
      Username = "rollback_user",
      PasswordSecretName = "rollback_secret",
      ProfileDirectory = Path.Combine(_paths.Profiles, "rollback"),
      Enabled = true
    });
    var before = _db.GetAccounts().Select(a => a.Username).ToList();

    // Put a directory where settings.json must land, so the commit phase
    // fails after the database and the new secret were already written.
    // Everything must roll back to the pre-restore state.
    var zip = Path.Combine(_tempRoot, "partial.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var secret = archive.CreateEntry("secrets/newone.bin");
      using (var w = new StreamWriter(secret.Open(), Encoding.UTF8))
      {
        w.Write("x");
      }
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using (var s = db.Open())
      {
        s.Write(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });
      }
      var settings = archive.CreateEntry(BackupService.SettingsEntry);
      using (var w2 = new StreamWriter(settings.Open(), Encoding.UTF8))
      {
        w2.Write("{\"marker\":\"restored\"}");
      }
    }

    File.Delete(_paths.Settings);
    Directory.CreateDirectory(_paths.Settings);

    Assert.Throws<UnauthorizedAccessException>(() => BackupService.RestoreBackup(_paths, zip));

    // Pre-existing state survives untouched. A failed rollback would leave
    // the eight-byte garbage database in place and throw here instead.
    Assert.Equal("original-secret", creds.Load("keepme"));
    Assert.Equal(before, _db.GetAccounts().Select(a => a.Username).ToList());
    // The new secret from the failed restore was rolled back.
    Assert.False(File.Exists(Path.Combine(_paths.Secrets, "newone.bin")));
    // No staging or rollback directories are left behind.
    Assert.Empty(Directory.GetDirectories(_tempRoot, "restore-*"));
    Assert.Empty(Directory.GetDirectories(_tempRoot, "rollback-*"));
  }

  [Fact]
  public void RestoreBackup_RemovesStaleWalSidecars()
  {
    File.WriteAllText(_paths.Database + "-wal", "stale wal");
    File.WriteAllText(_paths.Database + "-shm", "stale shm");
    var zip = Path.Combine(_tempRoot, "ok.zip");
    using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
    {
      var db = archive.CreateEntry(BackupService.DatabaseEntry);
      using var s = db.Open();
      s.Write(new byte[] { 1, 2, 3 });
    }
    BackupService.RestoreBackup(_paths, zip);
    Assert.False(File.Exists(_paths.Database + "-wal"));
    Assert.False(File.Exists(_paths.Database + "-shm"));
    Assert.Empty(Directory.GetDirectories(_tempRoot, "restore-*"));
    Assert.Empty(Directory.GetDirectories(_tempRoot, "rollback-*"));
  }
}
