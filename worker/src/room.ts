// One Durable Object per room: the lobby, the entry fees, the 15 Hz state relay, the server-timed countdown, the
// finish order, cup points across races, and the prize settlement. It is the only place that decides who won and who
// gets paid, which is why it is one object and not a table plus a race condition.
//
// Protocol (JSON text frames). Client → room:
//   hello {device, session?, character, kart}   ready {ready}   start   paying   pay {signature, round}   loaded
//   s {p,r,v,st,lap,cp,sc,tm}  (relayed to everyone else with the sender's id)   fin
//   ping {c}   spr {s} (echo of the room's sp)   next   again   leave   kick {id}
// Room → client:
//   welcome {id, serverNow, room}   room {…snapshot}   load {track, race, races}   countdown {startAt}
//   s {id,…}   finished {id, ms}   results {…}   payouts [...]   pong {c, s}   sp {s} (the room timing a round trip)   error {msg}   closed {reason}
// HTTP from the Worker: POST /init, GET /state, POST /entry {wallet, signature, round} (an entry payment by any route).
import { DurableObject } from 'cloudflare:workers';
import type { Env } from './index';
import { readSession, isSignature } from './auth';
import { FEE_TIERS, MIN_PAID_PLAYERS, splitPool, CATALOG, type Currency, type Split } from './catalog';
import { verifyPayment, buildBatch, sendRaw, txStatus, landedAny, findEntrySignatures, type EntryCheck } from './payout';
import { geoOf, newRef, push, finishT, finished, crossingsToFinish, type RefState } from './referee';

const POINTS = [10, 8, 6, 4, 3, 2];
export const MAX_PLAYERS = 6;
const MAX_SOCKETS = 24;                // sockets per room; when full, the oldest that never said hello is closed
const HELLO_WAIT_MS = 10_000;          // a socket that has not said hello by then is closed
const MAX_FRAME = 4096;                // no frame of ours comes near this; a padded one is dropped unread
// Every frame costs the object a turn, so every frame counts: a phone sends 15 states a second plus a ping and an echo
// every 2 s, and after an 8 s stall it re-sends that much at once. A socket that goes past this is closed.
const FRAME_BURST = 600, FRAME_PER_S = 40;   // over it a frame is dropped (never a fin); a socket twice over is closed
const CONTROL_BURST = 10, CONTROL_PER_S = 2;   // frames that write storage, the database or the chain, per socket
const LOBBY_TTL_MS = 30 * 60_000;      // a room nobody starts
const EMPTY_TTL_MS = 5 * 60_000;       // a room everybody left
const LOAD_TIMEOUT_MS = 30_000;        // slow phone loading the track
const COUNTDOWN_LEAD_MS = 4_500;       // GET READY buffer + 3-2-1
const FINISH_GRACE_MS = 60_000;        // after the first finisher, the rest have this long
const RACE_CAP_MS = 6 * 60_000;
const RESULTS_MS = 25_000;             // between races of a cup: the host may go on sooner; after this the room does
const CLOCK_AHEAD_S = 1.0;             // after GO a state's race clock may run this far ahead of the room's (clock fit)
const CLOCK_BEHIND_S = 10;             // ...and this far behind (a queued frame after a stall) before it is ignored
// A phone's race clock that runs late would make every time it reports short: a free head start. The room times each
// racer's round trip (sp/spr, only its own sp answered counts) and allows half the best one (at most a second) of lag,
// plus this. The lag is the least seen over the LAG_SPAN_MS before the finishing crossing, judged at that crossing:
// not over the whole race (a clock that starts right and runs slow is late by the end), not over the last moment (a
// stall at the line delays every state in it, honestly), and not later (an on-time state after the line would hide it).
const CLOCK_LAG_SLACK_S = 1.0, RTT_CAP_MS = 2000;
const LAG_BUCKET_MS = 5_000, LAG_SPAN_MS = 20_000;
const SP_EVERY_MS = 2000;
const MIN_SAMPLE_S = 0.02;             // the referee skips a state less than this after the last (phones send 15 Hz)
// Entry payments: asked about every few seconds while the chain has not seen them (a transfer that has not landed in
// ENTRY_TIMEOUT_MS never will: its blockhash has expired), and for up to a day with backoff while the RPC is failing,
// which says nothing about the payment.
const ENTRY_RETRY_MS = 4_000, ENTRY_TIMEOUT_MS = 120_000, ENTRY_GIVE_UP_MS = 24 * 3_600_000;
// A seat is held (no kick, no drop) while its phone is in the wallet app, and while a payment for it is being checked:
// this many times per wallet per round, then a payment no longer holds the seat (it is still verified and refunded).
const PAYING_HOLD_MS = 120_000, PAYING_HOLDS = 3;
const MAX_ORPHANS = 64, MAX_ORPHANS_PER_WALLET = 4;
const MAX_REFUNDS = 2;                 // pay, leave for a refund: this many times per wallet per round; a third pay is refunded, not seated
// Payouts: one recipient per transaction, saved as 'sent' with its signature before it goes out. A few quick tries,
// then back off (an RPC outage, an empty fee balance) for about half a day; only then is a row 'failed' for a person.
const SETTLE_RETRY_MS = 3_000, QUICK_SENDS = 4, MAX_BACKOFF_MS = 30 * 60_000, PAYOUT_GIVE_UP_MS = 12 * 3_600_000;

type State = 'lobby' | 'loading' | 'countdown' | 'racing' | 'results' | 'finished' | 'cancelled';

/** An entry payment the room has been told about, being verified: a seat's (Player.pending) or an orphan's. */
type Entry = { wallet: string; sig: string; round: number; since: number; nullSince?: number; nextTry?: number; tries?: number; claiming?: boolean };

type Player = {
  id: string; wallet: string | null; device: string; name: string;
  character: number; kart: string;
  joinedAt: number; host: boolean; connected: boolean; ready: boolean; left?: boolean;
  paid: boolean; paidSig?: string; pending?: Entry; payingUntil?: number;
  loaded: boolean; racing: boolean;
  finishMs?: number; dnf: boolean; lastState?: number;
  ref?: RefState; flagged?: string;      // the referee's view of this race, and why it stopped believing the drive
  // the race clock's least lag behind the room (s) per LAG_BUCKET_MS [start ms, lag]; the round trip: the sp waiting
  // for its echo, the best round trip (ms); finLag: the clock judged at the finishing crossing ('' = fine)
  lags?: number[][]; spOut?: number[]; rttMin?: number; lastSp?: number; finLag?: string;
  flagAt?: number[];                     // [gates, lap distance] when the referee flagged it: a caught cheat ranks no further
  place?: number; points: number; totalMs: number; wins: number;
};

type Payout = {
  id: string; wallet: string; kind: 'prize' | 'refund' | 'host'; place?: number;
  amount: string; currency: Currency; status: 'pending' | 'sent' | 'confirmed' | 'failed';
  signature?: string; attempts: number; nextTry?: number; lastValidBlockHeight?: number; round: number; batch: string; created: number;
  sigs?: string[];                       // every transaction ever sent for this row: none may have landed before another is sent
};

type Order = { id: string; name: string; ms: number | null; place: number; points: number; dnf: boolean };
type RaceResult = { race: number; races: number; track: string; final: boolean; order: Order[] };
type Standing = { id: string; name: string; points: number; wins: number; totalMs: number; place: number };

type Room = {
  code: string; createdAt: number; round: number;
  hostId: string; organiser?: string; tracks: string[]; raceIndex: number;
  fee: string; currency: Currency; cutBps: number; isPublic: boolean;
  state: State; players: Record<string, Player>;
  startAt?: number; loadDeadline?: number; finishDeadline?: number; emptySince?: number; nextAt?: number;
  lastResults?: RaceResult; standings?: Standing[];
  payouts: Payout[];
  entrySigs?: string[];                  // every entry signature this round accepted: one payment, one seat, once
  // Entry payments that cannot buy a seat (their payer left, was kicked or removed while paying, the room moved on,
  // or the seat is paid already): verified on, and refunded if they landed. The money is never kept for nothing.
  orphans?: Entry[];
  kicked?: string[];                     // removed by the host this round: they may not come back until the rematch
  refunds?: Record<string, number>;      // entry refunds per wallet this round
  holds?: Record<string, number>;        // seat holds (paying, or a payment being checked) per wallet this round
  members?: string[];                    // every wallet ever seated here: only they can leave a payment with the room
  // Wallets that said they were paying: when the hold ends with no payment delivered (the game was killed while the
  // wallet app was open), the room looks for the transfer on-chain itself, by its memo.
  watch?: { wallet: string; round: number; at: number }[];
};

const nowMs = () => Date.now();
const short = (w: string) => `${w.slice(0, 4)}…${w.slice(-4)}`;
const num = (v: unknown) => { const n = Number(v); return Number.isFinite(n) ? n : 0; };
const nums = (v: unknown, n: number) => Array.isArray(v) && v.length >= n ? v.slice(0, n).map(num) : undefined;

export class RoomDO extends DurableObject<Env> {
  room: Room | null = null;
  // In memory only: gone with the object, which also ends anything they guard.
  verifying = new Set<string>();         // entry signatures being checked right now: never twice at once
  greeting = new WeakSet<WebSocket>();   // a hello in flight on this socket
  gone = new WeakSet<WebSocket>();       // closed: a hello still in flight on it must not seat anyone
  frames = new WeakMap<WebSocket, { n: number; at: number }>();
  buckets = new WeakMap<WebSocket, { n: number; at: number }>();
  starting = false;                      // start() is between its first await and the load going out

  // ---------- HTTP: init/state/entry from the Worker, WebSocket upgrade from the phones ----------
  async fetch(req: Request): Promise<Response> {
    const url = new URL(req.url);
    await this.load();
    if (req.headers.get('Upgrade') === 'websocket') {
      if (!this.room) return new Response('no such room', { status: 404 });
      if (!this.makeRoomForSocket()) return new Response('room is full', { status: 429 });
      const pair = new WebSocketPair();
      const [client, server] = Object.values(pair);
      this.ctx.acceptWebSocket(server);
      // Where this phone is, as Cloudflare saw it: paid rooms are refused from blocked jurisdictions (research/20).
      server.serializeAttachment({ id: null, geo: req.headers.get('x-kc-geo') ?? '', at: nowMs() });
      return new Response(null, { status: 101, webSocket: client });
    }
    if (url.pathname === '/init' && req.method === 'POST') {
      if (this.room) return Response.json({ error: 'exists' }, { status: 409 });
      const b = await req.json<any>();
      this.room = {
        code: b.code, createdAt: nowMs(), round: 1, hostId: b.host, organiser: b.host, tracks: b.tracks, raceIndex: 0,
        fee: b.fee, currency: b.currency, cutBps: b.cutBps, isPublic: !!b.isPublic, state: 'lobby', players: {}, payouts: [],
      };
      await this.save();
      await this.schedule();
      return Response.json(this.snapshot());
    }
    if (url.pathname === '/entry' && req.method === 'POST') {
      if (!this.room) return Response.json({ error: 'no such room' }, { status: 404 });
      const b = await req.json<any>().catch(() => ({}));
      // scan: the phone paid (or may have) but never learned the signature (killed while the wallet app was open)
      const out = b.scan ? await this.scan(String(b.wallet ?? ''), Number(b.round)) : await this.intake(String(b.wallet ?? ''), String(b.signature ?? ''), Number(b.round));
      await this.save(); this.broadcastRoom(); await this.schedule();
      return Response.json({ entry: out }, { status: out === 'refused' ? 400 : out === 'busy' ? 429 : 200 });
    }
    if (url.pathname === '/state') {
      if (!this.room) return Response.json({ error: 'no such room' }, { status: 404 });
      if (this.owes()) await this.schedule();              // a room nobody visits still pays what it owes
      return Response.json(this.snapshot());
    }
    return new Response('not found', { status: 404 });
  }

  async load() { if (!this.room) this.room = (await this.ctx.storage.get<Room>('room')) ?? null; }
  async save() { if (this.room) await this.ctx.storage.put('room', this.room); }

  /** Close sockets that never said hello (older than HELLO_WAIT_MS, or the oldest if the room is full). */
  makeRoomForSocket(): boolean {
    const t = nowMs();
    const idle = this.ctx.getWebSockets().map(s => ({ s, a: (() => { try { return s.deserializeAttachment(); } catch { return null; } })() }))
      .filter(x => !x.a?.id).sort((x, y) => (x.a?.at ?? 0) - (y.a?.at ?? 0));
    for (const x of idle) if (t - (x.a?.at ?? 0) > HELLO_WAIT_MS) { try { x.s.close(1008, 'no hello'); } catch { /* gone */ } }
    let open = this.ctx.getWebSockets().filter(s => s.readyState === WebSocket.OPEN).length;
    for (const x of idle) {
      if (open < MAX_SOCKETS) break;
      if (x.s.readyState !== WebSocket.OPEN || this.greeting.has(x.s)) continue;
      try { x.s.close(1008, 'room is full'); } catch { /* gone */ }
      open--;
    }
    return open < MAX_SOCKETS;
  }

  // ---------- sockets ----------
  async webSocketMessage(ws: WebSocket, message: string | ArrayBuffer) {
    const over = this.budget(ws);
    if (over === 'close') { try { ws.close(1008, 'too many messages'); } catch { /* gone */ } return; }
    if (typeof message !== 'string' || message.length > MAX_FRAME) return;
    let m: any;
    try { m = JSON.parse(message); } catch { return; }
    if (!m || typeof m !== 'object') return;
    if (over === 'drop' && m.t !== 'fin') return;                    // past the budget: dropped, but never a finish
    await this.load();
    const room = this.room;
    if (!room) { this.send(ws, { t: 'error', msg: 'Room is gone' }); ws.close(1000, 'gone'); return; }
    const id: string | null = ws.deserializeAttachment()?.id ?? null;
    const p = id ? room.players[id] : undefined;

    if (m.t === 'ping') { this.send(ws, { t: 'pong', c: m.c, s: nowMs() }); return; }
    if (!p) {                                                             // before hello: a hello, or nothing
      if (m.t === 'hello' && this.allow(ws)) await this.hello(ws, m);
      return;
    }
    if (m.t === 'spr') {                                                  // hot path: no save, no broadcast
      // Only the echo of an sp the room sent and has not had answered: a made-up one could claim any round trip. A
      // late echo still counts, up to the cap the allowance stops at anyway.
      const s0 = Number(m.s), i = p.spOut?.indexOf(s0) ?? -1;
      if (i >= 0) {
        p.spOut!.splice(i, 1);
        const rtt = Math.min(nowMs() - s0, RTT_CAP_MS);
        if (rtt >= 0) p.rttMin = Math.min(p.rttMin ?? rtt, rtt);
      }
      return;
    }
    if (m.t === 's') { await this.relay(ws, p, m); return; }              // hot path: no save, no broadcast
    // Everything else writes storage, the database or the chain: a few a second per socket is plenty for a person.
    if (m.t !== 'fin' && !this.allow(ws)) return;

    switch (m.t) {
      case 'hello': await this.hello(ws, m); return;
      case 'ready': if (room.state !== 'lobby' || p.ready === !!m.ready) return; p.ready = !!m.ready; break;
      case 'paying': {
        if (m.off) { if (!p.payingUntil) return; p.payingUntil = undefined; break; }   // declined, or not enough funds
        // the phone is about to open the wallet app: hold the seat until the payment arrives (or two minutes pass)
        if (room.state !== 'lobby' || room.fee === '0' || p.paid || !p.wallet || !this.takeHold(p.wallet)) return;
        p.payingUntil = nowMs() + PAYING_HOLD_MS;
        room.watch = [...(room.watch ?? []).filter(w => !(w.wallet === p.wallet && w.round === room.round)), { wallet: p.wallet, round: room.round, at: p.payingUntil }];
        break;
      }
      case 'pay': if (!p.wallet) return; await this.intake(p.wallet, String(m.signature ?? ''), Number(m.round ?? room.round), ws); break;
      case 'start': if (!(await this.start(ws, p))) return; break;
      case 'loaded': await this.loaded(p); break;
      case 'fin': if (!(await this.finish(ws, p))) return; break;
      case 'next': if (p.host && room.state === 'results') await this.beginLoading(room.raceIndex + 1); else return; break;
      case 'again': if (p.host && (room.state === 'finished' || room.state === 'cancelled')) this.reset(); else return; break;
      case 'leave': await this.remove(p, 'left'); ws.close(1000, 'left'); break;
      case 'kick': {
        if (!p.host || room.state !== 'lobby') return;
        const target = room.players[String(m.id)];
        if (!target || target.id === p.id) return;
        if (this.paying(target)) { this.send(ws, { t: 'error', msg: `${target.name} is paying right now` }); return; }
        room.kicked = [...(room.kicked ?? []), target.id];
        await this.remove(target, 'kicked');
        break;
      }
      default: return;
    }
    await this.save();
    this.broadcastRoom();
    await this.schedule();
  }

  /** A token bucket per socket over every frame: past it frames are dropped, and a socket far past it is closed. */
  budget(ws: WebSocket): 'ok' | 'drop' | 'close' {
    const t = nowMs(), b = this.frames.get(ws) ?? { n: FRAME_BURST, at: t };
    b.n = Math.min(FRAME_BURST, b.n + (t - b.at) * FRAME_PER_S / 1000); b.at = t;
    this.frames.set(ws, b);
    b.n -= 1;
    return b.n >= 0 ? 'ok' : b.n < -FRAME_BURST ? 'close' : 'drop';
  }

  /** A seat hold for this wallet this round, if it has any left. */
  takeHold(wallet: string): boolean {
    const room = this.room!, used = room.holds?.[wallet] ?? 0;
    if (used >= PAYING_HOLDS) return false;
    room.holds = { ...(room.holds ?? {}), [wallet]: used + 1 };
    return true;
  }

  /** A token bucket per socket over the frames that cost something. */
  allow(ws: WebSocket): boolean {
    const t = nowMs(), b = this.buckets.get(ws) ?? { n: CONTROL_BURST, at: t };
    b.n = Math.min(CONTROL_BURST, b.n + (t - b.at) * CONTROL_PER_S / 1000); b.at = t;
    this.buckets.set(ws, b);
    if (b.n < 1) return false;
    b.n -= 1;
    return true;
  }

  async webSocketClose(ws: WebSocket) { this.gone.add(ws); await this.dropped(ws); }
  async webSocketError(ws: WebSocket) { this.gone.add(ws); await this.dropped(ws); }

  paying(p: Player) { return !!p.pending || (p.payingUntil ?? 0) > nowMs(); }

  async dropped(ws: WebSocket) {
    await this.load();
    const room = this.room; if (!room) return;
    const id = ws.deserializeAttachment()?.id; if (!id) return;
    const p = room.players[id]; if (!p) return;
    // A replaced socket (same player reconnected) must not mark the live one as gone.
    if (this.socketsFor(id).some(s => s !== ws)) return;
    p.connected = false;
    // In the lobby a seat nobody paid for (or any seat of a free room) is given up. A paid one waits for its owner
    // (start() refunds it if they are still away at the flag), and so does one whose owner is paying right now.
    if (room.state === 'lobby' && (!p.paid || room.fee === '0') && !this.paying(p)) await this.remove(p, 'left');
    else if (p.host) this.migrateHost();      // a crashed host must not freeze a cup: whoever is still here runs it
    if (!Object.values(room.players).some(x => x.connected)) room.emptySince = nowMs();
    if ((room.state === 'racing' || room.state === 'countdown') && this.raceDone()) await this.finishRace();
    if (room.state === 'loading' && this.loadWaitsFor().length && this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
    await this.save();
    this.broadcastRoom();
    await this.schedule();
  }

  socketsFor(id: string): WebSocket[] {
    return this.ctx.getWebSockets().filter(s => { try { return s.deserializeAttachment()?.id === id; } catch { return false; } });
  }

  send(ws: WebSocket, obj: unknown) { try { ws.send(JSON.stringify(obj)); } catch { /* closing */ } }

  /** To every socket that has said hello: one that has not is nobody in this room yet. */
  broadcast(obj: unknown, except?: WebSocket) {
    const text = JSON.stringify(obj);
    for (const s of this.ctx.getWebSockets()) if (s !== except) { try { if (s.deserializeAttachment()?.id) s.send(text); } catch { /* closing */ } }
  }

  broadcastRoom() { this.broadcast({ t: 'room', room: this.snapshot() }); void this.mirrorLobby(); }

  /** The browse list reads D1, not the object: keep the row's state, head count and host name current. */
  async mirrorLobby() {
    const room = this.room; if (!room) return;
    const here = Object.values(room.players).filter(p => p.connected).length;
    const host = room.players[room.hostId];
    const state = room.state === 'lobby' ? 'lobby' : room.state === 'cancelled' ? 'cancelled' : room.state === 'finished' ? 'finished' : 'racing';
    try {
      await this.env.DB.prepare('UPDATE rooms SET state = ?2, players = ?3, host_name = ?4, updated_at = ?5 WHERE code = ?1')
        .bind(room.code, state, here, host?.name ?? null, Math.floor(nowMs() / 1000)).run();
    } catch (e: any) { console.warn('mirror', e?.message ?? e); }
  }

  // ---------- lobby ----------
  async hello(ws: WebSocket, m: any) {
    if (this.greeting.has(ws)) return;
    this.greeting.add(ws);
    try { await this.greet(ws, m); } finally { this.greeting.delete(ws); }
  }

  /** A hello that cannot be seated. A socket with no seat is closed: the phone stops reconnecting to be refused again. */
  refuse(ws: WebSocket, msg: string) {
    this.send(ws, { t: 'error', msg });
    if (!ws.deserializeAttachment()?.id) { try { ws.close(1008, 'refused'); } catch { /* gone */ } }
  }

  async greet(ws: WebSocket, m: any) {
    const room = this.room!;
    const wallet = m.session ? await readSession(this.env, 'Bearer ' + m.session).catch(() => null) : null;
    const device = String(m.device ?? '').slice(0, 64);
    if (!wallet && !device) return this.refuse(ws, 'Could not identify this phone');
    if (!wallet && room.fee !== '0') return this.refuse(ws, 'Sign in with your wallet to join a paid room');
    const geo: string = ws.deserializeAttachment()?.geo ?? '';
    const id = wallet ?? await guestId(device);
    const away = () => this.gone.has(ws) || ws.readyState !== WebSocket.OPEN;   // the socket closed during an await
    // One player per socket: a second hello under another name would leave the first seated with nobody behind it.
    const prev = ws.deserializeAttachment()?.id;
    if (prev && prev !== id) { this.send(ws, { t: 'error', msg: 'This connection already joined as someone else' }); return; }
    // Only a new seat is checked against the region: a seated paid racer whose reconnect comes from an address placed
    // elsewhere (a Wi-Fi to cellular handover) is let back in to the race it paid for.
    const closed = () => room.kicked?.includes(id) ? 'The host removed you from this room'
      : room.state !== 'lobby' || this.starting ? 'This race has already started'
      : Object.keys(room.players).length >= MAX_PLAYERS ? 'Room is full'
      : room.fee !== '0' && geoBlocked(this.env, geo) ? 'Paid races are not available in your region' : null;
    let p = room.players[id];
    if (!p) {
      const no = closed(); if (no) return this.refuse(ws, no);
      const fresh: Player = {
        id, wallet, device, name: await this.displayName(wallet, device),
        character: 0, kart: 'oodi', joinedAt: nowMs(), host: false, connected: true, ready: false,
        paid: room.fee === '0', loaded: false, racing: false, dnf: false, points: 0, totalMs: 0, wins: 0,
      };
      if (away()) return;                                     // gone during that await: no seat for nobody
      p = room.players[id];                                   // the same player may have joined on another socket meanwhile
      if (!p) {
        const again = closed(); if (again) return this.refuse(ws, again);   // ...and the room may have moved on meanwhile
        p = room.players[id] = fresh;
      }
    }
    if (away()) return;
    if (wallet && !(room.members ?? []).includes(wallet)) room.members = [...(room.members ?? []), wallet].slice(-64);
    for (const other of this.socketsFor(id)) if (other !== ws) { try { other.close(1000, 'replaced'); } catch { /* gone */ } }
    ws.serializeAttachment({ id, geo, at: nowMs() });
    p.connected = true;
    room.emptySince = undefined;
    // The host is whoever set the room up, whenever they are here; otherwise the one holding it, or if they are gone,
    // this player. (A reconnect by the organiser takes it back; a stranger only gets it from an empty chair.)
    const holder = room.players[room.hostId];
    if (id === (room.organiser ?? room.hostId) || !holder || !holder.connected || !holder.host) this.makeHost(p);
    const c = Number(m.character); if (Number.isInteger(c) && c >= 0 && c < 32) p.character = c;
    if (typeof m.kart === 'string') p.kart = await this.kartFor(wallet, m.kart);
    this.send(ws, { t: 'welcome', id, serverNow: nowMs(), room: this.snapshot() });
    await this.save();
    this.broadcastRoom();
    await this.schedule();
  }

  makeHost(p: Player) {
    const room = this.room!;
    for (const x of Object.values(room.players)) x.host = false;
    p.host = true; room.hostId = p.id;
  }

  /** The kart everyone else sees: a free one, or a shop kart this wallet owns. Anything else races the default. */
  async kartFor(wallet: string | null, kart: string): Promise<string> {
    if (FREE_KARTS.has(kart)) return kart;
    const sku = 'kart.' + kart;
    if (!wallet || !SHOP_KARTS.has(sku)) return 'oodi';
    const own = await this.env.DB.prepare('SELECT 1 FROM unlocks WHERE wallet = ?1 AND sku = ?2').bind(wallet, sku).first().catch(() => null);
    return own ? kart : 'oodi';
  }

  /** Shown as rich text on every phone: letters and digits only from a device id, no markup from a stored name. */
  async displayName(wallet: string | null, device: string): Promise<string> {
    if (!wallet) return ('Guest ' + device.replace(/[^A-Za-z0-9]/g, '').slice(0, 4).toUpperCase()).trim();
    const row = await this.env.DB.prepare('SELECT skr_name FROM players WHERE wallet = ?1').bind(wallet).first<any>().catch(() => null);
    const name = typeof row?.skr_name === 'string' ? row.skr_name.replace(/[<>]/g, '').slice(0, 32) : '';
    return name || short(wallet);
  }

  // ---------- entry payments ----------
  entryMemo(round: number) { return `kc:entry:${this.room!.code}:${round}`; }

  /** An entry payment, by socket or by HTTP. It is never dropped: it buys the seat it was made for, or it becomes an
   *  orphan the alarm verifies and refunds. 'busy': too many orphans already (the phone keeps it and tries later). */
  async intake(wallet: string, sig: string, round: number, ws?: WebSocket): Promise<'checking' | 'refunding' | 'known' | 'refused' | 'busy'> {
    const room = this.room!;
    const say = (msg: string) => { if (ws) this.send(ws, { t: 'error', msg }); };
    const sigOk = isSignature(sig) || (this.env.MOCK_CHAIN === '1' && sig.startsWith('mock') && sig.length <= 128);
    if (!wallet || room.fee === '0') return 'refused';
    if (!sigOk) { say('Entry payment rejected: not a transaction signature'); return 'refused'; }
    if (!Number.isInteger(round) || round < 1 || round > room.round) round = room.round;
    // Already known from this wallet: nothing new to do. Refused only by a VERIFIED holder (claimed here, or a paid
    // seat's): another wallet merely claiming the same signature (someone who saw it on-chain first) must not stop
    // its payer; verification decides whose it is.
    const mine = Object.values(room.players).some(x => x.wallet === wallet && (x.paidSig === sig || x.pending?.sig === sig))
      || (room.orphans ?? []).some(o => o.sig === sig && o.wallet === wallet);
    if (mine) return 'known';
    const taken = (room.entrySigs ?? []).includes(sig) || Object.values(room.players).some(x => x.paidSig === sig);
    if (taken) { say('Entry payment rejected: that payment was already used'); return 'refused'; }
    // Only a wallet that has sat in this room can leave a payment with it: every real case (paid, then kicked, dropped,
    // started without, rematched) has, and strangers cannot fill the room's list or its RPC budget.
    if (!(room.members ?? []).includes(wallet)) return 'refused';
    room.watch = room.watch?.filter(w => !(w.wallet === wallet && w.round === round));
    const entry: Entry = { wallet, sig, round, since: nowMs() };
    const p = Object.values(room.players).find(x => x.wallet === wallet);
    if (p && room.state === 'lobby' && round === room.round && !p.paid && !p.pending && (room.refunds?.[wallet] ?? 0) < MAX_REFUNDS
      && (p.payingUntil || this.takeHold(wallet))) {           // (the 'paying' that preceded it already took this hold)
      p.pending = entry; p.payingUntil = undefined;
      await this.checkEntry(p);
      return 'checking';
    }
    // It cannot buy a seat: none to buy (gone, kicked, the room has moved on), paid already, one being checked, or the
    // refund limit reached. It is verified all the same and, if it landed, refunded.
    const orphans = room.orphans ?? [];
    if (orphans.length >= MAX_ORPHANS || orphans.filter(o => o.wallet === wallet).length >= MAX_ORPHANS_PER_WALLET) return 'busy';
    room.orphans = [...orphans, entry];
    if (p) say(p.paid ? 'You had already paid: that payment will be refunded' : 'That payment cannot buy a seat now: it will be refunded');
    await this.checkOrphan(entry);                     // at once; the alarm asks again if the chain has not shown it yet
    return 'refunding';
  }

  /** The phone may have paid but never got the signature back (killed while the wallet app was open), or said it was
   *  paying and then nothing came: look for the transfer on-chain by its memo and take in whatever is found. */
  async scan(wallet: string, round: number): Promise<'checking' | 'refunding' | 'known' | 'refused' | 'busy'> {
    const room = this.room!;
    if (!wallet || room.fee === '0' || !(room.members ?? []).includes(wallet)) return 'refused';
    if (!Number.isInteger(round) || round < 1 || round > room.round) round = room.round;
    let out: 'checking' | 'refunding' | 'known' | 'refused' | 'busy' = 'known';
    for (const sig of await findEntrySignatures(this.env, wallet, this.entryMemo(round))) out = await this.intake(wallet, sig, round);
    return out;
  }

  /** verifyPayment, once at a time per signature. */
  async verifyEntry(e: Entry): Promise<EntryCheck | null> {
    const room = this.room!;
    if (this.verifying.has(e.sig)) return null;
    this.verifying.add(e.sig);
    try { return await verifyPayment(this.env, e.sig, e.wallet, this.entryMemo(e.round), BigInt(room.fee), room.currency, 'entry'); }
    catch (err: any) { return { ok: false, pending: true, rpcError: true, why: String(err?.message ?? err) }; }
    finally { this.verifying.delete(e.sig); }
  }

  /** Ask again later? While the chain may still show it (not seen yet, for ENTRY_TIMEOUT_MS), and for as long as the RPC
   *  is failing (up to a day, backing off). Not once the chain has shown a transaction that does not pay the entry. */
  keepChecking(e: Entry, r: EntryCheck): boolean {
    if (r.ok) return false;
    const t = nowMs();
    e.tries = (e.tries ?? 0) + 1;
    if (r.rpcError) {
      console.warn('entry check: rpc', this.room?.code, e.sig.slice(0, 12), r.why);
      e.nullSince = undefined;                           // only consecutive 'not seen' answers count toward giving up
      e.nextTry = t + Math.min(10 * 60_000, ENTRY_RETRY_MS * 2 ** Math.min(e.tries, 10));
      return t - e.since < ENTRY_GIVE_UP_MS;
    }
    if (!r.pending) return false;
    e.nullSince ??= t;
    e.nextTry = t + Math.min(30_000, ENTRY_RETRY_MS * 2 ** Math.min(e.tries - 1, 3));   // 4, 8, 16, 30 s
    return t - e.nullSince < ENTRY_TIMEOUT_MS;
  }

  /** A verified payment's seat, once: room_entries.signature is UNIQUE and never deleted, so a payment that ever bought
   *  a seat anywhere cannot buy another, even if tx_log lost its row. The room's own list answers before D1 is asked. */
  async claimEntry(e: Entry): Promise<boolean> {
    const room = this.room!;
    if ((room.entrySigs ?? []).includes(e.sig)) return false;
    // D1 and the room's own storage are two writes. The entry is marked (and saved) before the insert, so if the room
    // restarts between them, the retry of THIS entry finds its own row and carries on; a fresh delivery of the same
    // signature has no mark and is refused.
    const retry = e.claiming === true;
    e.claiming = true;
    await this.save();
    const row = await this.env.DB.prepare('INSERT OR IGNORE INTO room_entries (code, round, wallet, signature, amount, currency, created_at) VALUES (?1,?2,?3,?4,?5,?6,?7)')
      .bind(room.code, e.round, e.wallet, e.sig, room.fee, room.currency, Math.floor(nowMs() / 1000)).run();
    if (!row.meta.changes) {
      const had = retry ? await this.env.DB.prepare('SELECT code, round, wallet FROM room_entries WHERE signature = ?1').bind(e.sig).first<any>() : null;
      if (!had || had.code !== room.code || had.round !== e.round || had.wallet !== e.wallet) return false;
    }
    if ((room.entrySigs ?? []).includes(e.sig)) return false;
    room.entrySigs = [...(room.entrySigs ?? []), e.sig];
    return true;
  }

  async checkEntry(p: Player) {
    const room = this.room!, e = p.pending;
    if (!e || (e.nextTry && e.nextTry > nowMs())) return;
    const r = await this.verifyEntry(e);
    if (!r) return;                                      // being checked already
    if (r.ok) {
      // The entry stays where it is until its claim has resolved: a D1 error here leaves it for the next alarm.
      let claimed: boolean;
      try { claimed = await this.claimEntry(e); }
      catch (err: any) { console.error('entry claim', room.code, err?.message ?? err); e.nextTry = nowMs() + ENTRY_RETRY_MS; return; }
      if (p.pending === e) p.pending = undefined;
      room.orphans = room.orphans?.filter(o => o !== e);
      if (!claimed) {
        for (const s of this.socketsFor(p.id)) this.send(s, { t: 'error', msg: 'Entry payment rejected: that payment was already used' });
        return;
      }
      // Paid already (a second payment verified meanwhile), gone (left, kicked or dropped while the chain was asked),
      // or the room has moved on: a payment that bought no seat goes back.
      if (p.paid || room.players[p.id] !== p || room.state !== 'lobby') { await this.refundTo(e.wallet, e.round); return; }
      p.paid = true; p.paidSig = e.sig; p.payingUntil = undefined;
      return;
    }
    if (this.keepChecking(e, r)) return;                 // alarm asks again
    if (p.pending === e) p.pending = undefined;
    for (const s of this.socketsFor(p.id)) this.send(s, { t: 'error', msg: r.pending ? 'Payment not seen on-chain. Try again.' : 'Entry payment rejected: ' + r.why });
  }

  async checkOrphan(o: Entry) {
    const room = this.room!;
    if (o.nextTry && o.nextTry > nowMs()) return;
    const r = await this.verifyEntry(o);
    if (!r) return;
    const drop = () => { room.orphans = room.orphans?.filter(x => x !== o); };   // this entry, not another wallet's with the same signature
    if (r.ok) {
      try { if (await this.claimEntry(o)) await this.refundTo(o.wallet, o.round); }
      catch (err: any) { console.error('orphan claim', room.code, err?.message ?? err); o.nextTry = nowMs() + ENTRY_RETRY_MS; return; }
      drop();
      return;
    }
    if (!this.keepChecking(o, r)) { drop(); if (r.rpcError) console.error('entry given up after a day of RPC errors', room.code, o.wallet, o.sig); }
  }

  orphan(p: Player) {
    const room = this.room!;
    const e = p.pending;
    if (e && !(room.orphans ?? []).some(o => o.sig === e.sig && o.wallet === e.wallet)) room.orphans = [...(room.orphans ?? []), e];
    p.pending = undefined;
  }

  // ---------- starting ----------
  async start(ws: WebSocket, p: Player): Promise<boolean> {
    const room = this.room!;
    if (!p.host || room.state !== 'lobby') return false;
    const here = Object.values(room.players).filter(x => x.connected);
    if (here.length < 2) { this.send(ws, { t: 'error', msg: 'You need at least 2 racers' }); return false; }
    if (room.fee !== '0') {
      if (this.env.PAID_ROOMS !== '1') { this.send(ws, { t: 'error', msg: 'Paid rooms are switched off right now' }); return false; }
      const waiting = Object.values(room.players).filter(x => this.paying(x) || (x.connected && !x.paid));
      if (waiting.length) { this.send(ws, { t: 'error', msg: `Waiting for ${waiting.map(x => x.name).join(', ')} to pay in` }); return false; }
      if (here.length < MIN_PAID_PLAYERS) { this.send(ws, { t: 'error', msg: `A paid race needs ${MIN_PAID_PLAYERS} racers` }); return false; }
    }
    // From here the lobby is closed: nobody new may join while the awaits below run.
    this.starting = true;
    try {
      // Anyone away when the flag drops is out of this round: a paid seat they hold is refunded.
      for (const x of Object.values(room.players)) if (!x.connected) await this.remove(x, 'away');
      for (const x of Object.values(room.players)) { x.points = 0; x.totalMs = 0; x.wins = 0; x.place = undefined; x.left = false; }
      room.standings = undefined; room.lastResults = undefined;
      await this.env.DB.prepare("UPDATE rooms SET state = 'racing' WHERE code = ?1").bind(room.code).run();
      await this.beginLoading(0);
    } finally { this.starting = false; }
    return true;
  }

  async beginLoading(raceIndex: number) {
    const room = this.room!;
    room.raceIndex = raceIndex;
    room.state = 'loading';
    room.startAt = undefined; room.finishDeadline = undefined;
    room.loadDeadline = nowMs() + LOAD_TIMEOUT_MS;
    room.nextAt = undefined;
    for (const x of Object.values(room.players)) { x.loaded = false; x.racing = false; x.finishMs = undefined; x.dnf = false; x.ready = false; x.ref = undefined; x.flagged = undefined; x.flagAt = undefined; x.finLag = undefined; }
    this.broadcast({ t: 'load', track: room.tracks[raceIndex], race: raceIndex + 1, races: room.tracks.length });
  }

  /** Who the track load waits for: everyone here, and in a paid room every paid seat whose phone may be reconnecting. */
  loadWaitsFor(): Player[] {
    const room = this.room!;
    return Object.values(room.players).filter(x => !x.left && (x.connected || (room.fee !== '0' && x.paid)));
  }

  async loaded(p: Player) {
    const room = this.room!;
    if (room.state !== 'loading') return;
    p.loaded = true;
    if (this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
  }

  async beginCountdown() {
    const room = this.room!;
    const paying = room.fee !== '0';
    if (paying && room.raceIndex === 0) {
      // Race one of a paid room: a paid seat whose phone never loaded the track has not raced. Its entry goes back and
      // it is out of the cup (it would otherwise race later rounds for points it paid nothing to be in).
      for (const x of Object.values(room.players)) {
        if (x.loaded && !x.left && x.paid) continue;
        this.orphan(x);
        await this.refund(x);
        delete room.players[x.id];
        for (const s of this.socketsFor(x.id)) { this.send(s, { t: 'closed', reason: 'You were not on the grid in time: your entry is refunded' }); try { s.close(1000, 'missed the start'); } catch { /* gone */ } }
      }
      if (!room.players[room.hostId]?.host) this.migrateHost();
      if (Object.values(room.players).filter(x => x.paid).length < MIN_PAID_PLAYERS) {
        // Not enough of a field for a paid race: back to the lobby, the seats still here and their entries kept.
        room.state = 'lobby'; room.loadDeadline = undefined; room.createdAt = nowMs();
        for (const x of Object.values(room.players)) x.loaded = false;
        this.broadcast({ t: 'error', msg: 'Not enough racers made it to the grid: back to the lobby' });
        await this.env.DB.prepare("UPDATE rooms SET state = 'lobby' WHERE code = ?1").bind(room.code).run();
        return;
      }
    }
    room.state = 'countdown';
    room.loadDeadline = undefined;
    room.startAt = nowMs() + COUNTDOWN_LEAD_MS;
    room.finishDeadline = room.startAt + RACE_CAP_MS;
    for (const x of Object.values(room.players)) {
      // A loaded phone races, connected or not this instant (a blip on the grid is a reconnect, not a withdrawal);
      // in a paid room only a paid seat does.
      x.racing = x.loaded && !x.left && (!paying || x.paid);
      x.ref = x.racing ? newRef() : undefined; x.flagged = undefined; x.flagAt = undefined; x.finLag = undefined;
      // Each race is judged on its own round trips: the network in race one says nothing about race two's.
      x.lags = undefined; x.spOut = x.rttMin = x.lastSp = undefined;
    }
    this.broadcast({ t: 'countdown', startAt: room.startAt, serverNow: nowMs() });
  }

  // ---------- race ----------
  async relay(ws: WebSocket, p: Player, m: any) {
    const room = this.room!;
    if (room.state !== 'countdown' && room.state !== 'racing') return;
    if (room.state === 'countdown' && nowMs() >= (room.startAt ?? 0)) room.state = 'racing';   // no save needed; the deadline is what matters
    // Only a kart the referee is following is shown to anyone: not a spectator's, not a DNF's, not a caught cheat's
    // (their karts collide).
    if (!p.racing || !p.ref || p.dnf || p.flagged) return;
    const geo = geoOf(room.tracks[room.raceIndex]);
    const tm = Number(m.tm), pos = nums(m.p, 3);
    const t = nowMs(), elapsed = (t - (room.startAt ?? t)) / 1000;
    if (!geo || !pos || !Number.isFinite(Number(m.p?.[0])) || !Number.isFinite(Number(m.p?.[2])) || !Number.isFinite(tm)) return;
    const live = p.finishMs === undefined && !finished(p.ref, geo);   // after the crossing nothing it does changes the result
    // The referee follows the kart from its own positions: gates in order, speed, the circuit. A state it cannot
    // believe (a teleport, a clock running ahead of the room's) is not shown to anyone else either.
    if (tm > 0 && tm > elapsed + CLOCK_AHEAD_S) { if (live) this.flag(p, `race clock ${(tm - elapsed).toFixed(2)} s ahead of the room`); return; }
    if (p.ref.last && tm - p.ref.last[0] < MIN_SAMPLE_S) return;     // a repeat, out of order, or faster than a phone sends
    if (tm > 0) this.noteLag(p, t, elapsed - tm);
    if (!p.lastSp || t - p.lastSp >= SP_EVERY_MS) { p.lastSp = t; p.spOut = [...(p.spOut ?? []), t].slice(-4); this.send(ws, { t: 'sp', s: t }); }
    const n = p.ref.n, was = finished(p.ref, geo);
    const why = push(p.ref, geo, { t: tm, x: pos[0], y: pos[1], z: pos[2] });
    if (why) { if (live) this.flag(p, why); if (why.startsWith('moved')) return; }
    // The clock is judged at the finishing crossing, over the states up to it: nothing sent after the line can hide
    // a late clock, and a racer who goes silent at the line is judged the same.
    if (!was && finished(p.ref, geo)) p.finLag = this.clockLag(p) ?? '';
    if (p.ref.n !== n) void this.save();                   // a gate: worth keeping if the object is evicted
    // Every state is refereed, however late it arrives (a stall or a reconnect across the line: the crossing is in
    // them); only the relay drops stale ones, and is capped at 25 Hz per racer, carrying the fields a phone draws from.
    if (tm < elapsed - CLOCK_BEHIND_S) return;
    if (p.lastState && t - p.lastState < 40) return;
    p.lastState = t;
    this.broadcast({ t: 's', id: p.id, p: pos, r: nums(m.r, 4), v: nums(m.v, 3), st: num(m.st), lap: Math.trunc(num(m.lap)), cp: Math.trunc(num(m.cp)), sc: num(m.sc), tm }, ws);
  }

  noteLag(p: Player, t: number, lag: number) {
    const lags = p.lags ??= [], last = lags[lags.length - 1];
    if (last && t - last[0] < LAG_BUCKET_MS) last[1] = Math.min(last[1], lag);
    else { lags.push([t, lag]); while (lags.length && lags[0][0] < t - LAG_SPAN_MS - LAG_BUCKET_MS) lags.shift(); }
  }

  /** Is this racer's clock later than its round trip explains, over the last LAG_SPAN_MS? */
  clockLag(p: Player): string | null {
    const t = nowMs(), seen = (p.lags ?? []).filter(b => b[0] >= t - LAG_SPAN_MS - LAG_BUCKET_MS).map(b => b[1]);
    if (!seen.length) return 'no race clock seen after GO';
    const lag = Math.min(...seen), bound = Math.min(p.rttMin ?? 0, RTT_CAP_MS) / 2000 + CLOCK_LAG_SLACK_S;
    return lag > bound ? `race clock ${lag.toFixed(2)} s behind the room (round trip ${p.rttMin ?? '?'} ms)` : null;
  }

  flag(p: Player, why: string) {
    if (p.flagged) return;
    p.flagged = why; p.flagAt = [p.ref?.n ?? 0, p.ref?.A ?? -1e9];
    console.warn('referee', this.room?.code, p.id, why);
  }

  /** The referee's verdict on a racer's drive: null if it followed a clean drive over the line with a clock in bounds. */
  verdict(p: Player): string | null {
    const geo = geoOf(this.room!.tracks[this.room!.raceIndex]);
    if (!geo) return 'unknown track';
    if (p.flagged) return p.flagged;
    if (!p.ref) return 'no drive seen';
    if (p.ref.bad) return p.ref.bad;
    if (finishT(p.ref, geo) === null) return `crossed ${p.ref.n} of ${crossingsToFinish(geo)} gates`;
    return p.finLag === undefined ? 'no drive seen' : p.finLag || null;
  }

  /** The phone says it finished. The room times it itself: the referee's own crossing, on the phone's race clock as
   *  judged at that crossing. The time and laps a phone claims are not used (a claim within any tolerance was a free
   *  fraction of a second); a fin the referee cannot back up is a DNF. */
  async finish(ws: WebSocket, p: Player): Promise<boolean> {
    const room = this.room!;
    if ((room.state !== 'racing' && room.state !== 'countdown') || !p.racing || p.finishMs !== undefined || p.dnf) return false;
    const why = this.verdict(p);
    if (why) { this.send(ws, { t: 'error', msg: 'Finish rejected' }); p.dnf = true; this.flag(p, why); console.warn('finish rejected', room.code, p.id, why); }
    else {
      p.finishMs = Math.round(finishT(p.ref!, geoOf(room.tracks[room.raceIndex])!)! * 1000);
      // The first finish that counts starts everyone else's last minute. A rejected one does not: anyone can send one.
      if (room.finishDeadline && room.finishDeadline > nowMs() + FINISH_GRACE_MS) room.finishDeadline = nowMs() + FINISH_GRACE_MS;
    }
    this.broadcast({ t: 'finished', id: p.id, ms: p.finishMs ?? null, dnf: p.dnf });
    if (this.raceDone()) await this.finishRace();
    return true;
  }

  /** Every racer has a result: a finish, a DNF, or (its socket gone) a drive the referee followed over the line,
   *  which finishRace() takes as its finish. A racer still out on the circuit keeps the race open. */
  raceDone(): boolean {
    const room = this.room!, geo = geoOf(room.tracks[room.raceIndex]);
    return Object.values(room.players).filter(x => x.racing).every(x => x.finishMs !== undefined || x.dnf
      || (!x.connected && !!geo && !!x.ref && finishT(x.ref, geo) !== null));
  }

  async finishRace() {
    const room = this.room!;
    const geo = geoOf(room.tracks[room.raceIndex]);
    const racers = Object.values(room.players).filter(x => x.racing);
    for (const x of racers) {
      if (x.finishMs !== undefined) continue;
      // No fin arrived (the socket dropped at the line, the phone quit to the menu), but the referee followed the
      // drive over the line and believed all of it: that drive is the finish.
      if (geo && !this.verdict(x)) { x.finishMs = Math.round(finishT(x.ref!, geo)! * 1000); x.dnf = false; }
      else x.dnf = true;
    }
    // DNFs by how far the referee followed them (gates, then distance round the lap), never by a number a phone sent.
    const far = (x: Player) => x.flagAt ?? [x.ref?.n ?? 0, x.ref?.A ?? -1e9];   // a flagged racer is ranked where it was caught
    racers.sort((a, b) => {
      if (a.dnf !== b.dnf) return a.dnf ? 1 : -1;
      if (!a.dnf) return (a.finishMs ?? 0) - (b.finishMs ?? 0);
      const [an, aa] = far(a), [bn, ba] = far(b);
      return bn - an || ba - aa;
    });
    // Nobody finished and nobody even crossed the start line (every phone dropped on the grid): not a race, no points.
    const contest = racers.some(x => !x.dnf || (x.ref?.n ?? 0) > 0);
    const order: Order[] = racers.map((x, i) => {
      const points = contest ? POINTS[i] ?? 0 : 0;   // a DNF still scores its place: a dropped call should not zero a cup
      x.place = i + 1; x.points += points; if (i === 0 && !x.dnf) x.wins++;
      x.totalMs += x.dnf ? RACE_CAP_MS : (x.finishMs ?? 0);
      return { id: x.id, name: x.name, ms: x.dnf ? null : (x.finishMs ?? null), place: i + 1, points, dnf: x.dnf };
    });
    // The last race ends the cup. (Everyone gone between races is the results alarm's call: abandonCup.)
    const final = room.raceIndex >= room.tracks.length - 1;
    room.standings = Object.values(room.players).filter(x => x.racing || x.points > 0)
      .sort((a, b) => b.points - a.points || b.wins - a.wins || a.totalMs - b.totalMs)
      .map((x, i) => ({ id: x.id, name: x.name, points: x.points, wins: x.wins, totalMs: x.totalMs, place: i + 1 }));
    room.lastResults = { race: room.raceIndex + 1, races: room.tracks.length, track: room.tracks[room.raceIndex], final, order };
    room.finishDeadline = undefined; room.startAt = undefined;
    room.state = final ? 'finished' : 'results';
    room.nextAt = final ? undefined : nowMs() + RESULTS_MS;
    if (final) await this.settleFinal();
    this.broadcast({ t: 'results', results: room.lastResults, standings: room.standings, payouts: this.publicPayouts(), pool: this.pool().toString(), cut: this.cut().toString(), room: this.snapshot() });
    await this.save();
    await this.schedule();
  }

  /** Every entry paid into this round leaves it as a prize, the house's cut, the organiser's share, or a refund. All
   *  the rows are made before anything is awaited, so nothing arriving meanwhile (a rematch, a leave) can skip one. */
  async settleFinal() {
    const room = this.room!;
    const now = Math.floor(nowMs() / 1000);
    const paying = room.fee !== '0';
    const rows: Payout[] = [];
    const inCup = new Set((room.standings ?? []).map(s => s.id));
    // A paid seat that never raced (it left or dropped before its first race) was never in the contest: refunded.
    if (paying) for (const x of Object.values(room.players)) if (x.paid && x.wallet && !inCup.has(x.id)) { const r = this.refundRow(x); if (r) rows.push(r); }
    let paid = (room.standings ?? []).map(s => room.players[s.id]).filter(x => x && x.paid && x.wallet && paying);
    // Fewer than two paid racers, or a cup in which nobody scored: there was no contest, so everyone's entry goes back.
    if (paid.length < MIN_PAID_PLAYERS || !(room.standings ?? []).some(s => s.points > 0)) {
      for (const x of paid) { const r = this.refundRow(x); if (r) rows.push(r); }
      paid = [];
    }
    const pool = paying ? BigInt(room.fee) * BigInt(paid.length) : 0n;
    const { cut, hostReward, prizes } = splitPool(pool, paid.length, room.cutBps);
    const payRow = (wallet: string, kind: Payout['kind'], amount: bigint, place?: number): Payout => {
      const id = crypto.randomUUID();
      return { id, wallet, kind, place, amount: amount.toString(), currency: room.currency, status: 'pending', attempts: 0, round: room.round, batch: id, created: nowMs() };
    };
    const prizeRows = prizes.map((amt, i) => payRow(paid[i].wallet!, 'prize', amt, i + 1));
    // The organiser's share goes to whoever set the room up, if they raced in it; else to the racer holding the room at
    // the end (the organiser left); else the house keeps it. Every row is its own transaction, so a share too small for
    // an emptied SOL account (below the rent floor) fails alone, without holding up a winner's prize.
    const org = room.players[room.organiser ?? room.hostId];
    const host = org && paid.includes(org) ? org : paid.find(x => x.id === room.hostId);
    const hostRow = hostReward > 0n && host?.wallet ? payRow(host.wallet, 'host', hostReward) : null;
    const kept = hostRow ? cut : cut + hostReward;
    room.payouts.push(...prizeRows, ...(hostRow ? [hostRow] : []));
    rows.push(...prizeRows, ...(hostRow ? [hostRow] : []));
    await this.env.DB.batch([
      this.env.DB.prepare("UPDATE rooms SET state = 'finished', pool = ?2, cut = ?3, finished_at = ?4 WHERE code = ?1")
        .bind(room.code, pool.toString(), kept.toString(), now),
      ...this.payoutInserts(rows),
    ]);
  }

  /** A cup nobody can carry on (everyone left, nobody loaded the next track): the races already run decide it and it
   *  pays out on those standings, the same as a finished cup. Before any race has run, everyone is refunded. */
  async abandonCup(reason: string) {
    const room = this.room!;
    if (!room.standings?.length) { await this.cancel(reason); return; }
    room.state = 'finished'; room.nextAt = undefined; room.loadDeadline = undefined;
    if (room.lastResults) room.lastResults.final = true;
    await this.settleFinal();
    this.broadcast({ t: 'results', results: room.lastResults, standings: room.standings, payouts: this.publicPayouts(), pool: this.pool().toString(), cut: this.cut().toString(), room: this.snapshot() });
  }

  // ---------- leaving, refunds, reset ----------
  async remove(p: Player, reason: string) {
    const room = this.room!;
    this.orphan(p);                          // a payment still being checked goes on being checked, and is refunded
    if (room.state === 'lobby' || room.state === 'finished' || room.state === 'cancelled') {
      if (p.paid && p.wallet && room.fee !== '0' && room.state === 'lobby') await this.refund(p);
      delete room.players[p.id];
    } else {
      p.connected = false; p.left = true; p.dnf = p.finishMs === undefined;   // mid-race: they are still in the standings, as a DNF
    }
    for (const s of this.socketsFor(p.id)) { this.send(s, { t: 'closed', reason }); try { s.close(1000, reason); } catch { /* gone */ } }
    if (p.host) this.migrateHost();
    if ((room.state === 'racing' || room.state === 'countdown') && this.raceDone()) await this.finishRace();
    if (room.state === 'loading' && this.loadWaitsFor().length && this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
  }

  migrateHost() {
    const room = this.room!;
    const next = Object.values(room.players).filter(x => x.connected && this.socketsFor(x.id).length).sort((a, b) => a.joinedAt - b.joinedAt)[0];
    for (const x of Object.values(room.players)) x.host = false;
    if (next) { next.host = true; room.hostId = next.id; }
  }

  /** A refund row for a paid seat, made at once (no await), and the seat unpaid. */
  refundRow(p: Player): Payout | null {
    if (!p.paid || !p.paidSig || !p.wallet) return null;   // (a payment that bought a seat can never buy another: intake refuses it)
    p.paid = false; p.paidSig = undefined;
    return this.refundRowTo(p.wallet, this.room!.round);
  }

  refundRowTo(wallet: string, round: number): Payout {
    const room = this.room!;
    const id = crypto.randomUUID();
    const row: Payout = { id, wallet, kind: 'refund', amount: room.fee, currency: room.currency, status: 'pending', attempts: 0, round, batch: id, created: nowMs() };
    room.payouts.push(row);
    if (round === room.round) room.refunds = { ...(room.refunds ?? {}), [wallet]: (room.refunds?.[wallet] ?? 0) + 1 };
    return row;
  }

  async refund(p: Player) { const r = this.refundRow(p); if (r) await this.env.DB.batch(this.payoutInserts([r])); }
  async refundTo(wallet: string, round: number) { await this.env.DB.batch(this.payoutInserts([this.refundRowTo(wallet, round)])); }

  payoutInserts(rows: Payout[]) {
    const room = this.room!, now = Math.floor(nowMs() / 1000);
    return rows.map(r => this.env.DB.prepare('INSERT INTO payouts (id, code, round, wallet, kind, place, amount, currency, status, attempts, created_at, updated_at) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,0,?10,?10)')
      .bind(r.id, room.code, r.round, r.wallet, r.kind, r.place ?? null, r.amount, r.currency, r.status, now));
  }

  async cancel(reason: string) {
    const room = this.room!;
    const rows = Object.values(room.players).map(p => room.fee !== '0' ? this.refundRow(p) : null).filter((r): r is Payout => !!r);
    room.state = 'cancelled';
    await this.env.DB.batch([this.env.DB.prepare("UPDATE rooms SET state = 'cancelled' WHERE code = ?1").bind(room.code), ...this.payoutInserts(rows)]);
    this.broadcast({ t: 'closed', reason, payouts: this.publicPayouts() });
  }

  /** Same code, same people, new round: the group stays together for a rematch. */
  reset() {
    const room = this.room!;
    room.round += 1; room.raceIndex = 0; room.state = 'lobby';
    room.startAt = room.loadDeadline = room.finishDeadline = room.nextAt = undefined;
    room.standings = undefined; room.lastResults = undefined; room.createdAt = nowMs();
    // A new round's entries carry the new round in their memo, so the old signatures cannot buy a seat in it anyway;
    // paid-out rows are history (D1 keeps them). What is still owed stays until it is paid, and a payment still being
    // checked is checked on and refunded.
    room.entrySigs = undefined; room.kicked = undefined; room.refunds = undefined; room.holds = undefined;
    room.payouts = room.payouts.filter(x => x.status !== 'confirmed');
    for (const p of Object.values(room.players)) {
      this.orphan(p);
      p.paid = room.fee === '0'; p.paidSig = undefined; p.payingUntil = undefined; p.ready = false; p.left = false;
      p.loaded = false; p.racing = false; p.finishMs = undefined; p.dnf = false; p.place = undefined; p.points = 0; p.totalMs = 0;
      if (!p.connected) delete room.players[p.id];
    }
    void this.env.DB.prepare("UPDATE rooms SET state = 'lobby', round = ?2 WHERE code = ?1").bind(room.code, room.round).run();
  }

  // ---------- money ----------
  pool(): bigint {
    const room = this.room!;
    if (room.fee === '0') return 0n;
    return BigInt(room.fee) * BigInt(Object.values(room.players).filter(p => p.paid && p.wallet).length);
  }
  split(): Split { const room = this.room!; return splitPool(this.pool(), Object.values(room.players).filter(p => p.paid && p.wallet).length, room.cutBps); }
  cut(): bigint { return this.split().cut; }

  /** This round's rows, and any earlier round's not yet paid (a rematch must not hide a prize still owed). */
  publicPayouts() {
    const room = this.room!;
    return room.payouts.filter(p => p.round === room.round || p.status !== 'confirmed').map(p => ({
      id: p.id, wallet: p.wallet, kind: p.kind, place: p.place ?? null, amount: p.amount, currency: p.currency, status: p.status, signature: p.signature ?? null, round: p.round,
    }));
  }

  owes(): boolean {
    const room = this.room!;
    return room.payouts.some(p => p.status === 'pending' || p.status === 'sent') || !!room.orphans?.length || Object.values(room.players).some(p => p.pending);
  }

  /** A send that failed, or a transaction that expired unseen: try again at once a few times, then less and less often. */
  retryLater(rows: Payout[]) {
    for (const r of rows) {
      r.attempts++;
      r.nextTry = r.attempts >= QUICK_SENDS ? nowMs() + Math.min(MAX_BACKOFF_MS, 30_000 * 2 ** (r.attempts - QUICK_SENDS)) : undefined;
    }
  }

  /** Alarm work: send what is pending, confirm what was sent. A transaction is built and signed first and the rows are
   *  SAVED as 'sent' under its signature before it goes out, so a restart or a lost response finds that signature and
   *  waits on it; a new transaction is sent only after the old one's blockhash has expired without it landing. */
  async settle() {
    const room = this.room!;
    const t = nowMs();
    const pending = room.payouts.filter(p => p.status === 'pending');
    const batches = new Map<string, Payout[]>();
    for (const p of pending) batches.set(p.batch, [...(batches.get(p.batch) ?? []), p]);
    for (const [batch, rows] of batches) {
      if (rows[0].nextTry && rows[0].nextTry > t) continue;
      if (t - (rows[0].created ?? t) > PAYOUT_GIVE_UP_MS) {
        for (const r of rows) r.status = 'failed';
        console.error('payout failed for good', room.code, batch, rows.map(r => `${r.kind} ${r.wallet} ${r.amount}`).join(', '));
        await this.mirror(rows).catch(() => {});
        continue;
      }
      const memo = `kc:${rows[0].kind}:${room.code}:${rows[0].round}:${batch.slice(0, 8)}`;
      try {
        // Before paying again, every transaction ever sent for these rows must be known not to have landed.
        const landed = rows[0].sigs?.length ? await landedAny(this.env, rows[0].sigs) : null;
        if (landed) { for (const r of rows) { r.status = 'confirmed'; r.signature = landed; } await this.mirror(rows).catch(() => {}); continue; }
        const built = await buildBatch(this.env, rows, memo);
        for (const r of rows) { r.status = 'sent'; r.signature = built.signature; r.sigs = [...(r.sigs ?? []), built.signature]; r.lastValidBlockHeight = built.lastValidBlockHeight; r.attempts++; r.nextTry = undefined; }
        await this.save();
        try { await sendRaw(this.env, built.raw); } catch (e: any) { console.error('payout send', room.code, batch, e?.message ?? e); }   // it may still land: txStatus decides
        await this.mirror(rows).catch(() => {});
      } catch (e: any) {
        console.error('payout build failed', room.code, batch, e?.message ?? e);
        if (rows[0].status === 'pending') this.retryLater(rows);
      }
    }
    const sent = room.payouts.filter(p => p.status === 'sent' && p.signature);
    const bySig = new Map<string, Payout[]>();
    for (const p of sent) bySig.set(p.signature!, [...(bySig.get(p.signature!) ?? []), p]);
    for (const [sig, rows] of bySig) {
      try {
        const st = await txStatus(this.env, sig, rows[0].lastValidBlockHeight ?? 0);
        if (st === 'confirmed') for (const r of rows) r.status = 'confirmed';
        else if (st === 'expired' || st === 'failed') { for (const r of rows) { r.status = 'pending'; r.signature = undefined; } this.retryLater(rows); }
        if (st !== 'pending') await this.mirror(rows).catch(() => {});
      } catch (e: any) { console.error('payout status failed', sig, e?.message ?? e); }
    }
    if (pending.length || sent.length) this.broadcast({ t: 'payouts', payouts: this.publicPayouts() });
  }

  async mirror(rows: Payout[]) {
    const now = Math.floor(nowMs() / 1000);
    await this.env.DB.batch(rows.map(r => this.env.DB.prepare('UPDATE payouts SET status = ?2, signature = ?3, attempts = ?4, updated_at = ?5 WHERE id = ?1')
      .bind(r.id, r.status, r.signature ?? null, r.attempts, now)));
  }

  // ---------- deadlines ----------
  async alarm() {
    await this.load();
    const room = this.room; if (!room) return;
    const t = nowMs();
    // Each step on its own: one failing (a D1 or RPC blip) must not stop the others, and the room always saves and
    // books its next alarm (a throwing alarm is retried only a few times, and money is waiting on it).
    const step = async (name: string, f: () => Promise<unknown>) => { try { await f(); } catch (e: any) { console.error('alarm', name, room.code, e?.message ?? e); } };
    try {
      await step('entries', async () => {
        for (const p of Object.values(room.players)) if (p.pending) await this.checkEntry(p);
        for (const o of [...(room.orphans ?? [])]) await this.checkOrphan(o);
        // a 'paying' hold that ended with no payment delivered: look for the transfer on-chain by its memo
        for (const w of (room.watch ?? []).filter(w => w.at <= t)) {
          room.watch = room.watch?.filter(x => x !== w);
          const p = Object.values(room.players).find(x => x.wallet === w.wallet);
          if (!(p?.paid && w.round === room.round)) await this.scan(w.wallet, w.round);
        }
      });
      await step('lobby', async () => {
        if (room.state === 'lobby' && (t - room.createdAt > LOBBY_TTL_MS || (room.emptySince && t - room.emptySince > EMPTY_TTL_MS))) await this.cancel('Room expired');
      });
      await step('loading', async () => {
        if (room.state !== 'loading' || !room.loadDeadline || t < room.loadDeadline) return;
        const here = Object.values(room.players).filter(x => x.loaded && !x.left);
        if (here.length >= 1) await this.beginCountdown();
        else if (room.standings?.length) await this.abandonCup('Nobody loaded the next track');   // mid-cup: settle what was raced
        else { room.state = 'lobby'; room.loadDeadline = undefined; }                             // before the first race: back to the lobby, seats and entries kept
      });
      await step('results', async () => {
        if (room.state === 'results' && room.nextAt && t >= room.nextAt) {
          if (Object.values(room.players).some(x => x.connected)) await this.beginLoading(room.raceIndex + 1);
          else await this.abandonCup('Everyone left the cup');
        }
      });
      await step('race', async () => {
        if ((room.state === 'racing' || room.state === 'countdown') && room.finishDeadline && t >= room.finishDeadline) await this.finishRace();
      });
      await step('empty', async () => {
        if (room.state === 'results' && room.emptySince && t - room.emptySince > EMPTY_TTL_MS) await this.abandonCup('Everyone left the cup');   // the races run decide it; the pool is paid, not kept
      });
      await step('settle', async () => {
        if (room.payouts.some(p => (p.status === 'pending' && !(p.nextTry && p.nextTry > t)) || p.status === 'sent')) await this.settle();
      });
    } finally {
      await this.save().catch(e => console.error('alarm save', room.code, e?.message ?? e));
      this.broadcastRoom();
      await this.schedule().catch(e => console.error('alarm schedule', room.code, e?.message ?? e));
    }
  }

  async schedule() {
    const room = this.room; if (!room) return;
    const t = nowMs();
    const times: number[] = [];
    if (room.state === 'lobby') times.push(room.createdAt + LOBBY_TTL_MS, ...(room.emptySince ? [room.emptySince + EMPTY_TTL_MS] : []));
    if (room.state === 'loading' && room.loadDeadline) times.push(room.loadDeadline);
    if ((room.state === 'racing' || room.state === 'countdown') && room.finishDeadline) times.push(room.finishDeadline);
    if (room.state === 'results' && room.emptySince) times.push(room.emptySince + EMPTY_TTL_MS);
    if (room.state === 'results' && room.nextAt) times.push(room.nextAt);
    for (const e of [...Object.values(room.players).map(p => p.pending), ...(room.orphans ?? [])]) if (e) times.push(e.nextTry ?? t + ENTRY_RETRY_MS);
    for (const p of Object.values(room.players)) if ((p.payingUntil ?? 0) > t) times.push(p.payingUntil!);   // its end is broadcast
    for (const w of room.watch ?? []) times.push(w.at);
    for (const p of room.payouts) {
      if (p.status === 'sent') times.push(t + SETTLE_RETRY_MS);
      else if (p.status === 'pending') times.push(p.nextTry ?? t + SETTLE_RETRY_MS);
    }
    if (!times.length) { await this.ctx.storage.deleteAlarm(); return; }
    // Never later than an alarm already booked: anyone who can read the room (GET /state is public) would otherwise
    // keep pushing a payout's alarm into the future by asking often enough.
    const next = Math.max(t + 250, Math.min(...times)), cur = await this.ctx.storage.getAlarm();
    if (cur !== null && cur > t && cur <= next) return;
    await this.ctx.storage.setAlarm(next);
  }

  // ---------- what everyone sees ----------
  snapshot() {
    const room = this.room!;
    const sp = this.split();
    const lobbyPaid = room.state === 'lobby' && room.fee !== '0';
    return {
      code: room.code, state: room.state, round: room.round, tracks: room.tracks, raceIndex: room.raceIndex,
      fee: room.fee, currency: room.currency, cutBps: room.cutBps, feeBps: sp.feeBps, hostReward: sp.hostReward.toString(), isPublic: room.isPublic,
      hostId: room.hostId, startAt: room.startAt ?? null, nextAt: room.nextAt ?? null,
      serverNow: nowMs(), pool: this.pool().toString(), prizes: sp.prizes.map(p => p.toString()),
      players: Object.values(room.players).sort((a, b) => a.joinedAt - b.joinedAt).map(p => ({
        id: p.id, name: p.name, wallet: p.wallet, character: p.character, kart: p.kart, host: p.host, connected: p.connected,
        ready: p.ready, paid: p.paid, paying: this.paying(p), loaded: p.loaded, racing: p.racing,
        // whether this seat may pay in now: the phone shows PAY only then, so no transfer is made that cannot buy it
        canPay: lobbyPaid && !p.paid && !p.pending && !!p.wallet && (room.refunds?.[p.wallet] ?? 0) < MAX_REFUNDS,
        finishMs: p.finishMs ?? null, dnf: p.dnf, place: p.place ?? null, points: p.points,
      })),
      standings: room.standings ?? null, lastResults: room.lastResults ?? null, payouts: this.publicPayouts(),
    };
  }
}

export function validFee(fee: string, currency: Currency) { return FEE_TIERS[currency]?.includes(fee) ?? false; }

// The five karts every racer has (Assets/_Game/Data/Kart_*.asset with no sku); every other kart is a shop sku.
const FREE_KARTS = new Set(['oodi', 'ooli', 'oobi', 'oopi', 'oozi']);
const SHOP_KARTS = new Set(CATALOG.filter(s => s.kind === 'kart').map(s => s.sku));

/** A guest's id in a room: stable per phone, but not the phone's id itself, which would let anyone who reads a
 *  snapshot say hello as that phone and take its seat. */
export async function guestId(device: string): Promise<string> {
  const h = new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode('kc-guest:' + device)));
  return 'g:' + Array.from(h.slice(0, 8), b => b.toString(16).padStart(2, '0')).join('');
}

/** "IN" or "US-AR" style entries in PAID_BLOCK; geo is "CC" or "CC-RR" from the request's cf object. */
export function geoBlocked(env: Env, geo: string): boolean {
  if (!geo) return false;
  const list = (env.PAID_BLOCK ?? '').split(',').map(s => s.trim().toUpperCase()).filter(Boolean);
  const [country] = geo.toUpperCase().split('-');
  return list.includes(country) || list.includes(geo.toUpperCase());
}
