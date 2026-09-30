# Account runtime status

This file is the specification. The dashboard, the account grid, and the
details pane all render values derived from the list below; nothing in the UI
invents a state that is not listed here.

Implementation: `AccountRuntimeState` in
`src/CamfrogMultiID.Core/Models.cs` and
`ProcessSessionService.ResolveRuntimeStates` in
`src/CamfrogMultiID.Infrastructure/Services.cs`. The resolver is a pure
function, so the whole matrix is covered by table-driven tests in
`tests/CamfrogMultiID.Tests/AccountManagementTests.cs`.

## Evidence inputs

Each refresh takes one WMI sweep of `Win32_Process` plus a managed-process
window-title read:

| Input | Source | Failure behavior |
| --- | --- | --- |
| Client processes (PID, parent PID, command line, start time) | `GetClientProcessSnapshot` | `null` snapshot, every account becomes `Unknown` |
| Parent map for every process | same sweep | required for attribution |
| Window title per client PID | `Process.MainWindowTitle` | best effort, empty means "no window" |
| Tracked wrapper liveness | `IsTrackedProcessAlive` (PID + start time + executable) | not in the live set |

## State list

| State | Shown as | Condition |
| --- | --- | --- |
| `Offline` | Offline | No tracked PID recorded, or the tracked wrapper is gone and no client process is attributable |
| `Starting` | Starting | Tracked wrapper is alive but no client process is attributable yet |
| `AwaitingLogin` | Awaiting login | Client attributable, no window realized. Sandboxie boxes need one manual login each |
| `Idle` | Online (no room) | Client attributable with a window, and no room is configured |
| `RoomRequested` | Room link sent | The room link is present on **this account's own** client command line |
| `RoomNotObserved` | Room link not seen | A room is configured, but this account's client command line does not contain the link |
| `Orphaned` | Orphaned client | A client process is attributable while the tracked wrapper is no longer alive |
| `Unknown` | Unknown | Evidence could not be read. Fails closed: never reported as offline |

`Untracked clients` is not a per-account state. When a client process belongs
to no account (started by hand, or a stale PID), the window shows a banner
with the count and leaves every account state untouched.

## What a state does not claim

- `RoomRequested` proves the manager passed the link to that client. It does
  **not** prove the client authenticated, joined the server-side room, or is
  visible to other members. Camfrog exposes no local API for that.
- No state claims paid status, nickname color, or subscription state; those
  are server-side and out of scope.
- `AwaitingLogin` is expected on first launch of a new Sandboxie box, not an
  error.

## Attribution rule

A client process belongs to the account whose tracked `ProcessId` appears in
the client's ancestry (up to 16 hops, cycle-guarded). This is what stops two
accounts configured with the same room link from claiming each other's
client: the older implementation matched any live command line against the
room URL and reported both as joined.

Limitations to keep in mind when reading a state:

- Parent PIDs are static metadata. A recycled PID can produce a false
  ancestry match, which is why liveness of the tracked wrapper is checked
  separately and why `Orphaned` exists.
- When two accounts are launched without Sandboxie, the client is
  single-instance per Windows session: the second launch hands off and exits,
  so only one account can be `Idle` at a time. Use Sandboxie boxes for
  concurrency.
