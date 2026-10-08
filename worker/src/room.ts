// One Durable Object per room: the lobby, the 15 Hz state relay, the server-timed countdown, the finish order, cup
// points across races, and for a paid room its escrow on-chain. It is the only place that decides who won, which is
// why it is one object and not a table plus a race condition. It never holds anyone's money: a paid room's stakes sit
// in the escrow program (escrow/programs/kartel_escrow), and this object only drives that account through its life
// (open, refund a seat, lock, settle or cancel, deliver every payout, close) with the referee key.
//
// Protocol (JSON text frames). Client → room:
//   hello {device, session?, character, kart}   ready {ready}   start   paying {off?}   pay {signature}   loaded
//   s {p,r,v,st,lap,cp,sc,tm}  (relayed to everyone else with the sender's id)   fin
//   ping {c}   spr {s} (echo of the room's sp)   next   again   leave   kick {id}
// Room → client:
//   welcome {id, serverNow, room}   room {…snapshot}   load {track, race, races}   countdown {startAt}
//   s {id,…}   finished {id, ms}   results {…}   payouts [...]   pong {c, s}   sp {s} (the room timing a round trip)   error {msg}   closed {reason}
// HTTP from the Worker: POST /init, GET /state.
import { DurableObject } from 'cloudflare:workers';
import type { Env } from './index';
import { readSession } from './auth';
import { MIN_PAID_PLAYERS, CATALOG, STAKE_UNIT, type Currency } from './catalog';
import { geoOf, newRef, push, finishT, finished, crossingsToFinish, type RefState } from './referee';
import { Referee, readRoom, type ChainRoom, type EscrowState } from './escrow';
import { sendRaw, txStatus } from './rpc';
import { ifWin } from './fairshares';

const POINTS = [10, 8, 6, 4, 3, 2];
/** Seats per room: every track has six grid slots (TrackSetup.StartGrid) and the phone is measured racing six karts.
 *  The escrow program enforces the same number on-chain (MAX_PLAYERS). */
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
const COUNTDOWN_LEAD_MS = 8_000;       // the prize-pool shot (about 5 s) then 3-2-1, on every phone at once
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
// A seat is held (no drop, no kick) while its phone is in the wallet app staking: this many times per wallet per round.
const PAYING_HOLD_MS = 120_000, PAYING_HOLDS = 3;
// Stake, get it back, stake again: each refund is a transaction the referee pays for. Two per wallet per round.
const MAX_REFUNDS = 2;
// The escrow: one transaction at a time per round, its status asked every CHAIN_TX_POLL_MS. A failed step is tried
// again after CHAIN_RETRY_MS, doubling to CHAIN_MAX_BACKOFF_MS. While a lobby takes stakes the account is read every
// CHAIN_POLL_PAYING_MS if someone is in their wallet app, else every CHAIN_POLL_LOBBY_MS (a stake made while the game
// was closed is still found).
const CHAIN_TX_POLL_MS = 1_000, CHAIN_RETRY_MS = 2_000, CHAIN_MAX_BACKOFF_MS = 5 * 60_000;
// A refund or payout that cannot land yet (a closed token account) waits on its own clock, up to this: it may take a
// player days to have an account again, and the escrow goes on without it.
const STUCK_MAX_BACKOFF_MS = 6 * 3600_000;
const CHAIN_POLL_PAYING_MS = 2_000, CHAIN_POLL_LOBBY_MS = 15_000;
const LOCK_TIMEOUT_MS = 45_000;        // the stakes not locked by then: the room is cancelled and every stake goes back
const ESCROW_TTL_S = 3 * 3600;         // after this anyone may cancel the escrow on-chain: stakes never depend on us
// The longest a race of a cup can take, load to results: the escrow must have this much left per race (and a margin)
// when it is locked, or a loser could cancel it after expiry while the cup is still on.
const RACE_BOUND_MS = LOAD_TIMEOUT_MS + COUNTDOWN_LEAD_MS + RACE_CAP_MS + RESULTS_MS, LOCK_MARGIN_MS = 10 * 60_000;

type State = 'lobby' | 'loading' | 'countdown' | 'racing' | 'results' | 'finished' | 'cancelled';

type Player = {
  id: string; wallet: string | null; device: string; name: string;
  character: number; kart: string;
  joinedAt: number; host: boolean; connected: boolean; ready: boolean; left?: boolean;
  /** A stake in this round's escrow, as last read from the chain (always true in a free room). */
  paid: boolean; stake?: string; payingUntil?: number;
  loaded: boolean; racing: boolean;
  finishMs?: number; dnf: boolean; lastState?: number;
  ref?: RefState; flagged?: string;      // the referee's view of this race, and why it stopped believing the drive
  // the race clock's least lag behind the room (s) per LAG_BUCKET_MS [start ms, lag]; the round trip: the sp waiting
  // for its echo, the best round trip (ms); finLag: the clock judged at the finishing crossing ('' = fine)
  lags?: number[][]; spOut?: number[]; rttMin?: number; lastSp?: number; finLag?: string;
  flagAt?: number[];                     // [gates, lap distance] when the referee flagged it: a caught cheat ranks no further
  place?: number; points: number; totalMs: number; wins: number;
};

type Order = { id: string; name: string; ms: number | null; place: number; points: number; dnf: boolean };
type RaceResult = { race: number; races: number; track: string; final: boolean; order: Order[] };
type Standing = { id: string; name: string; points: number; wins: number; totalMs: number; place: number };

/** The escrow account as last read: amounts as decimal strings, so the record survives storage as JSON. */
type Seen = {
  state: EscrowState; mint: string | null; expiresAt: number; payer: string; treasury: string; houseCut: string; cutPaid: boolean;
  entries: { player: string; stake: string; payout: string; paid: boolean; refund: boolean }[];
};

/** One round's escrow and where this room is driving it. */
type Escrow = {
  round: number; address: string;
  want: 'open' | 'locked' | 'settled' | 'cancelled';
  field?: string[];                      // want 'locked': the wallets that may keep a stake (the loaded paid seats)
  order?: string[];                      // want 'settled': every staked wallet, the winner first
  seen?: Seen;                           // the account as last read; kept after it closes, for the results card
  created?: boolean;                     // known to exist on-chain (its create landed, or it was read)
  gone?: boolean;                        // closed on-chain, or never created and no longer wanted: nothing left to do
  tx?: { op: string; sig: string; lastValid: number; at: number };   // the one transaction in flight
  failures: number; nextTry?: number; error?: string; readAt?: number;
  claims: Record<string, string>;        // wallet → the transaction that delivered its payout
  // A refund or payout that cannot land yet (a closed token account) waits on its own, so it never holds up anyone
  // else's, the lock or the cut: its op ("remove:<wallet>", "claim:<wallet>") → when to try again, and how often.
  stuck?: Record<string, { until: number; tries: number }>;
};

type Room = {
  code: string; createdAt: number; round: number;
  hostId: string; organiser?: string; tracks: string[]; raceIndex: number;
  /** Base units; '0' for a free room. Each player stakes from minStake to maxStake. */
  minStake: string; maxStake: string; currency: Currency; cutBps: number; isPublic: boolean;
  state: State; players: Record<string, Player>;
  startAt?: number; loadDeadline?: number; finishDeadline?: number; emptySince?: number; nextAt?: number;
  lockDeadline?: number;                 // race one of a paid room waits for the stakes to lock until then
  lastResults?: RaceResult; standings?: Standing[];
  escrows: Escrow[];                     // this round's, and any earlier round's still paying out
  kicked?: string[];                     // removed by the host this round: they may not come back until the rematch
  refunds?: Record<string, number>;      // stakes handed back per wallet this round
  holds?: Record<string, number>;        // seat holds (paying) per wallet this round
};

const nowMs = () => Date.now();
const short = (w: string) => `${w.slice(0, 4)}…${w.slice(-4)}`;
const num = (v: unknown) => { const n = Number(v); return Number.isFinite(n) ? n : 0; };
const nums = (v: unknown, n: number) => Array.isArray(v) && v.length >= n ? v.slice(0, n).map(num) : undefined;
const sum = (xs: (string | bigint)[]) => xs.reduce<bigint>((a, b) => a + BigInt(b), 0n);

export class RoomDO extends DurableObject<Env> {
  room: Room | null = null;
  // In memory only: gone with the object, which also ends anything they guard.
  greeting = new WeakSet<WebSocket>();   // a hello in flight on this socket
  gone = new WeakSet<WebSocket>();       // closed: a hello still in flight on it must not seat anyone
  frames = new WeakMap<WebSocket, { n: number; at: number }>();
  buckets = new WeakMap<WebSocket, { n: number; at: number }>();
  starting = false;                      // start() is between its first await and the load going out
  stepping = new Set<number>();          // escrow rounds with a chain step under way: never two at once
  referee?: Referee;

  // ---------- HTTP: init/state from the Worker, WebSocket upgrade from the phones ----------
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
        minStake: b.minStake, maxStake: b.maxStake, currency: b.currency, cutBps: b.cutBps, isPublic: !!b.isPublic,
        state: 'lobby', players: {}, escrows: [],
      };
      if (this.paidRoom()) this.openEscrow();
      await this.save();
      await this.schedule();
      return Response.json(this.snapshot());
    }
    if (url.pathname === '/state') {
      if (!this.room) return Response.json({ error: 'no such room' }, { status: 404 });
      if (this.owes()) await this.schedule();              // a room nobody visits still pays out what it owes
      return Response.json(this.snapshot());
    }
    return new Response('not found', { status: 404 });
  }

  async load() {
    if (this.room) return;
    const stored = await this.ctx.storage.get<any>('room');
    if (stored && !('minStake' in stored)) {
      // Written before the escrow (paid rooms were off then): a single fee becomes the stake range.
      stored.minStake = stored.maxStake = stored.fee ?? '0';
      stored.escrows = [];
      delete stored.fee; delete stored.payouts;
    }
    this.room = stored ?? null;
  }
  async save() { if (this.room) await this.ctx.storage.put('room', this.room); }

  paidRoom() { return this.room!.minStake !== '0'; }

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
        // the phone is about to open the wallet app: hold the seat until the stake lands (or two minutes pass)
        if (!this.canStake(p) || !this.takeHold(p.wallet!)) return;
        p.payingUntil = nowMs() + PAYING_HOLD_MS;
        break;
      }
      case 'pay': {
        // The wallet sent a stake: read the escrow now rather than at the next poll. The chain, not this message,
        // decides whether the seat is paid.
        const e = this.escrowFor(room.round);
        if (e) e.nextTry = undefined;
        break;
      }
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

  paying(p: Player) { return (p.payingUntil ?? 0) > nowMs(); }

  /** May this seat put a stake in now: a signed-in seat of a paid room's lobby, with no stake yet, whose escrow is
   *  open, and that has not already had its stake handed back MAX_REFUNDS times this round. */
  canStake(p: Player): boolean {
    const room = this.room!;
    return room.state === 'lobby' && this.paidRoom() && !p.paid && !!p.wallet
      && this.escrowFor(room.round)?.seen?.state === 'open' && (room.refunds?.[p.wallet] ?? 0) < MAX_REFUNDS;
  }

  async dropped(ws: WebSocket) {
    await this.load();
    const room = this.room; if (!room) return;
    const id = ws.deserializeAttachment()?.id; if (!id) return;
    const p = room.players[id]; if (!p) return;
    // A replaced socket (same player reconnected) must not mark the live one as gone.
    if (this.socketsFor(id).some(s => s !== ws)) return;
    p.connected = false;
    // In the lobby a seat nobody staked for (or any seat of a free room) is given up. A staked one waits for its owner
    // (start() hands the stake back if they are still away at the flag), and so does one whose owner is staking now.
    if (room.state === 'lobby' && (!p.paid || !this.paidRoom()) && !this.paying(p)) await this.remove(p, 'left');
    else if (p.host) this.migrateHost();      // a crashed host must not freeze a cup: whoever is still here runs it
    if (!Object.values(room.players).some(x => x.connected)) room.emptySince = nowMs();
    if ((room.state === 'racing' || room.state === 'countdown') && this.raceDone()) await this.finishRace();
    if (room.state === 'loading' && !room.lockDeadline && this.loadWaitsFor().length && this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
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
    if (!wallet && this.paidRoom()) return this.refuse(ws, 'Sign in with your wallet to join a paid room');
    const geo: string = ws.deserializeAttachment()?.geo ?? '';
    const id = wallet ?? await guestId(device);
    const away = () => this.gone.has(ws) || ws.readyState !== WebSocket.OPEN;   // the socket closed during an await
    // One player per socket: a second hello under another name would leave the first seated with nobody behind it.
    const prev = ws.deserializeAttachment()?.id;
    if (prev && prev !== id) { this.send(ws, { t: 'error', msg: 'This connection already joined as someone else' }); return; }
    // Only a new seat is checked against the region: a seated paid racer whose reconnect comes from an address placed
    // elsewhere (a Wi-Fi to cellular handover) is let back in to the race it staked on.
    const closed = () => room.kicked?.includes(id) ? 'The host removed you from this room'
      : room.state !== 'lobby' || this.starting ? 'This race has already started'
      : Object.keys(room.players).length >= MAX_PLAYERS ? 'Room is full'
      : this.paidRoom() && geoBlocked(this.env, geo) ? 'Paid races are not available in your region' : null;
    let p = room.players[id];
    if (!p) {
      const no = closed(); if (no) return this.refuse(ws, no);
      const fresh: Player = {
        id, wallet, device, name: await this.displayName(wallet, device),
        character: 0, kart: 'oodi', joinedAt: nowMs(), host: false, connected: true, ready: false,
        paid: !this.paidRoom(), loaded: false, racing: false, dnf: false, points: 0, totalMs: 0, wins: 0,
      };
      if (away()) return;                                     // gone during that await: no seat for nobody
      p = room.players[id];                                   // the same player may have joined on another socket meanwhile
      if (!p) {
        const again = closed(); if (again) return this.refuse(ws, again);   // ...and the room may have moved on meanwhile
        p = room.players[id] = fresh;
        this.applySeats();                                    // a stake made before this seat existed (a rejoin) counts at once
      }
    }
    if (away()) return;
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

  // ---------- starting ----------
  async start(ws: WebSocket, p: Player): Promise<boolean> {
    const room = this.room!;
    if (!p.host || room.state !== 'lobby') return false;
    const here = Object.values(room.players).filter(x => x.connected);
    if (here.length < 2) { this.send(ws, { t: 'error', msg: 'You need at least 2 racers' }); return false; }
    if (this.paidRoom()) {
      if (this.env.PAID_ROOMS !== '1') { this.send(ws, { t: 'error', msg: 'Paid rooms are switched off right now' }); return false; }
      const waiting = Object.values(room.players).filter(x => this.paying(x) || (x.connected && !x.paid));
      if (waiting.length) { this.send(ws, { t: 'error', msg: `Waiting for ${waiting.map(x => x.name).join(', ')} to stake` }); return false; }
      if (here.length < MIN_PAID_PLAYERS) { this.send(ws, { t: 'error', msg: `A paid race needs ${MIN_PAID_PLAYERS} racers` }); return false; }
    }
    // From here the lobby is closed: nobody new may join while the awaits below run.
    this.starting = true;
    try {
      // Anyone away when the flag drops is out of this round: a stake they hold is handed back.
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
    room.startAt = undefined; room.finishDeadline = undefined; room.lockDeadline = undefined;
    room.loadDeadline = nowMs() + LOAD_TIMEOUT_MS;
    room.nextAt = undefined;
    for (const x of Object.values(room.players)) { x.loaded = false; x.racing = false; x.finishMs = undefined; x.dnf = false; x.ready = false; x.ref = undefined; x.flagged = undefined; x.flagAt = undefined; x.finLag = undefined; }
    this.broadcast({ t: 'load', track: room.tracks[raceIndex], race: raceIndex + 1, races: room.tracks.length });
  }

  /** Who the track load waits for: everyone here, and in a paid room every staked seat whose phone may be reconnecting. */
  loadWaitsFor(): Player[] {
    const room = this.room!;
    return Object.values(room.players).filter(x => !x.left && (x.connected || (this.paidRoom() && x.paid)));
  }

  async loaded(p: Player) {
    const room = this.room!;
    if (room.state !== 'loading' || room.lockDeadline) return;
    p.loaded = true;
    if (this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
  }

  async beginCountdown() {
    const room = this.room!;
    if (this.paidRoom() && room.raceIndex === 0) {
      // Race one of a paid room: a staked seat whose phone never loaded the track has not raced. It is out of the
      // cup, and its stake goes back (the escrow step hands back any stake whose seat is gone).
      for (const x of Object.values(room.players)) {
        if (x.loaded && !x.left && x.paid) continue;
        delete room.players[x.id];
        for (const s of this.socketsFor(x.id)) { this.send(s, { t: 'closed', reason: 'You were not on the grid in time: your stake is going back to you' }); try { s.close(1000, 'missed the start'); } catch { /* gone */ } }
      }
      if (!room.players[room.hostId]?.host) this.migrateHost();
      const field = Object.values(room.players).filter(x => x.paid && x.wallet);
      if (field.length < MIN_PAID_PLAYERS) { await this.backToLobby('Not enough racers made it to the grid: back to the lobby'); return; }
      // The stakes are locked on-chain before anyone races for them: the countdown starts when the lock lands. Never
      // into an escrow that could expire before the cup is over (anyone may cancel an expired one).
      const e = this.escrowFor(room.round)!;
      if (!e.seen || e.seen.expiresAt * 1000 - nowMs() < room.tracks.length * RACE_BOUND_MS + LOCK_MARGIN_MS) {
        await this.cancel('This room is too old to race for stakes: every stake is going back');
        return;
      }
      e.want = 'locked'; e.field = field.map(x => x.wallet!); e.nextTry = undefined;
      room.lockDeadline = nowMs() + LOCK_TIMEOUT_MS; room.loadDeadline = undefined;
      return;
    }
    this.startCountdown();
  }

  /** Back to the lobby before the first race: the seats still here keep their stakes and the escrow stays open. */
  async backToLobby(msg: string) {
    const room = this.room!;
    // (The lobby keeps its original deadline: failing starts must not keep a room alive while its escrow ages.)
    room.state = 'lobby'; room.loadDeadline = undefined; room.lockDeadline = undefined;
    const e = this.escrowFor(room.round);
    if (e && e.want === 'locked') { e.want = 'open'; e.field = undefined; }
    for (const x of Object.values(room.players)) x.loaded = false;
    this.broadcast({ t: 'error', msg });
    await this.env.DB.prepare("UPDATE rooms SET state = 'lobby' WHERE code = ?1").bind(room.code).run();
  }

  startCountdown() {
    const room = this.room!;
    const paying = this.paidRoom();
    room.state = 'countdown';
    room.loadDeadline = undefined; room.lockDeadline = undefined;
    room.startAt = nowMs() + COUNTDOWN_LEAD_MS;
    room.finishDeadline = room.startAt + RACE_CAP_MS;
    for (const x of Object.values(room.players)) {
      // A loaded phone races, connected or not this instant (a blip on the grid is a reconnect, not a withdrawal);
      // in a paid room only a staked seat does.
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
    this.broadcast({ t: 'results', results: room.lastResults, standings: room.standings, payouts: this.publicPayouts(), pool: this.pool().toString(), room: this.snapshot() });
    await this.save();
    await this.schedule();
  }

  /** The cup is over: tell the escrow the finishing order of every staked wallet (the cup's standings, then anyone
   *  staked who scored nothing, by join order). The program alone works out what that order pays. With fewer than two
   *  stakes, or a cup in which nobody scored, there was no contest: every stake goes back. */
  async settleFinal() {
    const room = this.room!;
    const e = this.escrowFor(room.round);
    if (!e) return;
    const staked = (e.seen?.entries ?? []).filter(x => !x.refund).map(x => x.player);   // those left out at the lock are refunded
    const ranked = (room.standings ?? []).map(s => room.players[s.id]?.wallet).filter((w): w is string => !!w && staked.includes(w));
    const order = [...new Set([...ranked, ...staked])];
    const contest = (room.standings ?? []).some(s => s.points > 0);
    if (e.seen?.state === 'locked' && order.length >= MIN_PAID_PLAYERS && contest) { e.want = 'settled'; e.order = order; }
    else e.want = 'cancelled';
    e.nextTry = undefined;
    await this.env.DB.prepare("UPDATE rooms SET state = 'finished', pool = ?2, finished_at = ?3 WHERE code = ?1")
      .bind(room.code, this.pool().toString(), Math.floor(nowMs() / 1000)).run();
  }

  /** A cup nobody can carry on (everyone left, nobody loaded the next track): the races already run decide it and it
   *  pays out on those standings, the same as a finished cup. Before any race has run, everyone is refunded. */
  async abandonCup(reason: string) {
    const room = this.room!;
    if (!room.standings?.length) { await this.cancel(reason); return; }
    room.state = 'finished'; room.nextAt = undefined; room.loadDeadline = undefined; room.lockDeadline = undefined;
    if (room.lastResults) room.lastResults.final = true;
    await this.settleFinal();
    this.broadcast({ t: 'results', results: room.lastResults, standings: room.standings, payouts: this.publicPayouts(), pool: this.pool().toString(), room: this.snapshot() });
  }

  // ---------- leaving, cancelling, the rematch ----------
  async remove(p: Player, reason: string) {
    const room = this.room!;
    if (room.state === 'lobby' || room.state === 'finished' || room.state === 'cancelled') {
      // In the lobby its stake goes back: the escrow step hands back every stake whose seat is gone.
      if (room.state === 'lobby' && p.paid && p.wallet && this.paidRoom()) room.refunds = { ...(room.refunds ?? {}), [p.wallet]: (room.refunds?.[p.wallet] ?? 0) + 1 };
      delete room.players[p.id];
    } else {
      p.connected = false; p.left = true; p.dnf = p.finishMs === undefined;   // mid-race: they are still in the standings, as a DNF
      // Gone before the lock was sent: out of the field, so the lock leaves the stake out and it goes straight back.
      const e = this.escrowFor(room.round);
      if (room.lockDeadline && e?.want === 'locked' && e.tx?.op !== 'lock' && p.wallet) e.field = e.field?.filter(w => w !== p.wallet);
    }
    for (const s of this.socketsFor(p.id)) { this.send(s, { t: 'closed', reason }); try { s.close(1000, reason); } catch { /* gone */ } }
    if (p.host) this.migrateHost();
    if ((room.state === 'racing' || room.state === 'countdown') && this.raceDone()) await this.finishRace();
    if (room.state === 'loading' && !room.lockDeadline && this.loadWaitsFor().length && this.loadWaitsFor().every(x => x.loaded)) await this.beginCountdown();
  }

  migrateHost() {
    const room = this.room!;
    const next = Object.values(room.players).filter(x => x.connected && this.socketsFor(x.id).length).sort((a, b) => a.joinedAt - b.joinedAt)[0];
    for (const x of Object.values(room.players)) x.host = false;
    if (next) { next.host = true; room.hostId = next.id; }
  }

  async cancel(reason: string) {
    const room = this.room!;
    room.state = 'cancelled'; room.lockDeadline = undefined;
    const e = this.escrowFor(room.round);
    if (e && e.want !== 'settled') { e.want = 'cancelled'; e.nextTry = undefined; }
    await this.env.DB.prepare("UPDATE rooms SET state = 'cancelled' WHERE code = ?1").bind(room.code).run();
    this.broadcast({ t: 'closed', reason, payouts: this.publicPayouts() });
  }

  /** Same code, same people, new round: the group stays together for a rematch, with a new escrow for the new
   *  round's stakes. The old round's escrow goes on paying out until it closes. */
  reset() {
    const room = this.room!;
    room.round += 1; room.raceIndex = 0; room.state = 'lobby';
    room.startAt = room.loadDeadline = room.finishDeadline = room.nextAt = room.lockDeadline = undefined;
    room.standings = undefined; room.lastResults = undefined; room.createdAt = nowMs();
    room.kicked = undefined; room.refunds = undefined; room.holds = undefined;
    for (const p of Object.values(room.players)) {
      p.paid = !this.paidRoom(); p.stake = undefined; p.payingUntil = undefined; p.ready = false; p.left = false;
      p.loaded = false; p.racing = false; p.finishMs = undefined; p.dnf = false; p.place = undefined; p.points = 0; p.totalMs = 0;
      if (!p.connected) delete room.players[p.id];
    }
    if (this.paidRoom()) this.openEscrow();
    void this.env.DB.prepare("UPDATE rooms SET state = 'lobby', round = ?2 WHERE code = ?1").bind(room.code, room.round).run();
  }

  // ---------- the escrow ----------
  ref(): Referee { return this.referee ??= new Referee(this.env); }
  mint(): string | null { return this.room!.currency === 'SKR' ? this.env.SKR_MINT : null; }
  escrowFor(round: number): Escrow | undefined { return this.room!.escrows.find(e => e.round === round); }

  openEscrow() {
    const room = this.room!;
    room.escrows.push({ round: room.round, address: this.ref().room(room.code, room.round).toBase58(), want: 'open', failures: 0, claims: {} });
  }

  /** The pot of this round: every stake in its escrow that races (not those left out at the lock). */
  pool(): bigint { return sum((this.escrowFor(this.room!.round)?.seen?.entries ?? []).filter(x => !x.refund).map(x => x.stake)); }

  /** Who may keep a stake in this open escrow: every seat still in the lobby (that has not used up its refunds).
   *  Anyone else's stake is handed back. (At the lock, stakes outside the field are left out by the lock itself.) */
  allowed(e: Escrow): Set<string> {
    const room = this.room!;
    if (e.round !== room.round || e.want !== 'open') return new Set();
    return new Set(Object.values(room.players)
      .filter(p => p.wallet && !p.left && (p.paid || (room.refunds?.[p.wallet] ?? 0) < MAX_REFUNDS))
      .map(p => p.wallet!));
  }

  /** The next transaction this escrow needs, from what the chain holds and where the room wants it; null when there
   *  is nothing to do now. Each step is safe to repeat: the program refuses one that no longer applies. */
  nextStep(e: Escrow): { op: string; build: () => ReturnType<Referee['createRoom']>; cu: number } | null {
    const room = this.room!, r = this.ref(), s = e.seen, mint = this.mint(), address = this.ref().room(room.code, e.round);
    const token = !!mint;
    const ready = (op: string) => (e.stuck?.[op]?.until ?? 0) <= nowMs();
    const claim = (player: string) => ({ op: `claim:${player}`, cu: token ? 60_000 : 30_000, build: () => [r.claim(address, mint, player)] });
    if (e.gone) return null;
    if (!s) {
      if (!e.created && (e.want === 'open' || e.want === 'locked')) {
        return { op: 'create', cu: token ? 90_000 : 40_000, build: () => r.createRoom({
          code: room.code, round: e.round, mint, minStake: BigInt(room.minStake), maxStake: BigInt(room.maxStake),
          stakeUnit: BigInt(STAKE_UNIT[room.currency]), maxPlayers: MAX_PLAYERS, cutBps: room.cutBps, ttlSecs: ESCROW_TTL_S,
        }) };
      }
      return null;
    }
    switch (s.state) {
      case 'open': {
        // Cancelling makes every stake claimable at once: no seat-by-seat refunds first.
        if (e.want === 'cancelled' || e.want === 'settled') return { op: 'cancel', cu: 20_000, build: () => [r.cancel(address)] };
        if (e.want === 'locked') {
          // The field, as far as it is staked. Any other stake is left out by the lock and paid straight back, so no
          // stake arriving on the way can hold the lock up.
          const players = (e.field ?? []).filter(w => s.entries.some(x => x.player === w));
          return players.length >= MIN_PAID_PLAYERS ? { op: 'lock', cu: 30_000, build: () => [r.lock(address, players)] } : null;
        }
        const allowed = this.allowed(e);
        const stray = s.entries.find(x => !allowed.has(x.player) && ready(`remove:${x.player}`));
        if (stray) return { op: `remove:${stray.player}`, cu: token ? 60_000 : 30_000, build: () => [r.removeEntry(address, mint, stray.player)] };
        return null;
      }
      case 'locked': {
        const left = s.entries.find(x => x.refund && !x.paid && ready(`claim:${x.player}`));
        if (left) return claim(left.player);   // a stake left out of the race goes back before the race is run
        if (e.want === 'settled' && e.order) { const order = e.order; return { op: 'settle', cu: 60_000, build: () => [r.settle(address, order)] }; }
        if (e.want === 'cancelled') return { op: 'cancel', cu: 20_000, build: () => [r.cancel(address)] };
        return null;
      }
      default: {   // settled or cancelled: deliver every payout, the cut, then close
        const owed = s.entries.filter(x => !x.paid && BigInt(x.payout) > 0n);
        const next = owed.find(x => ready(`claim:${x.player}`));
        if (next) return claim(next.player);
        if (s.state === 'settled' && BigInt(s.houseCut) > 0n && !s.cutPaid) return { op: 'collect', cu: token ? 60_000 : 30_000, build: () => [r.collectCut(address, mint, s.treasury)] };
        if (owed.length) return null;    // only payouts that cannot land yet: each is tried again on its own clock
        return { op: 'close', cu: token ? 60_000 : 30_000, build: () => [r.closeRoom(address, mint, s.payer, s.treasury)] };
      }
    }
  }

  /** One turn of an escrow: settle the transaction in flight, read the account, act on what it holds. One transaction
   *  at a time per escrow, its signature saved BEFORE it is sent: a restart or a lost answer looks it up instead of
   *  sending another. */
  async chainStep(e: Escrow) {
    if (e.gone || this.stepping.has(e.round)) return;
    this.stepping.add(e.round);
    const room = this.room!;
    try {
      if (e.tx) {
        const st = await txStatus(this.env, e.tx.sig, e.tx.lastValid);
        if (st === 'pending') return;
        if (st === 'confirmed') {
          if (e.tx.op.startsWith('claim:')) e.claims[e.tx.op.slice(6)] = e.tx.sig;
          if (e.tx.op === 'create') e.created = true;
          if (e.stuck) delete e.stuck[e.tx.op];
          e.failures = 0; e.error = undefined;
        } else this.failed(e, `${e.tx.op} ${st}`, e.tx.op);
        e.tx = undefined;
      }
      if (e.nextTry && e.nextTry > nowMs()) return;
      const chain = await readRoom(this.env, e.address);
      e.readAt = nowMs();
      if (chain) { e.seen = seenOf(chain); e.created = true; }
      else if (!e.created) {
        if (e.want !== 'open' && e.want !== 'locked') { e.gone = true; return; }   // never created, no longer wanted
      } else {
        // Closed, if nothing was owed when last read (or it is long expired): only then. A node a few slots behind the
        // others also answers "no account", and giving up on a live escrow would strand its stakes.
        const last = e.seen;
        if (last && (((last.state === 'settled' || last.state === 'cancelled') && !owedOn(last)) || nowMs() > last.expiresAt * 1000 + 3600_000)) { e.gone = true; return; }
        throw new Error('the escrow account was not found (a node behind the others?)');
      }
      if (e.seen) await this.onSeen(e);
      const step = this.nextStep(e);
      if (!step) return;
      const built = await this.ref().sign(step.build(), step.cu);
      e.tx = { op: step.op, sig: built.signature, lastValid: built.lastValidBlockHeight, at: nowMs() };
      await this.save();
      let sent: Awaited<ReturnType<typeof sendRaw>>;
      try { sent = await sendRaw(this.env, built.raw); }
      catch (err: any) { console.error('escrow send', room.code, e.round, step.op, err?.message ?? err); return; }   // it may still land: the status decides
      if (sent !== 'sent') { e.tx = undefined; this.failed(e, `${step.op} refused: ${sent.rejected}`, step.op); }
    } catch (err: any) {
      this.failed(e, String(err?.message ?? err));
    } finally {
      this.stepping.delete(e.round);
    }
  }

  /** A step that did not land: one wallet's refund or payout backs off on its own (the escrow goes on with the rest),
   *  anything else backs the whole escrow off. */
  failed(e: Escrow, why: string, op?: string) {
    const backoff = (tries: number, cap: number) => Math.min(cap, CHAIN_RETRY_MS * 2 ** Math.min(tries - 1, 20));
    console.error('escrow', this.room?.code, e.round, why);
    if (op?.startsWith('claim:') || op?.startsWith('remove:')) {
      const tries = (e.stuck?.[op]?.tries ?? 0) + 1;
      e.stuck = { ...(e.stuck ?? {}), [op]: { until: nowMs() + backoff(tries, STUCK_MAX_BACKOFF_MS), tries } };
      return;
    }
    e.failures += 1; e.error = why.slice(0, 300);
    e.nextTry = nowMs() + backoff(e.failures, CHAIN_MAX_BACKOFF_MS);
  }

  /** What the room does with a fresh read of its escrow: the seats' stakes, the lock landing, a field too small. */
  async onSeen(e: Escrow) {
    const room = this.room!, s = e.seen!;
    if (e.round === room.round) this.applySeats();
    if (e.round !== room.round || room.state !== 'loading' || !room.lockDeadline) return;
    if (s.state === 'locked') {
      this.startCountdown();   // the race is on the locked stakes (applySeats: a seat left out of the lock does not race)
    } else if (s.state === 'open' && e.want === 'locked' && (e.field ?? []).filter(w => s.entries.some(x => x.player === w)).length < MIN_PAID_PLAYERS) {
      await this.backToLobby('Not enough stakes to race: back to the lobby');
    }
  }

  /** Each seat's stake, from the current round's escrow as last read. */
  applySeats() {
    const room = this.room!;
    if (!this.paidRoom()) return;
    const entries = this.escrowFor(room.round)?.seen?.entries ?? [];
    for (const p of Object.values(room.players)) {
      const x = p.wallet ? entries.find(y => y.player === p.wallet && !y.refund) : undefined;
      p.paid = !!x; p.stake = x?.stake;
      if (x) p.payingUntil = undefined;
    }
  }

  /** Every payout of this round, and of any earlier round still paying out, as the results card shows them. */
  publicPayouts() {
    const room = this.room!;
    return room.escrows.filter(e => e.round === room.round || !e.gone).flatMap(e => {
      const s = e.seen;
      if (!s || s.state === 'open') return [];
      return s.entries.filter(x => BigInt(x.payout) > 0n && (x.refund || s.state !== 'locked')).map(x => ({
        wallet: x.player, kind: s.state === 'settled' && !x.refund ? 'prize' : 'refund',
        place: s.state === 'settled' && e.order ? e.order.indexOf(x.player) + 1 || null : null,
        amount: x.payout, currency: room.currency,
        status: x.paid ? 'confirmed' : e.tx?.op === `claim:${x.player}` ? 'sent' : 'pending',
        signature: e.claims[x.player] ?? null, round: e.round,
      }));
    });
  }

  owes(): boolean { return this.room!.escrows.some(e => !e.gone); }

  // ---------- deadlines ----------
  async alarm() {
    await this.load();
    const room = this.room; if (!room) return;
    const t = nowMs();
    // Each step on its own: one failing (a D1 or RPC blip) must not stop the others, and the room always saves and
    // books its next alarm (a throwing alarm is retried only a few times, and money is waiting on it).
    const step = async (name: string, f: () => Promise<unknown>) => { try { await f(); } catch (e: any) { console.error('alarm', name, room.code, e?.message ?? e); } };
    try {
      await step('escrow', async () => {
        const before = JSON.stringify(this.publicPayouts());
        for (const e of room.escrows) await this.chainStep(e);
        room.escrows = room.escrows.filter(e => !e.gone || e.round === room.round);   // a closed earlier round is history
        const payouts = this.publicPayouts();
        if (JSON.stringify(payouts) !== before) this.broadcast({ t: 'payouts', payouts });   // the results card follows each claim
      });
      await step('lock', async () => {
        if (room.state === 'loading' && room.lockDeadline && t >= room.lockDeadline) await this.cancel('The stakes could not be locked in time: every stake is going back');
      });
      await step('lobby', async () => {
        if (room.state === 'lobby' && (t - room.createdAt > LOBBY_TTL_MS || (room.emptySince && t - room.emptySince > EMPTY_TTL_MS))) await this.cancel('Room expired');
      });
      await step('loading', async () => {
        if (room.state !== 'loading' || room.lockDeadline || !room.loadDeadline || t < room.loadDeadline) return;
        const here = Object.values(room.players).filter(x => x.loaded && !x.left);
        if (here.length >= 1) await this.beginCountdown();
        else if (room.standings?.length) await this.abandonCup('Nobody loaded the next track');   // mid-cup: settle what was raced
        else await this.backToLobby('Nobody loaded the track: back to the lobby');               // before the first race: seats and stakes kept
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
        if (room.state === 'results' && room.emptySince && t - room.emptySince > EMPTY_TTL_MS) await this.abandonCup('Everyone left the cup');   // the races run decide it; the pot is paid, not kept
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
    if (room.state === 'loading' && room.lockDeadline) times.push(room.lockDeadline);
    if ((room.state === 'racing' || room.state === 'countdown') && room.finishDeadline) times.push(room.finishDeadline);
    if (room.state === 'results' && room.emptySince) times.push(room.emptySince + EMPTY_TTL_MS);
    if (room.state === 'results' && room.nextAt) times.push(room.nextAt);
    for (const p of Object.values(room.players)) if ((p.payingUntil ?? 0) > t) times.push(p.payingUntil!);   // its end is broadcast
    for (const e of room.escrows) {
      if (e.gone) continue;
      if (e.tx) { times.push(e.tx.at + CHAIN_TX_POLL_MS); continue; }
      if (e.nextTry && e.nextTry > t) { times.push(e.nextTry); continue; }
      if ((!e.seen && (e.created || e.want === 'open' || e.want === 'locked')) || this.nextStep(e)) { times.push(t); continue; }
      times.push(...Object.values(e.stuck ?? {}).map(x => x.until).filter(u => u > t));   // a refund or payout waiting to try again
      if (e.round === room.round && room.state === 'lobby' && e.seen?.state === 'open') {
        const someonePaying = Object.values(room.players).some(p => this.paying(p));
        times.push((e.readAt ?? t) + (someonePaying ? CHAIN_POLL_PAYING_MS : CHAIN_POLL_LOBBY_MS));
      }
    }
    if (!times.length) { await this.ctx.storage.deleteAlarm(); return; }
    // Never later than an alarm already booked: anyone who can read the room (GET /state is public) would otherwise
    // keep pushing the escrow's alarm into the future by asking often enough.
    const next = Math.max(t + 250, Math.min(...times)), cur = await this.ctx.storage.getAlarm();
    if (cur !== null && cur > t && cur <= next) return;
    await this.ctx.storage.setAlarm(next);
  }

  // ---------- what everyone sees ----------
  snapshot() {
    const room = this.room!;
    const e = this.escrowFor(room.round);
    const entries = (e?.seen?.entries ?? []).filter(x => !x.refund);   // the stakes racing (not those left out at the lock)
    const stakes = entries.map(x => BigInt(x.stake));
    const lobbyPaid = room.state === 'lobby' && this.paidRoom();
    return {
      code: room.code, state: room.state, round: room.round, tracks: room.tracks, raceIndex: room.raceIndex,
      minStake: room.minStake, maxStake: room.maxStake, currency: room.currency, cutBps: room.cutBps, isPublic: room.isPublic,
      hostId: room.hostId, startAt: room.startAt ?? null, nextAt: room.nextAt ?? null, locking: !!room.lockDeadline,
      serverNow: nowMs(), pool: this.pool().toString(),
      escrow: e ? { address: e.address, state: e.seen?.state ?? (e.gone ? 'closed' : 'opening'), error: e.failures >= 3 ? e.error ?? null : null } : null,
      players: Object.values(room.players).sort((a, b) => a.joinedAt - b.joinedAt).map(p => {
        const seat = p.wallet ? entries.findIndex(x => x.player === p.wallet) : -1;
        return {
          id: p.id, name: p.name, wallet: p.wallet, character: p.character, kart: p.kart, host: p.host, connected: p.connected,
          ready: p.ready, paid: p.paid, stake: p.stake ?? null, paying: this.paying(p), loaded: p.loaded, racing: p.racing,
          // what this seat takes if it wins, with the stakes in now (fair shares: the same whatever the order behind it)
          ifWin: seat >= 0 && stakes.length >= MIN_PAID_PLAYERS ? ifWin(stakes, seat, room.cutBps).toString() : null,
          // whether this seat may stake now: the phone shows the stake button only then
          canPay: lobbyPaid && this.canStake(p),
          finishMs: p.finishMs ?? null, dnf: p.dnf, place: p.place ?? null, points: p.points,
        };
      }),
      standings: room.standings ?? null, lastResults: room.lastResults ?? null, payouts: this.publicPayouts(),
    };
  }
}

/** The account as stored on the escrow record: amounts as decimal strings. */
function seenOf(c: ChainRoom): Seen {
  return {
    state: c.state, mint: c.mint, expiresAt: c.expiresAt, payer: c.payer, treasury: c.treasury,
    houseCut: c.houseCut.toString(), cutPaid: c.cutPaid,
    entries: c.entries.map(x => ({ player: x.player, stake: x.stake.toString(), payout: x.payout.toString(), paid: x.paid, refund: x.refund })),
  };
}

/** Anything still to hand out from a finished escrow: an unpaid payout or the cut (the program's own `owes`). */
function owedOn(s: Seen): boolean {
  return s.entries.some(x => !x.paid && BigInt(x.payout) > 0n) || (BigInt(s.houseCut) > 0n && !s.cutPaid);
}

export function validStake(stake: string, currency: Currency, tiers: Record<Currency, string[]>) { return tiers[currency]?.includes(stake) ?? false; }

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
