# SPT Global Chat

**Shelved (2026-10-01), never released -- nothing here is ready to install.** Phase 1
is written and tested but has never run: see CLAUDE.md for where it stopped.

A chat box for SPT that talks to every other player running the mod: docked on the
main menu, and a small overlay in raid.

## How it works

Each game connects to one relay server on the internet. When you send a line, the
relay checks it and passes it to everyone connected. Players never connect to each
other, and nobody sees anyone else's IP address. If the relay is down, chat stops;
the game is unaffected.

```
your game  --wss-->  relay  --wss-->  everyone else's game
```

## Privacy

- **Off until you turn it on.** Nothing connects until you set `Enabled` in the
  config.
- **Sent:** your Discord display name (once you sign in) and what you type.
- **Not sent:** your SPT profile, your items, your IP to other players.
- **Kept on the relay:** the last 100 messages, your Discord id and name, bans.
- The relay's operator can read the chat. It is a public room.

See [SECURITY.md](SECURITY.md) for the full threat model.

## Layout

```
relay/    Cloudflare Worker + Durable Object (TypeScript)
plugin/   BepInEx client plugin (C#, net472)
tests/    plugin tests (xunit)
docs/     wire protocol, and the text-cleaning cases both halves are tested against
```
