// The Worker: the only public entry point. It routes, and nothing else -- every check
// on a chat message happens in the Room, where it cannot be skipped.

import { Room } from "./room";

export { Room };

export interface Env {
  ROOM: DurableObjectNamespace<Room>;
  // "true" lets a connection speak without signing in. Phase 1 only: it must be
  // "false" before the relay is announced to anyone. See CLAUDE.md, phase 3.
  ALLOW_ANONYMOUS: string;
}

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    const url = new URL(req.url);

    if (url.pathname === "/ws") {
      if (req.headers.get("Upgrade") !== "websocket") {
        return new Response("websocket only", { status: 426 });
      }
      // One room for everyone. A channel later is another idFromName.
      const room = env.ROOM.get(env.ROOM.idFromName("global"));
      return room.fetch(req);
    }

    if (url.pathname === "/health") return new Response("ok");

    // Phase 3: /login/start, /login/:id, /login/callback, /login/poll.

    return new Response("not found", { status: 404 });
  },
} satisfies ExportedHandler<Env>;
