# Camfrog Client Integration Surface

Verified by binary recon (Unicode strings) of
`Camfrog Video Chat.exe` (Aug 2026 build, CEF-based native client).
No reverse-engineering of protocols; this catalogs only the client's
own documented-by-construction surface.

## Launch

- Single switch: `--url="%1"` (from the registered `camfrog:` handler).
- No auto-login, credential, or headless switches exist.
- Single-instance per Windows session → sandboxed boxes required
  (see `sandboxie.md`).

## Protocol links (`camfrog:`)

| Link | Purpose |
|---|---|
| `camfrog://join_room/?name=[ROOM_NAME]` | Room auto-join (used by the manager) |
| `camfrog:join:` / `ecamfrog:join:` | Legacy join verbs |
| `camfrog:add:` / `camfrog:im:` / `camfrog:userprofile:` | Contacts, IM, profiles |
| `camfrog:register` | Registration |
| `camfrog://gift/...`, `open_gift_store`, `open_sticker_store` | Paid gifts (view only) |
| `camfrog://open_url/?url=` | External links |

## Backend hosts (connectivity diagnostics only)

- `api-desktop.camfrog.com/json/rpc.php` (token auth, user-scoped)
- `videochat.camfrog.com`, `room-history.camfrogcdn.com`
- Login: Camfrog account, Google OAuth, Facebook OAuth

## In-room chat commands (human-typed, not scriptable)

`/addfriend /kick /banlist /blockmic /invisible /clearbl /clearol
/featured`. The manager never sends chat.

## Permanent boundaries

- No credential injection or auto-login automation.
- No access-token scraping or use.
- No auth/licensing/CAPTCHA bypass.
- First login per box is manual (with remember-me); auto-join works
  on subsequent launches.
