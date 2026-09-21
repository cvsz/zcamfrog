namespace CamfrogMultiID.Core;

public sealed class CamfrogAccount
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordSecretName { get; set; } = string.Empty;
    public string ProfileDirectory { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Status { get; set; } = "Stopped";
    public int? ProcessId { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public string ProcessExecutablePath { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public string RoomUrl { get; set; } = string.Empty;
    public bool AutoRestart { get; set; }
    public DateTime? PasswordChangedUtc { get; set; }
}

public sealed class AppSettings
{
    public string ClientExecutable { get; set; } = string.Empty;
    public string ClientArgumentsTemplate { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public bool UseSandboxie { get; set; }
    public string SandboxieStartExe { get; set; } = string.Empty;
    public int AutoBackupDays { get; set; }
    public int AutoBackupKeepCount { get; set; } = 4;
    public DateTime? LastAutoBackupUtc { get; set; }
    public string Language { get; set; } = "en";
}
