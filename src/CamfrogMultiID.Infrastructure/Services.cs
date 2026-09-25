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
        RestrictSecretsAccess();
    }

    private void RestrictSecretsAccess()
    {
        // Best-effort: DPAPI blobs should be reachable only by the current user.
        // Never let an ACL failure break startup.
        try
        {
            var identity = System.Security.Principal.WindowsIdentity.GetCurrent()?.Name;
            if (string.IsNullOrWhiteSpace(identity))
                return;
            var security = new DirectoryInfo(Secrets).GetAccessControl();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                identity,
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Allow));
            new DirectoryInfo(Secrets).SetAccessControl(security);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (PlatformNotSupportedException) { }
        catch (InvalidOperationException) { }
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
                    last_error TEXT NOT NULL DEFAULT '',
                    room_url TEXT NOT NULL DEFAULT '',
                    auto_restart INTEGER NOT NULL DEFAULT 0,
                    password_changed_utc TEXT NOT NULL DEFAULT ''
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
            EnsureColumn(connection, "accounts", "room_url", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "accounts", "auto_restart", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "accounts", "password_changed_utc", "TEXT NOT NULL DEFAULT ''");
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
                       enabled,status,process_id,started_utc,process_executable_path,last_error,room_url,auto_restart,password_changed_utc
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
                        (DateTime.TryParse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedStart) ? parsedStart : null),
                    ProcessExecutablePath = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    LastError = reader.GetString(10),
                    RoomUrl = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                    AutoRestart = !reader.IsDBNull(12) && reader.GetInt64(12) != 0,
                    PasswordChangedUtc = ReadNullableDate(reader, 13)
                });
            }
            return result;
        }
    }

    private static DateTime? ReadNullableDate(Microsoft.Data.Sqlite.SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) || string.IsNullOrWhiteSpace(reader.GetString(ordinal)) ? null :
            (DateTime.TryParse(reader.GetString(ordinal), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null);

    public bool UsernameExists(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM accounts WHERE username = $username COLLATE NOCASE)";
            command.Parameters.AddWithValue("$username", username);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
    }

    public bool UsernameExistsExcept(string username, long excludeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(excludeId);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM accounts WHERE username = $username COLLATE NOCASE AND id <> $id)";
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$id", excludeId);
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0;
        }
    }

    public CamfrogAccount? GetById(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT id,display_name,username,secret_name,profile_directory,
                       enabled,status,process_id,started_utc,process_executable_path,last_error,room_url,auto_restart,password_changed_utc
                FROM accounts WHERE id=$id;
                """;
            command.Parameters.AddWithValue("$id", id);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
                return null;
            return new CamfrogAccount
            {
                Id = reader.GetInt64(0),
                DisplayName = reader.GetString(1),
                Username = reader.GetString(2),
                PasswordSecretName = reader.GetString(3),
                ProfileDirectory = reader.GetString(4),
                Enabled = reader.GetInt64(5) != 0,
                Status = reader.GetString(6),
                ProcessId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                StartedAtUtc = ReadNullableDate(reader, 8),
                ProcessExecutablePath = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                LastError = reader.GetString(10),
                RoomUrl = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                AutoRestart = !reader.IsDBNull(12) && reader.GetInt64(12) != 0,
                PasswordChangedUtc = ReadNullableDate(reader, 13)
            };
        }
    }

    public void UpdateDetails(long id, string displayName, string username, bool enabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE accounts
                SET display_name=$d, username=$u, enabled=$e
                WHERE id=$id;
                """;
            command.Parameters.AddWithValue("$d", displayName.Trim());
            command.Parameters.AddWithValue("$u", username.Trim());
            command.Parameters.AddWithValue("$e", enabled ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            var rows = command.ExecuteNonQuery();
            if (rows == 0)
                throw new InvalidOperationException($"Account id {id} was not found.");
        }
    }

    public void SetRoomUrl(long id, string roomUrl)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE accounts SET room_url=$r WHERE id=$id;";
            command.Parameters.AddWithValue("$r", roomUrl ?? string.Empty);
            command.Parameters.AddWithValue("$id", id);
            var rows = command.ExecuteNonQuery();
            if (rows == 0)
                throw new InvalidOperationException($"Account id {id} was not found.");
        }
    }

    public void SetPasswordChanged(long id, DateTime changedUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE accounts SET password_changed_utc=$t WHERE id=$id;";
            command.Parameters.AddWithValue("$t", changedUtc.ToUniversalTime().ToString("O"));
            command.Parameters.AddWithValue("$id", id);
            var rows = command.ExecuteNonQuery();
            if (rows == 0)
                throw new InvalidOperationException($"Account id {id} was not found.");
        }
    }

    public void SetAutoRestart(long id, bool autoRestart)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE accounts SET auto_restart=$a WHERE id=$id;";
            command.Parameters.AddWithValue("$a", autoRestart ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            var rows = command.ExecuteNonQuery();
            if (rows == 0)
                throw new InvalidOperationException($"Account id {id} was not found.");
        }
    }

    public void SetEnabled(long id, bool enabled)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE accounts SET enabled=$e WHERE id=$id;";
            command.Parameters.AddWithValue("$e", enabled ? 1 : 0);
            command.Parameters.AddWithValue("$id", id);
            var rows = command.ExecuteNonQuery();
            if (rows == 0)
                throw new InvalidOperationException($"Account id {id} was not found.");
        }
    }

    public bool Delete(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM accounts WHERE id=$id;";
            command.Parameters.AddWithValue("$id", id);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public void ClearError(long id)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        lock (_gate)
        {
            using var connection = Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE accounts SET last_error='', status=CASE WHEN status='Error' THEN 'Stopped' ELSE status END WHERE id=$id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
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
                    (display_name,username,secret_name,profile_directory,enabled,room_url,auto_restart)
                VALUES($d,$u,$s,$p,$e,$r,$a);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$d", account.DisplayName);
            command.Parameters.AddWithValue("$u", account.Username);
            command.Parameters.AddWithValue("$s", account.PasswordSecretName);
            command.Parameters.AddWithValue("$p", account.ProfileDirectory);
            command.Parameters.AddWithValue("$e", account.Enabled ? 1 : 0);
            command.Parameters.AddWithValue("$r", account.RoomUrl ?? string.Empty);
            command.Parameters.AddWithValue("$a", account.AutoRestart ? 1 : 0);
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

    public bool Exists(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        return File.Exists(SecretPath(name));
    }

    public bool Delete(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var path = SecretPath(name);
        try
        {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
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
        if (!string.IsNullOrWhiteSpace(account.RoomUrl))
            args = string.IsNullOrWhiteSpace(args)
                ? $"--url={Quote(NormalizeRoomUrl(account.RoomUrl))}"
                : $"{args} --url={Quote(NormalizeRoomUrl(account.RoomUrl))}";

        var trackedExecutable = executable;
        ProcessStartInfo psi;
        if (settings.UseSandboxie)
        {
            var configured = string.IsNullOrWhiteSpace(settings.SandboxieStartExe)
                ? FindSandboxieStart()
                : settings.SandboxieStartExe;
            if (!IsSandboxieReady(configured, out var reason))
                throw new InvalidOperationException($"Sandboxie is not ready: {reason}");
            var startExe = Path.GetFullPath(configured!);
            trackedExecutable = startExe;
            // Start.exe rejects unknown boxes ("Invalid box name parameter",
            // Sbie message 3204), so ensure the box exists first. Creation is
            // idempotent: an existing box is left untouched.
            try
            {
                CreateBox(startExe, SanitizeBoxName(account));
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException(
                    $"Could not ensure Sandboxie box '{SanitizeBoxName(account)}'. " +
                    "Create it via Settings, or verify the Sandboxie service is running.", ex);
            }
            psi = new ProcessStartInfo
            {
                FileName = startExe,
                // /wait keeps Start.exe alive while the sandboxed client runs, so the
                // tracked PID stays valid for the whole session.
                Arguments = $"/wait /Box:{SanitizeBoxName(account)} {Quote(executable)}{(string.IsNullOrWhiteSpace(args) ? string.Empty : " " + args)}",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
            };
        }
        else
        {
            var workingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory;
            psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                UseShellExecute = true,
                WorkingDirectory = workingDirectory
            };
        }

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start the client process.");

        DateTime startedUtc;
        try { startedUtc = process.StartTime.ToUniversalTime(); }
        catch { startedUtc = DateTime.UtcNow; }

        account.ProcessId = process.Id;
        account.StartedAtUtc = startedUtc;
        account.Status = "Running";
        account.LastError = string.Empty;
        account.ProcessExecutablePath = trackedExecutable;

        try
        {
            _database.UpdateRuntime(account.Id, "Running", process.Id, startedUtc, "", trackedExecutable);
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

    public static string PreviewArguments(string? template, CamfrogAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return ExpandArguments(template, account);
    }

    public static string PreviewCommandLine(string? executable, string? template, CamfrogAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var exe = string.IsNullOrWhiteSpace(executable) ? "<client-exe>" : executable.Trim();
        var args = ExpandArguments(template, account);
        if (!string.IsNullOrWhiteSpace(account.RoomUrl))
            args = string.IsNullOrWhiteSpace(args)
                ? $"--url={Quote(account.RoomUrl.Trim())}"
                : $"{args} --url={Quote(account.RoomUrl.Trim())}";
        return string.IsNullOrWhiteSpace(args) ? $"\"{exe}\"" : $"\"{exe}\" {args}";
    }

    public static string PreviewLaunch(AppSettings settings, CamfrogAccount account)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(account);
        var inner = PreviewCommandLine(settings.ClientExecutable, settings.ClientArgumentsTemplate, account);
        if (!settings.UseSandboxie)
            return inner;
        var startExe = string.IsNullOrWhiteSpace(settings.SandboxieStartExe) ? FindSandboxieStart() ?? "<Start.exe>" : settings.SandboxieStartExe;
        // Box names are sanitized to letters/digits only, so no quoting is
        // needed (and Sandboxie's parser rejects quoted names).
        return $"\"{startExe}\" /wait /Box:{SanitizeBoxName(account)} {inner}";
    }

    public static string NormalizeRoomUrl(string roomUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomUrl);
        var trimmed = roomUrl.Trim();
        if (!trimmed.StartsWith("camfrog:", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Room URL must use the camfrog: scheme (copy the room link from the client room directory).");
        if (trimmed.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new InvalidOperationException("Room URL must not contain whitespace or control characters.");
        return trimmed;
    }

    public static string SanitizeBoxName(CamfrogAccount account)
    {
        // Matches the Sandboxie engine rule (start.cpp Parse_Command_Line):
        // letters, digits, and underscore only, max 32 chars. Underscores
        // are preserved so box names stay recognizable next to nicknames
        // such as "_oIo_".
        ArgumentNullException.ThrowIfNull(account);
        var base_name = new string(account.Username.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (base_name.Length > 32)
            base_name = base_name.Substring(0, 32);
        if (string.IsNullOrEmpty(base_name))
            base_name = "account" + account.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return base_name;
    }

    public sealed record ForeignProcess(int ProcessId, string ExecutablePath, DateTime StartTimeUtc);

    /// <summary>
    /// Finds client processes that the manager does not track (e.g. a copy
    /// the user started by hand). A second launch while one of these lives
    /// typically hands off and exits within seconds because the Camfrog
    /// client is single-instance per session. Warning-only: the caller
    /// decides; never kills anything here.
    /// </summary>
    public static IReadOnlyList<ForeignProcess> FindForeignClientProcesses(string executablePath, int? excludePid = null)
    {
        var result = new List<ForeignProcess>();
        if (string.IsNullOrWhiteSpace(executablePath))
            return result;
        string processName;
        string expectedFullPath;
        try
        {
            expectedFullPath = Path.GetFullPath(executablePath);
            processName = Path.GetFileNameWithoutExtension(expectedFullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return result;
        }
        if (string.IsNullOrWhiteSpace(processName))
            return result;

        Process[] candidates;
        try { candidates = Process.GetProcessesByName(processName); }
        catch (InvalidOperationException) { return result; }

        foreach (var candidate in candidates)
        {
            using (candidate)
            {
                try
                {
                    if (candidate.HasExited)
                        continue;
                    if (excludePid is int excluded && candidate.Id == excluded)
                        continue;
                    string actualPath;
                    try { actualPath = candidate.MainModule?.FileName ?? string.Empty; }
                    catch (InvalidOperationException) { continue; }
                    catch (System.ComponentModel.Win32Exception) { continue; }
                    if (!string.IsNullOrWhiteSpace(actualPath) &&
                        !string.Equals(Path.GetFullPath(actualPath), expectedFullPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    DateTime started;
                    try { started = candidate.StartTime.ToUniversalTime(); }
                    catch (InvalidOperationException) { continue; }
                    catch (System.ComponentModel.Win32Exception) { continue; }
                    result.Add(new ForeignProcess(candidate.Id, actualPath, started));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return result;
    }

    public static string? FindSandboxieStart()
    {
        string[] candidates =
        [
            @"C:\Program Files\Sandboxie-Plus\Start.exe",
            @"C:\Program Files (x86)\Sandboxie\Start.exe",
        ];
        foreach (var candidate in candidates)
        {
            try { if (File.Exists(candidate)) return candidate; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return null;
    }

    public const string SandboxieReleasesUrl = "https://github.com/sandboxie-plus/Sandboxie/releases";
    public const string SandboxieRepository = "https://github.com/sandboxie-plus/Sandboxie";

    /// <summary>
    /// Verifies Sandboxie can actually run boxes: Start.exe must exist and
    /// the SbieSvc service must be installed and running (a bare file copy
    /// without driver/service install fails every launch).
    /// </summary>
    public static bool IsSandboxieReady(string? configuredStartExe, out string? reason)
    {
        reason = null;
        var startExe = string.IsNullOrWhiteSpace(configuredStartExe) ? FindSandboxieStart() : configuredStartExe;
        if (string.IsNullOrWhiteSpace(startExe) || !File.Exists(startExe))
        {
            reason = "Start.exe was not found.";
            return false;
        }
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SbieSvc");
            if (key is null)
            {
                reason = "The SbieSvc service is not installed. Run the Sandboxie-Plus installer (as admin) first.";
                return false;
            }
        }
        catch (System.Security.SecurityException)
        {
            reason = "Cannot read service status.";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "Cannot read service status.";
            return false;
        }
        catch (IOException)
        {
            reason = "Cannot read service status.";
            return false;
        }

        if (!IsServiceRunning("SbieSvc"))
        {
            reason = "The SbieSvc service is installed but not running. Start it from services.msc (as admin).";
            return false;
        }
        return true;
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            using var service = new System.ServiceProcess.ServiceController(serviceName);
            return service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    /// <summary>
    /// Best-effort start of the Sandboxie service (requires elevation;
    /// fails gracefully with a reason otherwise).
    /// </summary>
    public static bool TryStartSandboxieService(out string? reason)
    {
        reason = null;
        try
        {
            using var service = new System.ServiceProcess.ServiceController("SbieSvc");
            try { var _ = service.Status; }
            catch (InvalidOperationException)
            {
                reason = "The SbieSvc service is not installed. Run the Sandboxie-Plus installer (as admin) first.";
                return false;
            }
            if (service.Status == System.ServiceProcess.ServiceControllerStatus.Running)
                return true;
            service.Start();
            service.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            return service.Status == System.ServiceProcess.ServiceControllerStatus.Running;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.ServiceProcess.TimeoutException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            reason = $"Could not start the SbieSvc service ({ex.Message}). Run the manager as admin, or start it from services.msc.";
            return false;
        }
    }

    public static string? GetSandboxieVersion(string? startExePath)
    {
        if (string.IsNullOrWhiteSpace(startExePath))
            return null;
        try
        {
            if (!File.Exists(startExePath))
                return null;
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(startExePath);
            var version = info.ProductVersion ?? info.FileVersion;
            return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static string GetBoxesRoot()
    {
        // Overridable for tests; default matches Sandboxie-Plus per-user layout.
        var overrideRoot = Environment.GetEnvironmentVariable("CAMFROGMULTIID_SANDBOX_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return overrideRoot;
        return Path.Combine(
            Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\",
            "Sandbox",
            Environment.UserName);
    }

    public static bool BoxExists(string boxName)
    {
        if (string.IsNullOrWhiteSpace(boxName))
            return false;
        try { return Directory.Exists(Path.Combine(GetBoxesRoot(), boxName)); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public static string BuildCreateBoxArguments(string boxName) =>
        $"/Box:{boxName} \"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")}\" /c exit";

    public static void CreateBox(string startExe, string boxName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startExe);
        ArgumentException.ThrowIfNullOrWhiteSpace(boxName);
        if (!File.Exists(startExe))
            throw new FileNotFoundException("Sandboxie Start.exe was not found.", startExe);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = startExe,
            Arguments = BuildCreateBoxArguments(boxName),
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Could not start Sandboxie Start.exe.");
        // cmd /c exit terminates immediately; the box persists afterwards.
        if (!process.WaitForExit(30000))
            throw new TimeoutException($"Timed out creating Sandboxie box '{boxName}'.");
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Sandboxie box creation failed with exit code {process.ExitCode}.");
    }

    public static IReadOnlyList<string> ValidateArgumentsTemplate(string? template)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(template))
            return warnings;
        // Detect likely-unintended curly-brace tokens other than the two supported placeholders.
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
                break;
            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                warnings.Add("Unclosed '{' in arguments template.");
                break;
            }
            var token = template.Substring(open, close - open + 1);
            if (!string.Equals(token, "{username}", StringComparison.Ordinal) &&
                !string.Equals(token, "{profile}", StringComparison.Ordinal))
            {
                warnings.Add($"Unsupported placeholder '{token}'. Supported: {{username}}, {{profile}}.");
            }
            i = close + 1;
        }
        return warnings;
    }

    private static string ExpandArguments(string? template, CamfrogAccount account) =>
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
