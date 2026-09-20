using System.IO.Compression;
using CamfrogMultiID.Core;
using Microsoft.Data.Sqlite;

namespace CamfrogMultiID.Infrastructure;

/// <summary>
/// Exports and restores manager data (SQLite database, DPAPI secrets,
/// settings). Profile directories are intentionally excluded: they are
/// large and client-regenerable. All zip entry names are validated to
/// block path traversal on restore.
/// </summary>
public static class BackupService
{
    public const string DatabaseEntry = "camfrog.db";
    public const string SettingsEntry = "settings.json";
    public const string SecretsPrefix = "secrets/";

    public static void CreateBackup(AppPaths paths, string destinationZip)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZip);

        var tempZip = destinationZip + "." + Guid.NewGuid().ToString("N") + ".tmp";
        // Snapshot the live database with VACUUM INTO: pooled SQLite connections
        // keep camfrog.db locked, so zipping it directly fails while running.
        var dbSnapshot = Path.Combine(
            Path.GetTempPath(),
            "camfrog-backup-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            SnapshotDatabase(paths.Database, dbSnapshot);
            using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                AddFile(archive, dbSnapshot, DatabaseEntry);
                AddFile(archive, paths.Settings, SettingsEntry);
                if (Directory.Exists(paths.Secrets))
                {
                    foreach (var file in Directory.GetFiles(paths.Secrets, "*.bin"))
                        AddFile(archive, file, SecretsPrefix + Path.GetFileName(file));
                }
            }

            File.Move(tempZip, destinationZip, true);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (File.Exists(dbSnapshot)) File.Delete(dbSnapshot); } catch { }
        }
    }

    private static void SnapshotDatabase(string databasePath, string snapshotPath)
    {
        if (!File.Exists(databasePath))
            return;
        using var connection = new SqliteConnection($"Data Source={databasePath};Cache=Shared");
        connection.Open();
        using var command = connection.CreateCommand();
        // Parameterize the path via quote escaping: single quotes doubled.
        var escaped = snapshotPath.Replace("'", "''", StringComparison.Ordinal);
        command.CommandText = $"VACUUM INTO '{escaped}';";
        command.ExecuteNonQuery();
    }

    public static string BackupsDirectory(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var dir = Path.Combine(paths.Root, "backups");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string DefaultBackupName(DateTime now) =>
        $"CamfrogMultiID-backup-{now:yyyyMMdd-HHmmss}.zip";

    public static int PruneBackups(string backupsDirectory, int keepCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupsDirectory);
        ArgumentOutOfRangeException.ThrowIfNegative(keepCount);
        if (!Directory.Exists(backupsDirectory))
            return 0;
        var files = Directory.GetFiles(backupsDirectory, "CamfrogMultiID-backup-*.zip")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(keepCount)
            .ToList();
        var removed = 0;
        foreach (var file in files)
        {
            try { File.Delete(file); removed++; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return removed;
    }

    public static bool IsBackupDue(AppSettings settings, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.AutoBackupDays <= 0)
            return false;
        if (settings.LastAutoBackupUtc is not DateTime last)
            return true;
        return (nowUtc - last.ToUniversalTime()).TotalDays >= settings.AutoBackupDays;
    }

    public static IReadOnlyList<string> ListEntries(string backupZip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupZip);
        using var archive = ZipFile.OpenRead(backupZip);
        return archive.Entries.Select(e => e.FullName).ToList();
    }

    public static void ValidateBackup(string backupZip)
    {
        var entries = ListEntries(backupZip);
        if (!entries.Contains(DatabaseEntry, StringComparer.Ordinal))
            throw new InvalidDataException("Backup is missing the database entry.");
        foreach (var entry in entries)
            ValidateEntryName(entry);
    }

    public static void RestoreBackup(AppPaths paths, string backupZip)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ValidateBackup(backupZip);

        // Release pooled handles on the live database so overwrite succeeds.
        SqliteConnection.ClearAllPools();
        using var archive = ZipFile.OpenRead(backupZip);
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            var target = MapEntry(paths, entry.FullName);
            if (target is null)
                continue;

            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            entry.ExtractToFile(target, overwrite: true);
        }
    }

    internal static void ValidateEntryName(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            throw new InvalidDataException("Backup contains an empty entry name.");
        var normalized = entry.Replace('\\', '/');
        if (normalized.StartsWith('/'))
            throw new InvalidDataException($"Unsafe backup entry: '{entry}'.");
        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "" or "." or "..")
                throw new InvalidDataException($"Unsafe backup entry: '{entry}'.");
            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException($"Unsafe backup entry: '{entry}'.");
        }
        if (!string.Equals(normalized, DatabaseEntry, StringComparison.Ordinal) &&
            !string.Equals(normalized, SettingsEntry, StringComparison.Ordinal) &&
            !normalized.StartsWith(SecretsPrefix, StringComparison.Ordinal))
            throw new InvalidDataException($"Unexpected backup entry: '{entry}'.");
    }

    private static void AddFile(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
            return;
        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
    }

    private static string? MapEntry(AppPaths paths, string entry)
    {
        var normalized = entry.Replace('\\', '/');
        if (string.Equals(normalized, DatabaseEntry, StringComparison.Ordinal))
            return paths.Database;
        if (string.Equals(normalized, SettingsEntry, StringComparison.Ordinal))
            return paths.Settings;
        if (normalized.StartsWith(SecretsPrefix, StringComparison.Ordinal))
            return Path.Combine(paths.Secrets, normalized.Substring(SecretsPrefix.Length));
        return null;
    }
}
