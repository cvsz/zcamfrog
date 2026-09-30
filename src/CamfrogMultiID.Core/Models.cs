namespace CamfrogMultiID.Core;

/// <summary>
/// Observable client state for one account. Every value maps to evidence the
/// manager can read locally; nothing here claims server-side Camfrog state.
/// The list is documented in docs/account-status.md and is the single source
/// of truth for the dashboard and the details pane.
/// </summary>
public enum AccountRuntimeState
{
  /// <summary>No tracked wrapper and no attributable client process.</summary>
  Offline = 0,
  /// <summary>Tracked wrapper is alive but no client process is attributable yet.</summary>
  Starting = 1,
  /// <summary>Client process attributable, but no window has been realized (login pending).</summary>
  AwaitingLogin = 2,
  /// <summary>Client running with a window, and no room is configured for the account.</summary>
  Idle = 3,
  /// <summary>This account's own client was launched with its configured room link.</summary>
  RoomRequested = 4,
  /// <summary>A room is configured, but this account's client did not receive the link.</summary>
  RoomNotObserved = 5,
  /// <summary>Client process survives, but the tracked wrapper is gone (wrapper crash/exit).</summary>
  Orphaned = 6,
  /// <summary>Evidence could not be read (access denied, WMI failure). Fails closed.</summary>
  Unknown = 7,
}

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
  // Transient UI-only view state. Never persisted: no reader/writer touches these.
  public AccountRuntimeState RuntimeState { get; set; } = AccountRuntimeState.Offline;
  public string PresenceDisplay { get; set; } = string.Empty;
  public string RoomDisplay { get; set; } = string.Empty;
  public string StateEvidence { get; set; } = string.Empty;
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
  public bool AutoStartAccounts { get; set; }
}
