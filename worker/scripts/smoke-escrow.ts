// Paid rooms end to end on a local chain. Every racer is a real keypair: it stakes from its own wallet into the escrow
// program, drives the real circuit through the room's referee, and is paid by the program. Balances are checked to
// the lamport (and to the base unit for SKR). Takes about six minutes.
//   terminal 1: solana-test-validator …        (npm run escrow:localnet -- validator prints the command)
//   terminal 2: npm run escrow:localnet && npx wrangler dev --port 8787 $(cat .escrow-local/wrangler-vars.txt)
//   terminal 3: npm run smoke:escrow
import { createHmac } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Connection, Keypair, LAMPORTS_PER_SOL, PublicKey, Transaction, sendAndConfirmTransaction } from '@solana/web3.js';
import { closeAccount, createAssociatedTokenAccount, getAccount, getOrCreateAssociatedTokenAccount, mintTo } from '@solana/spl-token';
import { joinInstruction, readRoom, roomAddress, tokenAccount } from '../src/escrow';
import { split } from '../src/fairshares';
import { drive, race as driveRace } from './drive.mjs';

const BASE = 'http://127.0.0.1:8787', WS = BASE.replace(/^http/, 'ws'), RPC = 'http://127.0.0.1:8899';
const PROGRAM = new PublicKey('KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv');
const conn = new Connection(RPC, 'confirmed');
const env = { HELIUS_URL: RPC, ESCROW_PROGRAM: PROGRAM.toBase58() } as any;
const local = (name: string) => Keypair.fromSecretKey(Uint8Array.from(JSON.parse(readFileSync(resolve(process.cwd(), '.escrow-local', `${name}.json`), 'utf8'))));
const admin = local('admin'), treasury = local('treasury').publicKey, referee = local('referee').publicKey, SKR = local('skr-mint').publicKey;
const HMAC = Object.fromEntries(readFileSync(resolve(process.cwd(), '.dev.vars'), 'utf8').split('\n').filter(l => l.includes('=')).map(l => l.split('=', 2))).SESSION_HMAC_KEY;
const b64u = (b: Buffer | string) => Buffer.from(b).toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
const session = (w: string) => { const body = b64u(JSON.stringify({ w, e: Math.floor(Date.now() / 1000) + 3600 })); return `${body}.${b64u(createHmac('sha256', HMAC).update(body).digest())}`; };
const SOL = (x: number) => BigInt(Math.round(x * LAMPORTS_PER_SOL));

let failures = 0;
const check = (what: string, ok: boolean, extra = '') => { console.log(`${ok ? 'pass' : 'FAIL'}  ${what}${ok ? '' : '  ' + extra}`); if (!ok) failures++; };
const sleep = (ms: number) => new Promise(r => setTimeout(r, ms));
const post = async (path: string, body: unknown, token?: string, extra: Record<string, string> = {}) => {
  const r = await fetch(BASE + path, { method: 'POST', headers: { 'content-type': 'application/json', ...(token ? { authorization: 'Bearer ' + token } : {}), ...extra }, body: JSON.stringify(body) });
  return { status: r.status, body: await r.json().catch(() => ({})) as any };
};
async function until<T>(what: string, f: () => Promise<T | null | undefined | false>, ms = 60_000): Promise<T> {
  const end = Date.now() + ms;
  for (;;) {
    const v = await f().catch(() => null);
    if (v) return v;
    if (Date.now() > end) throw new Error(`timeout: ${what}`);
    await sleep(500);
  }
}

class Racer {
  ws!: WebSocket; id?: string; inbox: any[] = []; waiters: ((m: any) => boolean)[] = [];
  fees = 0n;   // what this racer's own transactions cost it (read from each, not assumed: a fresh validator charges none)
  readonly key = Keypair.generate();
  readonly token: string;
  constructor(readonly name: string) { this.token = session(this.key.publicKey.toBase58()); }
  get wallet() { return this.key.publicKey.toBase58(); }
  async fund(sol = 5) { await conn.confirmTransaction(await conn.requestAirdrop(this.key.publicKey, sol * LAMPORTS_PER_SOL), 'confirmed'); return this; }
  connect(code: string): Promise<this> {
    return new Promise((res, rej) => {
      this.ws = new WebSocket(`${WS}/v1/rooms/${code}/ws`);
      this.ws.onopen = () => { this.send({ t: 'hello', session: this.token, device: 'dev' + this.name, character: 0, kart: 'oodi' }); res(this); };
      this.ws.onerror = () => rej(new Error(`${this.name}: socket error`));
      this.ws.onmessage = ev => {
        const m = JSON.parse(String(ev.data));
        if (m.t === 'sp') { this.send({ t: 'spr', s: m.s }); return; }
        if (m.t === 'welcome') this.id = m.id;
        this.inbox.push(m); this.waiters = this.waiters.filter(w => !w(m));
      };
    });
  }
  send(o: unknown) { try { this.ws.send(JSON.stringify(o)); } catch { /* closed */ } }
  wait(pred: (m: any) => boolean, ms = 10_000, label = ''): Promise<any> {
    const hit = this.inbox.find(pred);
    if (hit) { this.inbox.splice(this.inbox.indexOf(hit), 1); return Promise.resolve(hit); }
    return new Promise((res, rej) => {
      const timer = setTimeout(() => rej(new Error(`${this.name}: timeout waiting for ${label}; last: ${this.inbox.slice(-4).map(m => m.t + (m.msg ? ':' + m.msg : '')).join(' | ')}`)), ms);
      this.waiters.push(m => { if (!pred(m)) return false; clearTimeout(timer); this.inbox.splice(this.inbox.indexOf(m), 1); res(m); return true; });
    });
  }
  room(pred: (r: any) => boolean, ms = 30_000, label = '') { return this.wait(m => (m.t === 'room' || m.t === 'welcome') && pred(m.room), ms, label).then(m => m.room); }
  me(r: any) { return r.players.find((p: any) => p.wallet === this.wallet); }
  /** Stakes into the room's escrow from this racer's own wallet, as the phone does, and tells the room. */
  async stake(code: string, round: number, amount: bigint, mint: PublicKey | null = null) {
    const sig = await sendAndConfirmTransaction(conn, new Transaction().add(joinInstruction(PROGRAM, code, round, this.key.publicKey, mint, amount)), [this.key], { commitment: 'confirmed' });
    this.fees += BigInt((await conn.getTransaction(sig, { commitment: 'confirmed', maxSupportedTransactionVersion: 0 }))!.meta!.fee);
    this.send({ t: 'pay', signature: sig });
    return sig;
  }
  balance() { return conn.getBalance(this.key.publicKey, 'confirmed').then(BigInt); }
  close() { try { this.ws.close(); } catch { /* gone */ } }
}

/** Everyone loads; in a paid room the countdown waits for the lock. Each racer then drives at its speed (m/s). */
async function race(racers: Racer[], speeds: number[], skipLoad: Racer[] = []) {
  const loads = await Promise.all(racers.map(r => r.wait(m => m.t === 'load', 15_000, 'load')));
  racers.filter(r => !skipLoad.includes(r)).forEach(r => r.send({ t: 'loaded' }));
  const driving = racers.filter(r => !skipLoad.includes(r));
  const cd = await Promise.all(driving.map(r => r.wait(m => m.t === 'countdown', 60_000, 'countdown')));
  await Promise.all(driving.map((r, i) => driveRace((o: unknown) => r.send(o), cd[i].startAt, drive(loads[0].track, speeds[racers.indexOf(r)]))));
  return Promise.all(driving.map(r => r.wait(m => m.t === 'results', 30_000, 'results')));
}

/** The escrow account gone (closed) after every payout: the room is fully paid. */
const closed = (code: string, round: number) => until(`escrow ${code}/${round} closed`, async () => !(await readRoom(env, roomAddress(PROGRAM, code, round).toBase58())), 90_000);

// ---------- 0. the API refuses what it must ----------
{
  const host = new Racer('api');
  const legacy = await post('/v1/rooms', { tracks: ['forest'], fee: '10000000', currency: 'SOL' }, host.token);
  check('an app from before the escrow cannot host a paid room (426)', legacy.status === 426, JSON.stringify(legacy));
  check('a paid room needs a wallet session (401)', (await post('/v1/rooms', { tracks: ['forest'], minStake: '10000000', currency: 'SOL', device: 'x' })).status === 401);
  check('the minimum must be an offered tier (400)', (await post('/v1/rooms', { tracks: ['forest'], minStake: '12345', currency: 'SOL' }, host.token)).status === 400);
  check('paid rooms are refused from a blocked jurisdiction (451)', (await post('/v1/rooms', { tracks: ['forest'], minStake: '10000000', currency: 'SOL' }, host.token, { 'x-kc-geo': 'IN' })).status === 451);
  const cfg = await (await fetch(`${BASE}/v1/config`)).json() as any;
  check('config names the escrow program and the stake tiers', cfg.escrowProgram === PROGRAM.toBase58() && cfg.stakes.SOL.length === 5 && cfg.maxPlayers === 6 && cfg.paidRooms === true);
}

// ---------- 1. a paid SOL cup with three different stakes: fair shares, paid by the program to the lamport ----------
let rematch: { code: string; host: Racer; others: Racer[] } | null = null;
{
  const [a, b, c] = await Promise.all(['A', 'B', 'C'].map(n => new Racer(n).fund()));
  const created = await post('/v1/rooms', { tracks: ['forest'], minStake: '50000000', currency: 'SOL' }, a.token);
  check('create a paid room (stakes from 0.05 SOL)', created.status === 200, JSON.stringify(created));
  const code: string = created.body.code;
  for (const r of [a, b, c]) await r.connect(code);
  const open = await a.room(r => r.escrow?.state === 'open' && r.players.length === 3, 30_000, 'escrow open');
  check('the escrow account is opened on-chain for the room', open.escrow.address === roomAddress(PROGRAM, code, 1).toBase58());
  check('every seat may stake, none has yet', open.players.every((p: any) => p.canPay && !p.paid));

  const stakes = { [a.wallet]: SOL(0.5), [b.wallet]: SOL(0.1), [c.wallet]: SOL(0.05) };
  const before: Record<string, bigint> = {};
  for (const r of [a, b, c]) { before[r.wallet] = await r.balance(); await r.stake(code, 1, stakes[r.wallet]); }
  const staked = await a.room(r => r.players.every((p: any) => p.paid), 30_000, 'all staked');
  check('the room reads each stake from the chain', staked.players.every((p: any) => p.stake === stakes[p.wallet].toString()));
  check('the pot is the sum of the stakes', staked.pool === SOL(0.65).toString(), staked.pool);
  const order = [a, b, c].map(r => r.wallet), stakeList = order.map(w => stakes[w]);
  check("each player's 'if you win' is its fair share as first", staked.players.every((p: any) => {
    const seat = order.indexOf(p.wallet);
    return p.ifWin === split(stakeList, stakeList.map((_, i) => (i === seat ? 0 : i < seat ? i + 1 : i)), 1000).payouts[seat].toString();
  }), JSON.stringify(staked.players.map((p: any) => p.ifWin)));

  a.send({ t: 'start' });
  // B wins, A second, C third: B (0.1) can only win from A what it risked, so A's unmatched 0.4 comes back to A.
  const results = await race([a, b, c], [38, 44, 32]);
  const finish = results[0].results.order.map((o: any) => o.id);
  check('the race is decided by the referee: B, A, C', finish.join() === [b, a, c].map(r => r.wallet).join(), finish.join());
  const rank = order.map(w => finish.indexOf(w));
  const want = split(stakeList, rank, 1000);
  await closed(code, 1);
  for (const [i, r] of [a, b, c].entries()) {
    const got = await r.balance();
    check(`${r.name}: balance = before − stake − fee + fair share (${Number(want.payouts[i]) / 1e9} SOL)`, got === before[r.wallet] - stakeList[i] - r.fees + want.payouts[i], `${got} vs ${before[r.wallet] - stakeList[i] - r.fees + want.payouts[i]}`);
  }
  check('A, beaten, still gets its unmatched 0.4 SOL back', want.payouts[0] > SOL(0.4));
  const paidOut = await a.room(r => r.payouts.length > 0 && r.payouts.every((p: any) => p.status === 'confirmed'), 30_000, 'payouts confirmed');
  check('the results card shows each payout with its transaction', paidOut.payouts.every((p: any) => p.kind === 'prize' && /^[1-9A-HJ-NP-Za-km-z]{64,90}$/.test(p.signature)));
  rematch = { code, host: a, others: [b, c] };
  console.log(`   (house cut ${Number(want.house) / 1e9} SOL)`);
}

// ---------- 2. the rematch gets a new escrow; the old one is closed ----------
{
  const { code, host } = rematch!;
  host.send({ t: 'again' });
  const r2 = await host.room(r => r.round === 2 && r.escrow?.state === 'open', 30_000, 'round 2 escrow');
  check('a rematch opens a new escrow for round 2', r2.escrow.address === roomAddress(PROGRAM, code, 2).toBase58() && r2.players.every((p: any) => !p.paid));
  for (const r of [host, ...rematch!.others]) r.close();
}

// ---------- 3. stakes go back: a player leaving the lobby, and a stranger who stakes without a seat ----------
{
  const [h, d, stranger] = await Promise.all(['H', 'D', 'X'].map(n => new Racer(n).fund()));
  const code: string = (await post('/v1/rooms', { tracks: ['forest'], minStake: '10000000', currency: 'SOL' }, h.token)).body.code;
  await h.connect(code); await d.connect(code);
  await h.room(r => r.escrow?.state === 'open' && r.players.length === 2, 30_000, 'escrow open');
  const dBefore = await d.balance(), xBefore = await stranger.balance();
  await d.stake(code, 1, SOL(0.15));
  await h.room(r => r.players.some((p: any) => p.wallet === d.wallet && p.paid), 30_000, 'D staked');
  d.send({ t: 'leave' }); d.close();
  await stranger.stake(code, 1, SOL(0.05));
  await until('both stakes handed back', async () => {
    const room = await readRoom(env, roomAddress(PROGRAM, code, 1).toBase58());
    return room && room.entries.length === 0;
  });
  check('a player who leaves the lobby gets the whole stake back (less its own fee)', await d.balance() === dBefore - d.fees);
  check('a stranger who stakes without a seat gets it back too', await stranger.balance() === xBefore - stranger.fees);
  h.close();
}

// ---------- 4. a staked seat that never reaches the grid goes back; the other two race for their own stakes ----------
{
  const [f, g, n] = await Promise.all(['F', 'G', 'N'].map(x => new Racer(x).fund()));
  const code: string = (await post('/v1/rooms', { tracks: ['forest'], minStake: '10000000', currency: 'SOL' }, f.token)).body.code;
  for (const r of [f, g, n]) await r.connect(code);
  await f.room(r => r.escrow?.state === 'open' && r.players.length === 3, 30_000, 'escrow open');
  const before: Record<string, bigint> = {};
  for (const r of [f, g, n]) { before[r.wallet] = await r.balance(); await r.stake(code, 1, SOL(0.1)); }
  await f.room(r => r.players.every((p: any) => p.paid), 30_000, 'all staked');
  f.send({ t: 'start' });
  const nClosed = n.wait(m => m.t === 'closed', 60_000, 'N removed');
  const results = await race([f, g, n], [40, 36, 30], [n]);    // N's phone never finishes loading
  check('the seat that missed the grid is told its stake is going back', /stake/i.test((await nClosed).reason));
  check('the two who loaded raced', results[0].results.order.length === 2);
  await closed(code, 1);
  check('N gets its whole stake back (less its own fee)', await n.balance() === before[n.wallet] - n.fees);
  const win = split([SOL(0.1), SOL(0.1)], [0, 1], 1000);
  check('F wins G\'s stake less the cut', await f.balance() === before[f.wallet] - SOL(0.1) - f.fees + win.payouts[0]);
  check('G loses its stake', await g.balance() === before[g.wallet] - SOL(0.1) - g.fees);
  for (const r of [f, g, n]) r.close();
}

// ---------- 5. an SKR room: tokens in the room's vault, paid out to each racer's token account ----------
{
  const [p, q] = await Promise.all(['P', 'Q'].map(x => new Racer(x).fund()));
  for (const r of [p, q]) {
    const acc = await getOrCreateAssociatedTokenAccount(conn, admin, SKR, r.key.publicKey, false, 'confirmed');
    await mintTo(conn, admin, SKR, acc.address, admin, 1_000_000_000, [], { commitment: 'confirmed' });
  }
  const treasuryBefore = (await getAccount(conn, tokenAccount(treasury, SKR), 'confirmed')).amount;
  const code: string = (await post('/v1/rooms', { tracks: ['forest'], minStake: '5000000', currency: 'SKR' }, p.token)).body.code;
  for (const r of [p, q]) await r.connect(code);
  await p.room(r => r.escrow?.state === 'open' && r.players.length === 2, 30_000, 'escrow open');
  await p.stake(code, 1, 100_000_000n, SKR);
  await q.stake(code, 1, 25_000_000n, SKR);
  await p.room(r => r.players.every((x: any) => x.paid), 30_000, 'both staked');
  p.send({ t: 'start' });
  await race([p, q], [32, 42]);   // Q wins: it can only win P's matching 25 SKR; P's other 75 come back
  await closed(code, 1);
  const tokens = async (r: Racer) => (await getAccount(conn, tokenAccount(r.key.publicKey, SKR), 'confirmed')).amount;
  const want = split([100_000_000n, 25_000_000n], [1, 0], 1000);
  check(`P: 1000 − 100 + ${Number(want.payouts[0]) / 1e6} SKR`, await tokens(p) === 1_000_000_000n - 100_000_000n + want.payouts[0]);
  check(`Q: 1000 − 25 + ${Number(want.payouts[1]) / 1e6} SKR`, await tokens(q) === 1_000_000_000n - 25_000_000n + want.payouts[1]);
  check('the treasury receives the cut in SKR', (await getAccount(conn, tokenAccount(treasury, SKR), 'confirmed')).amount === treasuryBefore + want.house);
  check("the room's vault is closed", !(await conn.getAccountInfo(tokenAccount(roomAddress(PROGRAM, code, 1), SKR))));
  for (const r of [p, q]) r.close();
}

// ---------- 5b. a payout that cannot land holds up nobody: the winner closed its emptied token account ----------
{
  const [a, b, c] = await Promise.all(['S1', 'S2', 'S3'].map(x => new Racer(x).fund()));
  const stake = 25_000_000n;
  for (const [r, amount] of [[a, stake], [b, 1_000_000_000n], [c, 1_000_000_000n]] as const) {
    const acc = await getOrCreateAssociatedTokenAccount(conn, admin, SKR, r.key.publicKey, false, 'confirmed');
    await mintTo(conn, admin, SKR, acc.address, admin, amount, [], { commitment: 'confirmed' });
  }
  const code: string = (await post('/v1/rooms', { tracks: ['forest'], minStake: '5000000', currency: 'SKR' }, a.token)).body.code;
  for (const r of [a, b, c]) await r.connect(code);
  await a.room(r => r.escrow?.state === 'open' && r.players.length === 3, 30_000, 'escrow open');
  let offUnit = false;
  try { await c.stake(code, 1, stake + 1n, SKR); } catch { offUnit = true; }
  check('a stake that is not a whole number of units (5 SKR) is refused on-chain', offUnit);
  for (const r of [a, b, c]) await r.stake(code, 1, stake, SKR);
  // A staked its whole balance, then tidied its wallet: the empty token account is closed.
  await closeAccount(conn, a.key, tokenAccount(a.key.publicKey, SKR), a.key.publicKey, a.key, [], { commitment: 'confirmed' });
  await a.room(r => r.players.every((p: any) => p.paid), 30_000, 'all staked');
  a.send({ t: 'start' });
  await race([a, b, c], [44, 38, 32]);   // A wins, and has nowhere to be paid
  const want = split([stake, stake, stake], [0, 1, 2], 1000);
  const tokens = async (r: Racer) => (await getAccount(conn, tokenAccount(r.key.publicKey, SKR), 'confirmed')).amount;
  await until('the other payouts and the cut, past the stuck one', async () => {
    if (await tokens(b) !== 1_000_000_000n - stake + want.payouts[1] || await tokens(c) !== 1_000_000_000n - stake + want.payouts[2]) return false;
    const room = await readRoom(env, roomAddress(PROGRAM, code, 1).toBase58());
    return room && room.cutPaid && !room.entries[0].paid;
  }, 90_000);
  check('everyone else is paid and the cut collected while A cannot be', true);
  check('the room stays open for A', !!(await readRoom(env, roomAddress(PROGRAM, code, 1).toBase58())));
  await createAssociatedTokenAccount(conn, a.key, SKR, a.key.publicKey, { commitment: 'confirmed' });   // A has an account again
  await closed(code, 1);
  check('once A can receive it, A is paid and the room closes', await tokens(a) === want.payouts[0]);
  for (const r of [a, b, c]) r.close();
}

// ---------- 6. six seats, never seven ----------
{
  const code: string = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devG0' })).body.code;
  const guests = Array.from({ length: 7 }, (_, i) => new WebSocket(`${WS}/v1/rooms/${code}/ws`));
  const replies = await Promise.all(guests.map((ws, i) => new Promise<any>(res => {
    ws.onopen = () => setTimeout(() => ws.send(JSON.stringify({ t: 'hello', device: `devSeat${i}` })), i * 150);
    ws.onmessage = ev => { const m = JSON.parse(String(ev.data)); if (m.t === 'welcome' || m.t === 'error') res(m); };
  })));
  check('six racers are seated', replies.filter(m => m.t === 'welcome').length === 6);
  check('the seventh is told the room is full', replies.some(m => m.t === 'error' && /full/i.test(m.msg)));
  guests.forEach(ws => ws.close());
}

const refereeLeft = await conn.getBalance(referee, 'confirmed');
console.log(`   (referee holds ${refereeLeft / 1e9} SOL after every room: rent comes back when rooms close)`);
if (failures) { console.log(`${failures} FAILURE(S)`); process.exit(1); }
console.log('smoke:escrow ALL PASS');
process.exit(0);
