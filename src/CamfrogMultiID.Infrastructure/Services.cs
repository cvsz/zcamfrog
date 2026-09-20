using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CamfrogMultiID.Core;
using Microsoft.Data.Sqlite;

namespace CamfrogMultiID.Infrastructure;

public sealed class AppPaths
{
    public string Root { get; }
    public string Database => Path.Combine(Root, "camfrog.db");
    public string Profiles => Path.Combine(Root, "profiles");
    public string Secrets => Path.Combine(Root, "secrets");
    public string Logs => Path.Combine(Root, "logs");
    public string Settings => Path.Combine(Root, "settings.json");

    public AppPaths(string? root = null)
    {
        Root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamfrogMultiID")
            : Path.GetFullPath(root);

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Profiles);
        Directory.CreateDirectory(Secrets);
        Directory.CreateDirectory(Logs);
    }
}

public sealed class DatabaseService
{
    private readonly AppPaths _paths;
    private readonly object _gate = new();

    public DatabaseService(AppPaths paths) => _paths = paths;

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={_paths.Database};Cache=Shared");
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void Initialize()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS accounts (
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
                CREATE UNIQUE INDEX IF NOT EXISTS ux_accounts_username ON accounts(username COLLATE NOCASE);
                CREATE TABLE IF NOT EXISTS events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    utc TEXT NOT NULL,
                    level TEXT NOT NULL,
                    message TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();

            EnsureColumn(connection, "accounts", "process_executable_path", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "accounts", "last_error", "TEXT NOT NULL DEFAULT ''");
        }
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    public List<CamfrogAccount> GetAccounts()
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,display_name,username,secret_name,profile_directory,
                       enabled,status,process_id,started_utc,process_executable_path,last_error
                FROM accounts ORDER BY id;
                """;
            using var reader = command.ExecuteReader();
            var result = new List<CamfrogAccount>();
            while (reader.Read())
            {
                result.Add(ReadAccount(reader));
            }
            return result;
        }
    }

    public CamfrogAccount? GetAccount(long id)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,display_name,username,secret_name,profile_directory,
                       enabled,status,process_id,started_utc,process_executable_path,last_error
                FROM accounts WHERE id=$id;
                """;
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            return reader.Read() ? ReadAccount(reader) : null;
        }
    }

    private static CamfrogAccount ReadAccount(SqliteDataReader reader) =>
        new()
        {
            Id = reader.GetInt64(0),
            DisplayName = reader.GetString(1),
            Username = reader.GetString(2),
            PasswordSecretName = reader.GetString(3),
            ProfileDirectory = reader.GetString(4),
            Enabled = reader.GetInt64(5) != 0,
            Status = reader.GetString(6),
            ProcessId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            StartedAtUtc = reader.IsDBNull(8)
                ? null
                : DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            ProcessExecutablePath = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            LastError = reader.GetString(10)
        };

    public bool UsernameExists(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM accounts WHERE username = $username COLLATE NOCASE)";
            command.Parameters.AddWithValue("$username", username);
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0;
        }
    }

    public long Add(CamfrogAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        ValidateAccount(account);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO accounts
                    (display_name,username,secret_name,profile_directory,enabled)
                VALUES($d,$u,$s,$p,$e);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$d", account.DisplayName);
            command.Parameters.AddWithValue("$u", account.Username);
            command.Parameters.AddWithValue("$s", account.PasswordSecretName);
            command.Parameters.AddWithValue("$p", account.ProfileDirectory);
            command.Parameters.AddWithValue("$e", account.Enabled ? 1 : 0);
            return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
    }

    public void Delete(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        lock (_gate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction();

            using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = "SELECT status FROM accounts WHERE id=$id;";
            check.Parameters.AddWithValue("$id", id);
            var status = check.ExecuteScalar()?.ToString();

            if (status is null)
                throw new InvalidOperationException("The account no longer exists.");

            if (status.Equals("Running", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Stop the account before deleting it.");

            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM accounts WHERE id=$id;";
            delete.Parameters.AddWithValue("$id", id);
            delete.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    public void UpdateRuntime(long id, string status, int? pid, DateTime? started, string error = "", string executablePath = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE accounts
                SET status=$s, process_id=$p, started_utc=$t,
                    process_executable_path=$x, last_error=$e
                WHERE id=$id;
                """;
            command.Parameters.AddWithValue("$s", status);
            command.Parameters.AddWithValue("$p", (object?)pid ?? DBNull.Value);
            command.Parameters.AddWithValue("$t", (object?)started?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
            command.Parameters.AddWithValue("$x", executablePath ?? string.Empty);
            command.Parameters.AddWithValue("$e", error ?? string.Empty);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public void Log(string level, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);
        ArgumentNullException.ThrowIfNull(message);

        lock (_gate)
        {
            var safeMessage = message.Replace("\r", " ").Replace("\n", " ");
            var utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            try
            {
                using var connection = Open();
                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO events(utc,level,message) VALUES($u,$l,$m)";
                command.Parameters.AddWithValue("$u", utc);
                command.Parameters.AddWithValue("$l", level);
                command.Parameters.AddWithValue("$m", safeMessage);
                command.ExecuteNonQuery();
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
            {
                // Logging is best-effort and must never take down the manager.
            }

            try
            {
                var logPath = Path.Combine(_paths.Logs, "app.log");
                RotateLogIfNeeded(logPath);
                File.AppendAllText(
                    logPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {safeMessage}{Environment.NewLine}",
                    new UTF8Encoding(false));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void ValidateAccount(CamfrogAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.DisplayName))
            throw new ArgumentException("Display name is required.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.Username))
            throw new ArgumentException("Username is required.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.PasswordSecretName))
            throw new ArgumentException("Password secret name is required.", nameof(account));
        if (string.IsNullOrWhiteSpace(account.ProfileDirectory))
            throw new ArgumentException("Profile directory is required.", nameof(account));
    }

    private static void RotateLogIfNeeded(string path)
    {
        const long maxBytes = 5 * 1024 * 1024;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < maxBytes)
                return;

            var archive = path + ".1";
            try { if (File.Exists(archive)) File.Delete(archive); } catch { }
            File.Move(path, archive, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

public sealed class CredentialService
{
    private readonly AppPaths _paths;
    public CredentialService(AppPaths paths) => _paths = paths;

    private string SecretPath(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains(Path.DirectorySeparatorChar) ||
            name.Contains(Path.AltDirectorySeparatorChar) ||
            name is "." or "..")
            throw new ArgumentException("Invalid secret name.", nameof(name));

        return Path.Combine(_paths.Secrets, name + ".bin");
    }

    public void Save(string name, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var path = SecretPath(name);
        var plain = Encoding.UTF8.GetBytes(password);

        try
        {
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            try
            {
                var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temp, protectedBytes);
                    File.Move(temp, path, true);
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public string? Load(string name)
    {
        var path = SecretPath(name);
        if (!File.Exists(path)) return null;

        var protectedBytes = File.ReadAllBytes(path);
        try
        {
            var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(plain); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public void Delete(string name)
    {
        var path = SecretPath(name);
        if (!File.Exists(path)) return;
        File.Delete(path);
    }
}

public sealed class SettingsService
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService(AppPaths paths) => _paths = paths;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_paths.Settings))
                return Defaults();

            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(_paths.Settings, Encoding.UTF8));

            return settings ?? Defaults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var executable = settings.ClientExecutable?.Trim() ?? string.Empty;
        var args = settings.ClientArgumentsTemplate?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(executable))
        {
            executable = Path.GetFullPath(executable);
            if (!File.Exists(executable))
                throw new FileNotFoundException("The configured client executable does not exist.", executable);
        }

        var normalized = new AppSettings
        {
            ClientExecutable = executable,
            ClientArgumentsTemplate = args,
            DataDirectory = _paths.Root
        };

        var json = JsonSerializer.Serialize(normalized, _jsonOptions);
        var temp = _paths.Settings + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, _paths.Settings, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private AppSettings Defaults() => new() { DataDirectory = _paths.Root };
}

public sealed class ProcessSessionService
{
    private readonly DatabaseService _database;

    public ProcessSessionService(DatabaseService database) => _database = database;

    public Process Start(CamfrogAccount account, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ClientExecutable))
            throw new InvalidOperationException("Configure the Camfrog client executable first.");

        var executable = Path.GetFullPath(settings.ClientExecutable);
        if (!File.Exists(executable))
            throw new FileNotFoundException("Configure a valid Camfrog client executable first.", executable);

        Directory.CreateDirectory(account.ProfileDirectory);

        var args = ExpandArguments(settings.ClientArgumentsTemplate, account);
        var workingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory;
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = args,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start the client process.");

        DateTime startedUtc;
        try { startedUtc = process.StartTime.ToUniversalTime(); }
        catch { startedUtc = DateTime.UtcNow; }

        account.ProcessId = process.Id;
        account.StartedAtUtc = startedUtc;
        account.Status = "Running";
        account.LastError = string.Empty;
        account.ProcessExecutablePath = executable;

        try
        {
            _database.UpdateRuntime(account.Id, "Running", process.Id, startedUtc, "", executable);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }

            process.Dispose();
            throw;
        }

        _database.Log("INFO", $"Started account '{account.DisplayName}' (PID {process.Id}).");
        return process;
    }

    public void Stop(CamfrogAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        if (account.ProcessId is not int pid)
        {
            _database.UpdateRuntime(account.Id, "Stopped", null, null);
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);

            if (!TryMatchTrackedProcess(process, account, out var reason))
            {
                _database.Log("WARN", $"Refusing to terminate PID {pid} for account '{account.DisplayName}': {reason}");
                throw new InvalidOperationException($"Cannot safely verify the tracked process: {reason}");
            }

            if (!process.HasExited)
            {
                try { process.CloseMainWindow(); } catch (InvalidOperationException) { }

                if (!process.HasExited && !process.WaitForExit(5000))
                    process.Kill(entireProcessTree: true);

                if (!process.HasExited && !process.WaitForExit(5000))
                    throw new InvalidOperationException("The client process did not terminate.");
            }

            _database.UpdateRuntime(account.Id, "Stopped", null, null);
            _database.Log("INFO", $"Stopped account '{account.DisplayName}' (PID {pid}).");
        }
        catch (ArgumentException)
        {
            _database.UpdateRuntime(account.Id, "Stopped", null, null);
            _database.Log("INFO", $"Account '{account.DisplayName}' process PID {pid} was already gone.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            _database.UpdateRuntime(account.Id, "Error", pid, account.StartedAtUtc, ex.Message, account.ProcessExecutablePath);
            _database.Log("ERROR", $"Failed to stop account '{account.DisplayName}' (PID {pid}): {ex.Message}");
            throw;
        }
    }

    public static bool IsTrackedProcessAlive(CamfrogAccount account, out string? reason)
    {
        reason = null;
        if (account.ProcessId is not int pid)
        {
            reason = "No process ID.";
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
            {
                reason = "Process exited.";
                return false;
            }

            if (!TryMatchTrackedProcess(process, account, out reason))
                return false;

            return true;
        }
        catch (ArgumentException)
        {
            reason = "Process no longer exists.";
            return false;
        }
        catch (InvalidOperationException ex)
        {
            reason = $"Unable to verify process identity: {ex.Message}";
            return false;
        }
        catch (Win32Exception ex)
        {
            reason = $"Unable to verify process identity: {ex.Message}";
            return false;
        }
    }

    private static bool TryMatchTrackedProcess(Process process, CamfrogAccount account, out string reason)
    {
        reason = string.Empty;

        if (account.ProcessId is not int || process.HasExited)
        {
            reason = "Process no longer exists.";
            return false;
        }

        if (account.StartedAtUtc is DateTime expectedStart)
        {
            DateTime actualStart;
            try
            {
                actualStart = process.StartTime.ToUniversalTime();
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.Message;
                return false;
            }
            catch (Win32Exception ex)
            {
                reason = ex.Message;
                return false;
            }

            if (Math.Abs((actualStart - expectedStart.ToUniversalTime()).TotalSeconds) > 5)
            {
                reason = "PID was reused by a different process.";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(account.ProcessExecutablePath))
        {
            string? actualPath;
            try
            {
                actualPath = process.MainModule?.FileName;
            }
            catch (InvalidOperationException ex)
            {
                reason = ex.Message;
                return false;
            }
            catch (Win32Exception ex)
            {
                reason = ex.Message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(actualPath))
            {
                reason = "The process executable path could not be verified.";
                return false;
            }

            if (!string.Equals(
                    Path.GetFullPath(actualPath),
                    Path.GetFullPath(account.ProcessExecutablePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                reason = "Process executable path does not match the tracked executable.";
                return false;
            }
        }

        return true;
    }

    private static string ExpandArguments(string template, CamfrogAccount account) =>
        (template ?? string.Empty)
            .Replace("{username}", Quote(account.Username), StringComparison.Ordinal)
            .Replace("{profile}", Quote(account.ProfileDirectory), StringComparison.Ordinal);

    private static string Quote(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        var slashes = 0;
        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                slashes++;
                continue;
            }

            if (ch == '"')
            {
                sb.Append('\\', slashes * 2 + 1);
                sb.Append('"');
                slashes = 0;
                continue;
            }

            if (slashes > 0)
            {
                sb.Append('\\', slashes);
                slashes = 0;
            }

            sb.Append(ch);
        }

        if (slashes > 0)
            sb.Append('\\', slashes * 2);

        sb.Append('"');
        return sb.ToString();
    }
}
