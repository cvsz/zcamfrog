using System.IO.Compression;
using CamfrogMultiID.Core;
using Microsoft.Data.Sqlite;

namespace CamfrogMultiID.Infrastructure;

/// <summary>
/// Exports and restores manager data (SQLite database, DPAPI secrets,
/// settings). Profile directories are intentionally excluded: they are
/// large and client-regenerable. Restore accepts only the documented entry
/// names and resolves secret entries to a single safe file-name component.
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

  /// <summary>Upper bounds so a hostile archive cannot exhaust the disk.</summary>
  private const long MaxEntryBytes = 64L * 1024 * 1024;
  private const long MaxTotalBytes = 512L * 1024 * 1024;
  private const int MaxEntries = 4096;

  /// <summary>
  /// Restores a backup as an all-or-nothing operation. Every entry is
  /// extracted into a staging directory first, existing targets are copied
  /// aside, and any failure rolls the whole restore back. A partial restore
  /// would otherwise leave, for example, a new database beside the old
  /// credentials.
  /// </summary>
  public static void RestoreBackup(AppPaths paths, string backupZip)
  {
    ArgumentNullException.ThrowIfNull(paths);
    ValidateBackup(backupZip);

    SqliteConnection.ClearAllPools();

    var staging = Path.Combine(paths.Root, "restore-" + Guid.NewGuid().ToString("N"));
    var rollback = Path.Combine(paths.Root, "rollback-" + Guid.NewGuid().ToString("N"));
    var staged = new List<(string Staged, string Target)>();
    var replaced = new List<(string Target, string Saved)>();
    var created = new List<string>();
    try
    {
      Directory.CreateDirectory(staging);
      Directory.CreateDirectory(rollback);

      using (var archive = ZipFile.OpenRead(backupZip))
      {
        if (archive.Entries.Count > MaxEntries)
          throw new InvalidDataException($"Backup has too many entries (limit {MaxEntries}).");
        long total = 0;
        foreach (var entry in archive.Entries)
        {
          ValidateEntryName(entry.FullName);
          if (entry.Length > MaxEntryBytes)
            throw new InvalidDataException($"Backup entry '{entry.FullName}' exceeds the {MaxEntryBytes} byte per-entry limit.");
          total += entry.Length;
          if (total > MaxTotalBytes)
            throw new InvalidDataException("Backup exceeds the maximum uncompressed size limit.");
          if (MapEntry(paths, entry.FullName) is { } target)
            staged.Add((Extract(entry, staging), target));
        }
      }

      // Nothing is overwritten until every entry is staged successfully.
      foreach (var (_, target) in staged)
      {
        if (File.Exists(target))
        {
          var saved = Path.Combine(rollback, Guid.NewGuid().ToString("N") + ".bak");
          File.Copy(target, saved, overwrite: true);
          replaced.Add((target, saved));
        }
        else
        {
          created.Add(target);
        }
      }

      foreach (var (stagedPath, target) in staged)
      {
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
          Directory.CreateDirectory(directory);
        File.Move(stagedPath, target, overwrite: true);
      }

      // A replaced database must not keep WAL/SHM sidecars from the old file.
      if (staged.Any(s => string.Equals(s.Target, paths.Database, StringComparison.OrdinalIgnoreCase)))
      {
        TryDelete(paths.Database + "-wal");
        TryDelete(paths.Database + "-shm");
      }
    }
    catch
    {
      foreach (var (target, saved) in replaced)
      {
        try
        {
          if (File.Exists(saved))
            File.Copy(saved, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
      }
      foreach (var target in created)
        TryDelete(target);
      throw;
    }
    finally
    {
      TryDeleteDirectory(staging);
      TryDeleteDirectory(rollback);
    }
  }

  private static string Extract(ZipArchiveEntry entry, string staging)
  {
    var staged = Path.Combine(staging, Guid.NewGuid().ToString("N") + ".stage");
    entry.ExtractToFile(staged, overwrite: true);
    return staged;
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
        File.Delete(path);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
  }

  private static void TryDeleteDirectory(string path)
  {
    try
    {
      if (Directory.Exists(path))
        Directory.Delete(path, recursive: true);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
    }
  }

  internal static void ValidateEntryName(string entry)
  {
    if (string.IsNullOrWhiteSpace(entry))
      throw new InvalidDataException("Backup contains an empty entry name.");
    var normalized = entry.Replace('\\', '/');
    if (Path.IsPathFullyQualified(normalized) || normalized.StartsWith('/'))
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
    if (!normalized.StartsWith(SecretsPrefix, StringComparison.Ordinal))
      return null;

    // Path.GetFileName is an explicit Zip Slip sanitizer: only a single
    // leaf file name may reach Path.Combine/ExtractToFile.
    var secretFile = Path.GetFileName(normalized);
    if (string.IsNullOrWhiteSpace(secretFile) ||
        !string.Equals(normalized, SecretsPrefix + secretFile, StringComparison.Ordinal) ||
        !secretFile.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException($"Unsafe backup entry: '{entry}'.");

    return Path.Combine(paths.Secrets, secretFile);
  }
}
