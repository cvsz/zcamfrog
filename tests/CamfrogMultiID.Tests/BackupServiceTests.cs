using System.IO;
using System.IO.Compression;
using System.Text;
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
}
