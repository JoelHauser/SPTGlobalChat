# Security

SPT Global Chat connects a single-player game to a server on the internet and shows
strangers' text on players' screens. This file is the threat model it is built
against and the checks every release must pass.

## Reporting a problem

Open a GitHub issue **without** details for anything exploitable, and ask for a
private channel. Do not post a working exploit in public.

## What is protected

| Asset | Why |
| --- | --- |
| Players' computers | The plugin runs inside their game and reads untrusted text. |
| Players' identities | Nobody posts as someone else. |
| Session tokens | A stolen token posts as its owner. |
| Other players' screens | One message must not break, freeze or flood anyone's UI. |
| The relay and the operator's accounts | Whoever holds them holds the chat. |
| Players' data | Keep as little as possible; say what is kept. |

## The rules, by boundary

### The player's machine (plugin)

- Outbound only. The plugin never listens on a port.
- Incoming text is displayed and nothing else: no commands, no clickable links,
  nothing passed to the game or the SPT server.
- JSON is read into fixed classes; `TypeNameHandling` is `None` and metadata
  properties are ignored (`Wire.cs`).
- Frames over 16 KB, and binary frames, drop the connection.
- Text is cleaned again before display (`TextSafety.cs`), and rich text is turned off
  on the components that draw it (phase 2), so a compromised relay still cannot
  inject markup.

### The connection

- `wss://` only, to an address fixed in the build. Debug builds may point at
  localhost; release builds have no setting at all.
- **Certificates are checked by the plugin's own callback** (`RelayConnection.cs`).
  websocket-sharp's default accepts every certificate, and .NET's process-wide
  callback can be loosened by any other mod, so neither is relied on.
- The session token travels only in the first frame -- never in a URL, never in a
  cookie (so a website in the player's browser cannot ride on it).

### Login (phase 3)

- Discord OAuth, `identify` scope only. Discord's access token is discarded once the
  id and name are read.
- Two random values per login: the **login id** in the URL the player sees, and the
  **poll secret** that never leaves the plugin. Seeing the page is not enough to
  collect the token.
- A **confirmation code** shown in game and on the page, against the phishing attack
  where someone sends a player a login they started themselves. Logins expire in
  5 minutes and are single-use.
- Session tokens: 32 random bytes, stored only as SHA-256, expiring, revocable
  ("log out everywhere"). On the player's machine: DPAPI-encrypted, never logged,
  never in the BepInEx config.

### Text

Cleaned on the relay before storing or sending, and again in the plugin before drawing,
by two implementations held to one set of cases (`docs/clean-cases.json`):

- lone surrogates, C0/C1 controls, zero-width characters, line/paragraph separators,
  bidi embeds/overrides/isolates (U+202E shows `gnp.exe` as `exe.png`), word joiners,
  the BOM, and `<` `>`;
- line breaks become spaces, whitespace runs collapse;
- at most two combining marks per character (no "Zalgo");
- 300 code points per message, 32 per name, never splitting a surrogate pair.

Names come from the relay, never the client, with a tag derived from the Discord id so
lookalike names are told apart.

### The relay

- Checks in order: size, JSON, type, signed in, banned/muted, rate, clean, broadcast.
- Rate limits per connection (per user from phase 3), connections per IP (4), logins
  started per IP.
- IPs are used, hashed, only for those limits, and never stored.
- Runs on the Cloudflare free plan, which stops at its limits instead of billing --
  so a flood cannot run up a bill.

### The operator

- Two-factor authentication on Cloudflare, Discord and GitHub (not SMS).
- Secrets only in `wrangler secret`; `.dev.vars` is git-ignored.
- Admins are Discord ids in an environment variable, not settable from chat; admin
  actions are logged.
- Each release publishes the plugin DLL's SHA-256.

## Kept, and not

- **Kept:** Discord id, display name, the last 100 messages, the ban list, session
  hashes.
- **Not kept:** IP addresses, SPT profile data, Discord access tokens.
- `/forgetme` deletes a player's sessions and messages (phase 4).
- The operator can read all chat. It is a public room; there is nothing to encrypt
  end to end.

## Not prevented, and said so

- Malware running as the player can read their token. DPAPI stops other Windows
  users, not other programs of the same user.
- Scam links typed as plain text. Moderation and reports handle those.
- Trolls with aged Discord accounts. Mutes and bans handle those.

## Release checklist

1. In game, the plugin **fails** to connect to `wss://self-signed.badssl.com/` and
   `wss://expired.badssl.com/`, and **succeeds** against the real relay.
2. A 1 MB frame from `wscat` drops the connection; nothing crashes.
3. Rich-text tags, U+202E and Zalgo text arrive as harmless plain text.
4. Sending without signing in is refused; a reused or expired login is refused.
5. Opening someone else's login URL cannot collect their token.
6. Spam from `wscat` is throttled; a banned id cannot send.
7. The token string appears in no log and no config file.
8. `.dev.vars` is not in the git history; `ALLOW_ANONYMOUS` is `"false"`.
