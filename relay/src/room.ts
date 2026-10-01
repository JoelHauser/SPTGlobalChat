// The one chat room. Holds every open connection through the WebSocket Hibernation
// API, so idle time is not billed, and repeats each accepted message to everyone.
//
// Order of checks on every frame, cheapest first, and nothing is trusted from the
// client: size -> JSON -> type -> signed in -> rate -> clean -> broadcast.

import { DurableObject } from "cloudflare:workers";
import { cleanText } from "./clean";
import type { Env } from "./index";

const MAX_FRAME_BYTES = 2_000;
const MAX_CONNECTIONS_PER_IP = 4;

// Token bucket: BURST messages at once, refilled at one per REFILL_MS.
const BURST = 5;
const REFILL_MS = 2_000;

// Kept on the socket with serializeAttachment, so it survives hibernation.
interface Session {
  name: string | null;   // null until signed in (phase 3)
  tag: string;
  tokens: number;
  last: number;
}

export class Room extends DurableObject<Env> {
  async fetch(req: Request): Promise<Response> {
    // The IP is used only as a hashed tag for the per-IP cap, held in memory with the
    // socket, and never written to storage or logs.
    const ipTag = await hashTag(req.headers.get("CF-Connecting-IP") ?? "unknown");
    if (this.ctx.getWebSockets(ipTag).length >= MAX_CONNECTIONS_PER_IP) {
      return new Response("too many connections", { status: 429 });
    }

    const pair = new WebSocketPair();
    const [client, server] = [pair[0], pair[1]];

    this.ctx.acceptWebSocket(server, [ipTag]);
    const session: Session = { name: null, tag: "anon", tokens: BURST, last: Date.now() };
    server.serializeAttachment(session);

    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws: WebSocket, raw: string | ArrayBuffer): Promise<void> {
    if (typeof raw !== "string" || raw.length > MAX_FRAME_BYTES) {
      return reject(ws, "bad_frame");
    }

    let frame: unknown;
    try {
      frame = JSON.parse(raw);
    } catch {
      return reject(ws, "bad_frame");
    }
    if (typeof frame !== "object" || frame === null) return reject(ws, "bad_frame");

    const session = ws.deserializeAttachment() as Session;
    const type = (frame as { type?: unknown }).type;

    if (type === "auth") {
      // Phase 3: look up the SHA-256 of the token, check bans, set name and tag,
      // send "welcome" and then "history".
      return reject(ws, "not_signed_in");
    }

    if (type !== "say") return reject(ws, "bad_frame");

    if (session.name === null && this.env.ALLOW_ANONYMOUS !== "true") {
      return reject(ws, "not_signed_in");
    }

    if (!takeToken(session)) {
      ws.serializeAttachment(session);
      return reject(ws, "rate_limited");
    }
    ws.serializeAttachment(session);

    const text = cleanText((frame as { text?: unknown }).text);
    if (!text) return;

    const out = JSON.stringify({
      type: "msg",
      from: session.name ?? "anon",
      tag: session.tag,
      text,
      at: Date.now(),
    });

    // Phase 4: append to the history table before broadcasting.
    for (const peer of this.ctx.getWebSockets()) {
      try {
        peer.send(out);
      } catch {
        // A socket closing mid-broadcast is not this message's problem.
      }
    }
  }

  async webSocketClose(ws: WebSocket, code: number): Promise<void> {
    try {
      ws.close(code === 1005 ? 1000 : code, "closing");
    } catch {
      // Already closed.
    }
  }

  async webSocketError(ws: WebSocket): Promise<void> {
    try {
      ws.close(1011, "error");
    } catch {
      // Already closed.
    }
  }
}

function takeToken(s: Session): boolean {
  const now = Date.now();
  s.tokens = Math.min(BURST, s.tokens + (now - s.last) / REFILL_MS);
  s.last = now;
  if (s.tokens < 1) return false;
  s.tokens -= 1;
  return true;
}

function reject(ws: WebSocket, reason: string): void {
  try {
    ws.send(JSON.stringify({ type: "error", reason }));
  } catch {
    // Already closed.
  }
}

async function hashTag(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value));
  const hex = [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, "0")).join("");
  return "ip:" + hex.slice(0, 16);
}
