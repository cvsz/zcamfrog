using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using CamfrogMultiID.Core;

namespace CamfrogMultiID.Infrastructure;

/// <summary>
/// Builds an opt-in, local-only diagnostics bundle for support requests.
/// Explicit export only: nothing is collected or transmitted automatically,
/// and the bundle never includes secrets, passwords, or usernames.
/// </summary>
public static class DiagnosticsService
{
    public static void ExportBundle(AppPaths paths, DatabaseService database, string destinationZip)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationZip);

        var tempZip = destinationZip + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
            {
                AddTextEntry(archive, "versions.txt", BuildVersions());
                AddTextEntry(archive, "accounts-summary.txt", BuildAccountsSummary(database));
                AddFileEntry(archive, Path.Combine(paths.Logs, "app.log"), "app.log");
                AddFileEntry(archive, paths.Settings, "settings.json");
            }

            File.Move(tempZip, destinationZip, true);
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
        }
    }

    private static string BuildVersions()
    {
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
        return string.Join(Environment.NewLine, new[]
        {
            $"App: {appVersion}",
            $"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            $"Runtime: {RuntimeInformation.FrameworkDescription}",
            $"UTC: {DateTime.UtcNow:O}"
        }) + Environment.NewLine;
    }

    private static string BuildAccountsSummary(DatabaseService database)
    {
        var accounts = database.GetAccounts();
        var byStatus = accounts
            .GroupBy(a => a.Status, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}: {g.Count()}");
        return string.Join(Environment.NewLine, new[]
        {
            $"Total: {accounts.Count}",
            $"Enabled: {accounts.Count(a => a.Enabled)}",
            $"AutoRestart: {accounts.Count(a => a.AutoRestart)}",
            string.Join(Environment.NewLine, byStatus)
        }) + Environment.NewLine;
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
        writer.Write(content);
    }

    private static void AddFileEntry(ZipArchive archive, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
            return;
        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
    }
}
