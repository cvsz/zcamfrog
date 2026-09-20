using System.Diagnostics;
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

    public AppPaths() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CamfrogMultiID"))
    {
    }

    public AppPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
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
        pragma.CommandText = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON;";
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

            // Forward-compatible migration for databases created by older builds.
            EnsureColumn(connection, "accounts", "process_executable_path", "TEXT NOT NULL DEFAULT ''");
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
                result.Add(new CamfrogAccount
                {
                    Id = reader.GetInt64(0),
                    DisplayName = reader.GetString(1),
                    Username = reader.GetString(2),
                    PasswordSecretName = reader.GetString(3),
                    ProfileDirectory = reader.GetString(4),
                    Enabled = reader.GetInt64(5) != 0,
                    Status = reader.GetString(6),
                    ProcessId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                    StartedAtUtc = reader.IsDBNull(8) ? null :
                        DateTime.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    ProcessExecutablePath = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    LastError = reader.GetString(10)
                });
            }
            return result;
        }
    }

    public bool UsernameExists(string username)
    {
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM accounts WHERE username = $username COLLATE NOCASE)";
            command.Parameters.AddWithValue("$username", username);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
    }

    public long Add(CamfrogAccount account)
    {
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
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public void UpdateRuntime(long id, string status, int? pid, DateTime? started, string error = "", string executablePath = "")
    {
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
            command.Parameters.AddWithValue("$t", (object?)started?.ToString("O") ?? DBNull.Value);
            command.Parameters.AddWithValue("$x", executablePath ?? string.Empty);
            command.Parameters.AddWithValue("$e", error ?? string.Empty);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
    }

    public void Log(string level, string message)
    {
        lock (_gate)
        {
            var safeMessage = message.Replace("\r", " ").Replace("\n", " ");
            var utc = DateTime.UtcNow.ToString("O");

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
            catch (Exception _ex) when (_ex is SqliteException or IOException or UnauthorizedAccessException)
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
                    Encoding.UTF8);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
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

    private string SecretPath(string name) => Path.Combine(_paths.Secrets, name + ".bin");

    public void Save(string name, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var plain = Encoding.UTF8.GetBytes(password);
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            try
            {
                var path = SecretPath(name);
                var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllBytes(temp, protectedBytes);
                File.Move(temp, path, true);
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

            return JsonSerializer.Deserialize<AppSettings>(
                       File.ReadAllText(_paths.Settings, Encoding.UTF8)) ?? Defaults();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Defaults();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.DataDirectory = _paths.Root;
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
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
            UseShellExecute = true,
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

            throw;
        }

        _database.Log("INFO", $"Started account '{account.DisplayName}' (PID {process.Id}).");
        return process;
    }

    public void Stop(CamfrogAccount account)
    {
        if (account.ProcessId is not int pid)
        {
            _database.UpdateRuntime(account.Id, "Stopped", null, null);
            return;
        }

        try
        {
            using var process = Process.GetProcessById(pid);

            if (!MatchesTrackedProcess(process, account))
            {
                _database.Log("WARN", $"PID {pid} no longer matches account '{account.DisplayName}'; refusing to terminate it.");
                _database.UpdateRuntime(account.Id, "Stopped", null, null, "Tracked process no longer matches.");
                return;
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
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
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

            if (!MatchesTrackedProcess(process, account))
            {
                reason = "PID was reused by a different process.";
                return false;
            }

            return true;
        }
        catch (ArgumentException)
        {
            reason = "Process no longer exists.";
            return false;
        }
        catch (InvalidOperationException)
        {
            reason = "Process state is unavailable.";
            return true;
        }
    }

    private static bool MatchesTrackedProcess(Process process, CamfrogAccount account)
    {
        if (account.ProcessId is not int || process.HasExited)
            return false;

        if (account.StartedAtUtc is DateTime expectedStart)
        {
            try
            {
                var actual = process.StartTime.ToUniversalTime();
                if (Math.Abs((actual - expectedStart.ToUniversalTime()).TotalSeconds) > 5)
                    return false;
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        if (!string.IsNullOrWhiteSpace(account.ProcessExecutablePath))
        {
            try
            {
                var actualPath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(actualPath) &&
                    !string.Equals(
                        Path.GetFullPath(actualPath),
                        Path.GetFullPath(account.ProcessExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
        }

        return true;
    }

    private static string ExpandArguments(string template, CamfrogAccount account) =>
        (template ?? string.Empty)
            .Replace("{username}", Quote(account.Username), StringComparison.Ordinal)
            .Replace("{profile}", Quote(account.ProfileDirectory), StringComparison.Ordinal);

    private static string Quote(string value)
    {
        // Windows command-line quoting: escape quotes and only the backslashes
        // that immediately precede a quote or the closing quote.
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
