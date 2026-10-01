# Wire protocol

One WebSocket per game, to `wss://<relay>/ws`. Every frame is a single UTF-8 JSON
object with a `type`. Anything else is dropped. Binary frames are dropped.

Limits the relay enforces (the plugin mirrors the display-side ones):

| Limit | Value |
| --- | --- |
| Frame size, either direction | 2,000 bytes in, 16 KB out (history) |
| Message text, after cleaning | 300 code points |
| Display name | 32 code points |
| Rate | 5 messages per 10 s per connection (per user from phase 3) |
| Connections per IP | 4 |

## Plugin -> relay

```jsonc
{ "type": "auth", "token": "<session token>" }   // must be the first frame (phase 3)
{ "type": "say",  "text": "anyone got a LEDX?" }
```

The token is only ever sent in this frame -- never in the URL, which ends up in logs.

## Relay -> plugin

```jsonc
{ "type": "msg",     "from": "Scoop", "tag": "4f2a", "text": "...", "at": 1759350000000 }
{ "type": "history", "messages": [ /* msg objects, oldest first */ ] }   // phase 4
{ "type": "welcome", "name": "Scoop", "tag": "4f2a" }                     // after auth, phase 3
{ "type": "error",   "reason": "rate_limited" | "too_long" | "not_signed_in" | "banned" | "bad_frame" }
```

- `from` and `tag` are set by the relay. A client never chooses its own name.
- `tag` is the last four hex digits of a hash of the Discord id, so lookalike names
  are told apart. Anonymous (phase 1 only) messages carry `tag: "anon"`.
- `at` is the relay's clock, in Unix milliseconds.

## Login (phase 3, HTTP)

```
POST /login/start   -> { loginUrl, pollSecret, code, expiresAt }
GET  <loginUrl>     -> browser: shows `code`, then redirects to Discord
GET  /login/callback?code=..&state=<loginId>   (Discord redirects here)
POST /login/poll    { pollSecret } -> 202 pending | 200 { token, name, tag } | 410 expired
```

`loginId` (in the URL the player sees) and `pollSecret` (never leaves the plugin) are
different random values on purpose: a streamer showing the login page must not hand
their session to viewers. See SECURITY.md.
