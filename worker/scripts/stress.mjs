// The Worker under stress (12 Oct 2026, the user: "run a proper stress test ... every purchase, every in-game currency
// ... stress it as much as you can"). It serves a fake Solana RPC and starts `wrangler dev` wired to it with the real
// payment checks on (MOCK_CHAIN off), so every shop item is bought in every currency through the same verification the
// live Worker runs, against transactions crafted here: honest, short, wrong mint, wrong treasury, wrong memo, not
// signed, failed on-chain, someone else's, replayed, not landed yet, the RPC down. Then load: 150 races finishing at
// once (with forged and doubled finishes), a read burst, garbage at every route, and 25 rooms racing at the same time.
// Nothing here touches the live Worker or the chain.        node scripts/stress.mjs [--keep]   (from worker/)
import { spawn } from 'node:child_process';
import http from 'node:http';
import { randomBytes } from 'node:crypto';
import bs58 from 'bs58';
import { Keypair, PublicKey } from '@solana/web3.js';
import { getAssociatedTokenAddressSync } from '@solana/spl-token';
import { ed25519 } from '@noble/curves/ed25519';
import { drive, race as driveRace } from './drive.mjs';

const PORT = 8788, RPC_PORT = 8899, BASE = `http://127.0.0.1:${PORT}`, WS = BASE.replace(/^http/, 'ws');
const SKR = 'SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3', ORE = 'oreoU2P8bN6jkk3jbaiVxYnG1dCXcYxwhwyK9jSybcp';
const TREASURY = Keypair.generate().publicKey.toBase58();
const ata = (mint, owner) => getAssociatedTokenAddressSync(new PublicKey(mint), new PublicKey(owner), true).toBase58();
const TATA = { SKR: ata(SKR, TREASURY), ORE: ata(ORE, TREASURY) };

let failures = 0; const notes = [];
const check = (what, ok, extra = '') => { console.log(`${ok ? 'pass' : 'FAIL'}  ${what}${ok ? '' : '  ' + extra}`); if (!ok) failures++; };
const sleep = ms => new Promise(r => setTimeout(r, ms));
const ip = () => `10.${(Math.random() * 255) | 0}.${(Math.random() * 255) | 0}.${(Math.random() * 255) | 0}`;   // a different player each call
async function call(method, path, body, token, headers = {}) {
  const t0 = performance.now();
  try {
    const r = await fetch(BASE + path, { method, headers: { 'content-type': 'application/json', 'cf-connecting-ip': ip(), ...(token ? { authorization: 'Bearer ' + token } : {}), ...headers },
                                         body: body === undefined ? undefined : typeof body === 'string' ? body : JSON.stringify(body) });
    const text = await r.text(); let json = {}; try { json = JSON.parse(text); } catch {}
    return { status: r.status, body: json, ms: performance.now() - t0, text };
  } catch (e) { return { status: 0, body: {}, ms: performance.now() - t0, text: String(e) }; }
}
const post = (p, b, t, h) => call('POST', p, b, t, h), get = (p, t) => call('GET', p, undefined, t);

// ---------- the fake RPC: getTransaction answers from this map (a tx, null = not landed, 'down' = the RPC errors) ----------
const txs = new Map();
const rpc = http.createServer((req, res) => {
  let data = ''; req.on('data', c => data += c); req.on('end', () => {
    let q = {}; try { q = JSON.parse(data); } catch {}
    const reply = o => { res.writeHead(200, { 'content-type': 'application/json' }); res.end(JSON.stringify({ jsonrpc: '2.0', id: q.id, ...o })); };
    if (q.method === 'getTransaction') {
      const v = txs.get(q.params?.[0]);
      if (v === 'down') { res.writeHead(503); res.end('down'); return; }
      return reply({ result: v ?? null });
    }
    reply({ result: null });          // getAsset, name lookups...: nothing known
  });
}).listen(RPC_PORT);

// ---------- the Worker, real payment checks, on the fake RPC ----------
const vars = { MOCK_CHAIN: '0', HELIUS_URL: `http://127.0.0.1:${RPC_PORT}`, TREASURY_OWNER: TREASURY, RATES_JSON: '{"SKR":0.0176,"SOL":112,"ORE":119}' };
const wrangler = spawn('npx', ['wrangler', 'dev', '--port', String(PORT), ...Object.entries(vars).flatMap(([k, v]) => ['--var', `${k}:${v}`])], { stdio: ['ignore', 'pipe', 'pipe'] });
let wlog = ''; wrangler.stdout.on('data', d => wlog += d); wrangler.stderr.on('data', d => wlog += d);
const stop = () => { try { wrangler.kill('SIGTERM'); } catch {} rpc.close(); };
process.on('exit', stop);
for (let i = 0; i < 90; i++) { if ((await get('/v1/config')).status === 200) break; await sleep(1000); }
const cfg = (await get('/v1/config')).body;
check('the local Worker is up on the fake RPC with real payment checks', cfg.treasuryOwner === TREASURY && Array.isArray(cfg.catalog), wlog.slice(-500));

// ---------- wallets ----------
async function signIn() {
  const kp = Keypair.generate(), address = kp.publicKey.toBase58();
  const ch = await post('/v1/auth/challenge', {});
  const msg = new TextEncoder().encode(`${ch.body.domain} wants you to sign in with your Solana account:\n${address}\n\nSign in to Kartel\n\nNonce: ${ch.body.nonce}`);
  const sig = ed25519.sign(msg, kp.secretKey.slice(0, 32));
  const v = await post('/v1/auth/verify', { nonce: ch.body.nonce, address, challengeSig: Buffer.concat([Buffer.from(msg), Buffer.from(sig)]).toString('base64') });
  return { address, token: v.body.token, status: v.status };
}
const sigOf = () => bs58.encode(randomBytes(64));

/** A landed transaction paying `amount` base units of `cur` for `memo`. `bad` bends one thing. */
function tx(wallet, cur, amount, memo, bad = {}) {
  const ix = [{ program: 'spl-memo', parsed: bad.memo ?? memo }];
  if (cur === 'SOL') ix.push({ program: 'system', parsed: { type: 'transfer', info: { source: bad.source ?? wallet, destination: bad.dest ?? TREASURY, lamports: Number(amount) } } });
  else {
    const mint = bad.mint ?? (cur === 'ORE' ? ORE : SKR);
    ix.push({ program: 'spl-token', parsed: { type: 'transferChecked', info: { mint, source: bad.source ?? ata(cur === 'ORE' ? ORE : SKR, wallet), destination: bad.dest ?? TATA[cur],
      authority: bad.authority ?? wallet, tokenAmount: { amount: String(amount) } } } });
  }
  return { slot: 100 + (Math.random() * 1e6 | 0), meta: { err: bad.failed ? { InstructionError: [1, 'Custom'] } : null, innerInstructions: [] },
    transaction: { message: { accountKeys: [{ pubkey: bad.signer ?? wallet, signer: true, writable: true }, { pubkey: TREASURY, signer: false, writable: true }], instructions: ix } } };
}
async function buy(w, sku, cur, amount, bad = {}, sigOverride = null, orderId = 'o' + randomBytes(5).toString('hex')) {
  const sig = sigOverride ?? sigOf();
  if (!sigOverride || !txs.has(sig)) txs.set(sig, 'state' in bad ? bad.state : tx(w.address, cur, amount, `kc:buy:${sku}:${orderId}`, bad));
  return { sig, orderId, ...(await post('/v1/purchase/verify', { signature: sig, sku, orderId, currency: cur }, w.token)) };
}

// ================= 1. every purchase, every currency =================
console.log('\n== 1. the shop: every item, every currency, honest and not');
{
  const wallets = await Promise.all(Array.from({ length: 6 }, signIn));
  check('six wallets sign in (SIWS, Ed25519)', wallets.every(w => w.status === 200 && w.token), wallets.map(w => w.status).join(','));
  let bought = 0, quoted = 0, k = 0;
  for (const item of cfg.catalog) {
    for (const cur of ['SOL', 'SKR', 'ORE']) {
      const p = item.price?.[cur];
      if (!p) { if (cur === 'SOL' && item.skrOnly) { const w = wallets[k++ % 6]; const r = await buy(w, item.sku, 'SOL', 30000000n); check(`${item.sku} is never sold for SOL`, r.status === 400, r.status + ' ' + r.text); } continue; }
      quoted++;
      const w = wallets[k++ % 6], price = BigInt(p);
      const ok = await buy(w, item.sku, cur, price);
      const want = [item.sku, ...(item.grants ?? [])].sort().join(',');
      const got = (ok.body.granted ?? []).slice().sort().join(',');
      check(`${item.sku} for ${cur} (${p}) is granted exactly what it sells`, ok.status === 200 && got === want, `${ok.status} ${ok.text.slice(0, 200)}`);
      if (ok.status === 200) bought++;
      const floor = await buy(w, item.sku, cur, (price * 9n) / 10n);
      check(`${item.sku} for ${cur}: 90% of the quote is still taken (a price that moved)`, floor.status === 200, floor.status + ' ' + floor.text.slice(0, 120));
      const short = await buy(w, item.sku, cur, (price * 9n) / 10n - 1n);
      check(`${item.sku} for ${cur}: one unit under 90% is refused`, short.status === 400, short.status + ' ' + short.text.slice(0, 120));
    }
  }
  console.log(`   ${bought} of ${quoted} quoted prices bought once each across ${cfg.catalog.length} items`);

  // the dishonest payments, on a kart, a gem pack and a bundle, in each currency
  const w = wallets[0], other = wallets[1];
  for (const sku of ['kart.firetruck', 'gems.550', 'bundle.founder', 'driver.beeman']) {
    const item = cfg.catalog.find(i => i.sku === sku); if (!item) { check(`${sku} is in the catalog`, false); continue; }
    for (const cur of ['SOL', 'SKR', 'ORE']) {
      const price = item.price?.[cur] ? BigInt(item.price[cur]) : null; if (price === null) continue;
      const cases = [
        ['to the wrong account', { dest: Keypair.generate().publicKey.toBase58() }],
        ['with another memo', { memo: `kc:buy:paint.white:x` }],
        ['signed by someone else', { signer: other.address }],
        ['that failed on-chain', { failed: true }],
        ['from another wallet', { source: cur === 'SOL' ? other.address : ata(cur === 'ORE' ? ORE : SKR, other.address) }],
        ...(cur === 'SOL' ? [] : [['in the wrong token', { mint: cur === 'SKR' ? ORE : SKR }], ['moved by another authority', { authority: other.address }]]),
      ];
      for (const [why, bad] of cases) { const r = await buy(w, sku, cur, price, bad); check(`${sku} ${cur}: a payment ${why} is refused`, r.status === 400, r.status + ' ' + r.text.slice(0, 120)); }
      const pending = await buy(w, sku, cur, price, { state: null });
      check(`${sku} ${cur}: not landed yet answers 202 pending, no grant`, pending.status === 202 && pending.body.pending === true, pending.status + ' ' + pending.text);
      txs.set(pending.sig, tx(w.address, cur, price, `kc:buy:${sku}:${pending.orderId}`));
      const later = await post('/v1/purchase/verify', { signature: pending.sig, sku, orderId: pending.orderId, currency: cur }, w.token);
      check(`${sku} ${cur}: ...and is granted once it lands`, later.status === 200, later.status + ' ' + later.text.slice(0, 120));
      const down = await buy(w, sku, cur, price, { state: 'down' });
      check(`${sku} ${cur}: the RPC down answers 202 (retry later), never a refusal`, down.status === 202, down.status + ' ' + down.text.slice(0, 120));
    }
  }

  // replays: the same signature again, by its payer, by another wallet, concurrently
  const item = cfg.catalog.find(i => i.sku === 'gems.1200'), price = BigInt(item.price.SOL);
  const first = await buy(w, 'gems.1200', 'SOL', price);
  check('a gem pack bought', first.status === 200);
  const again = await post('/v1/purchase/verify', { signature: first.sig, sku: 'gems.1200', orderId: first.orderId, currency: 'SOL' }, w.token);
  check('the same payment asked about again by its payer is the same purchase (idempotent, nothing new)', again.status === 200 && (again.body.granted ?? []).join() === 'gems.1200', again.text);
  const stolen = await post('/v1/purchase/verify', { signature: first.sig, sku: 'gems.1200', orderId: first.orderId, currency: 'SOL' }, other.token);
  check("someone else's payment cannot be redeemed by another wallet", stolen.status === 400, stolen.status + ' ' + stolen.text);
  const reuse = await post('/v1/purchase/verify', { signature: first.sig, sku: 'driver.toly', orderId: first.orderId, currency: 'SOL' }, w.token);
  check('one payment cannot buy a second item (memo names the first)', reuse.status === 400, reuse.status + ' ' + reuse.text);
  const race = sigOf(); txs.set(race, tx(w.address, 'SOL', price, 'kc:buy:gems.1200:R1'));
  const storm = await Promise.all([...Array(10)].map((_, i) => post('/v1/purchase/verify', { signature: race, sku: 'gems.1200', orderId: 'R1', currency: 'SOL' }, i % 2 ? other.token : w.token)));
  const mine = storm.filter((_, i) => i % 2 === 0), theirs = storm.filter((_, i) => i % 2 === 1);
  check('10 concurrent claims of one payment: its payer is granted, the other wallet never is', mine.every(r => r.status === 200) && theirs.every(r => r.status === 400), storm.map(r => r.status).join(','));
  check('no claim of a payment ever answers 5xx', storm.every(r => r.status < 500));
  for (const [what, body, tok] of [['no session', { signature: sigOf(), sku: 'gems.100', orderId: 'x', currency: 'SOL' }, null],
                                    ['an unknown sku', { signature: sigOf(), sku: 'kart.nope', orderId: 'x', currency: 'SOL' }, w.token],
                                    ['no order id', { signature: sigOf(), sku: 'gems.100', currency: 'SOL' }, w.token],
                                    ['a 200-character sku', { signature: sigOf(), sku: 'x'.repeat(200), orderId: 'x', currency: 'SOL' }, w.token],
                                    ['not a signature', { signature: 'hello', sku: 'gems.100', orderId: 'x', currency: 'SOL' }, w.token],
                                    ['a forged session', { signature: sigOf(), sku: 'gems.100', orderId: 'x', currency: 'SOL' }, 'eyJ3IjoieCJ9.Zm9yZ2Vk']]) {
    const r = await post('/v1/purchase/verify', body, tok);
    check(`purchase with ${what} is refused (4xx)`, r.status >= 400 && r.status < 500, r.status + ' ' + r.text.slice(0, 100));
  }
  const me = await get('/v1/me', w.token);
  check('the profile lists what was bought', me.status === 200 && me.body.unlocks.includes('gems.1200') && me.body.unlocks.includes('kart.firetruck'), me.text.slice(0, 200));
  const del = await call('DELETE', '/v1/me', undefined, w.token);
  const replayAfterDelete = await post('/v1/purchase/verify', { signature: first.sig, sku: 'gems.1200', orderId: first.orderId, currency: 'SOL' }, (await signIn()).token);
  check('deleting an account never frees its payments to be redeemed again', del.status === 200 && replayAfterDelete.status === 400, `${del.status} ${replayAfterDelete.status}`);
}

// ================= 2. races: 150 at once, forged and doubled finishes =================
console.log('\n== 2. races under load');
const TRACKS = ['beach', 'alpine', 'forest', 'speedway', 'volcano'];
const starts = [];
{
  const t0 = performance.now();
  const reqs = []; for (let i = 0; i < 150; i++) { const track = TRACKS[i % 5]; reqs.push(post('/v1/race/start', { deviceId: 'stress' + String(i).padStart(4, '0'), trackId: track }).then(r => ({ r, track, i }))); }
  for (const x of await Promise.all(reqs)) starts.push(x);
  const ok = starts.filter(x => x.r.status === 200);
  check(`150 races started at once (${(performance.now() - t0).toFixed(0)} ms)`, ok.length === 150, starts.filter(x => x.r.status !== 200).map(x => x.r.status).slice(0, 10).join(','));
}
const startedAt = Date.now();

// the Weekly: twelve signed-in wallets race this week's circuit at different speeds, one on the wrong circuit, and a
// guest whose run is claimed after it finishes
const wk = (await get('/v1/weekly')).body, wTrack = wk.track, other = TRACKS.find(t => t !== wTrack);
const weeklyRacers = await Promise.all(Array.from({ length: 12 }, signIn));
const weeklyRuns = await Promise.all(weeklyRacers.map(async (w, i) => ({ w, v: 36 + i * 0.7, r: await post('/v1/race/start', { deviceId: 'weekly' + i, trackId: wTrack, mode: 'weekly' }, w.token) })));
const offTrack = { w: weeklyRacers[0], r: await post('/v1/race/start', { deviceId: 'weeklyX', trackId: other, mode: 'weekly' }, weeklyRacers[0].token) };
const guestRun = await post('/v1/race/start', { deviceId: 'weeklyG', trackId: wTrack, mode: 'weekly' });
check(`the Weekly is on a known circuit (${wTrack}) and its races start`, TRACKS.includes(wTrack) && weeklyRuns.every(x => x.r.status === 200) && offTrack.r.status === 200 && guestRun.status === 200);

// ================= 3. reads under load, while the races run =================
console.log('\n== 3. reads under load');
{
  const paths = ['/v1/config', '/v1/weekly', '/v1/rooms/public', ...TRACKS.map(t => `/v1/leaderboard?track=${t}`)];
  const lat = [], codes = {}; let i = 0;
  const worker = async () => { while (i < 3000) { const p = paths[i++ % paths.length]; const r = await get(p); lat.push(r.ms); codes[r.status] = (codes[r.status] ?? 0) + 1; } };
  const t0 = performance.now(); await Promise.all([...Array(60)].map(worker)); const secs = (performance.now() - t0) / 1000;
  lat.sort((a, b) => a - b); const q = f => lat[Math.min(lat.length - 1, Math.floor(f * lat.length))].toFixed(0);
  console.log(`   3000 GETs, 60 at a time: ${(3000 / secs).toFixed(0)}/s, p50 ${q(0.5)} ms, p95 ${q(0.95)} ms, max ${q(1)} ms, codes ${JSON.stringify(codes)}`);
  check('3000 concurrent reads: no 5xx, no dropped connection', !Object.keys(codes).some(c => c === '0' || +c >= 500), JSON.stringify(codes));
}

// ================= 4. garbage at every route =================
console.log('\n== 4. garbage at every route');
{
  const routes = ['/v1/auth/challenge', '/v1/auth/verify', '/v1/race/start', '/v1/race/finish', '/v1/race/claim', '/v1/purchase/verify', '/v1/tx/confirm', '/v1/event', '/v1/rooms', '/v1/demo/unlock'];
  const junk = [undefined, '', 'null', '[]', '{', '"x"', '12', '{"a":', JSON.stringify({ deviceId: 'x'.repeat(5000) }), JSON.stringify({ trackId: { $ne: 1 } }),
    JSON.stringify({ deviceId: "'; DROP TABLE results;--", trackId: 'beach' }), JSON.stringify({ deviceId: 'dev😀😀😀', trackId: '__proto__' }), JSON.stringify({ raceToken: 'a.b', laps: [1e309], path: 'x' }),
    JSON.stringify({ raceToken: null, totalMs: -1, lapMs: [NaN], path: [[0, 'a', null, {}]] }), JSON.stringify({ tracks: ['beach'.repeat(100)], minStake: '-1', currency: 'BTC', device: 1 }),
    JSON.stringify({ nonce: 'x', signInResult: { address: 1, signed_message: '!!', signature: '%%' } }), JSON.stringify({ name: 'x', props: { a: 'b'.repeat(20000) } }),
    JSON.stringify(Array(2000).fill({ a: 1 })), 'x'.repeat(200000)];
  const codes = {}, fives = [];
  await Promise.all(routes.flatMap(r => junk.map(async body => {
    for (const tok of [null, 'garbage', 'Bearer.x.y']) {
      const res = await post(r, body, tok);
      codes[res.status] = (codes[res.status] ?? 0) + 1;
      if (res.status >= 500 || res.status === 0) fives.push(`${r} ${res.status} ${String(body).slice(0, 40)} → ${res.text.slice(0, 80)}`);
    }
  })));
  // and signed in, so the routes that check the session first get as far as reading the junk
  const w = await signIn();
  await Promise.all(['/v1/purchase/verify', '/v1/tx/confirm', '/v1/race/claim', '/v1/demo/unlock', '/v1/race/finish', '/v1/rooms'].flatMap(r => junk.map(async body => {
    const res = await post(r, body, w.token);
    codes[res.status] = (codes[res.status] ?? 0) + 1;
    if (res.status >= 500 || res.status === 0) fives.push(`${r} (signed in) ${res.status} ${String(body).slice(0, 40)} → ${res.text.slice(0, 80)}`);
  })));
  console.log(`   ${routes.length * junk.length * 3 + 6 * junk.length} requests: ${JSON.stringify(codes)}`);
  check('garbage never makes a route answer 5xx', fives.length === 0, '\n      ' + fives.slice(0, 12).join('\n      '));
  for (const p of ['/v1/rooms/ZZZZZ', '/v1/rooms/' + 'A'.repeat(500), '/v1/leaderboard?track=' + encodeURIComponent("beach' OR 1=1"), '/v1/weekly?wallet=' + 'x'.repeat(3000), '/r/' + '%00'.repeat(50), '/nope']) {
    const r = await get(p); check(`GET ${p.slice(0, 50)} answers without a 5xx`, r.status > 0 && r.status < 500, r.status + ' ' + r.text.slice(0, 80));
  }
}

// ================= 5. 25 rooms racing at once =================
console.log('\n== 5. 25 rooms, 4 racers each, at once');
class Client {
  constructor(name) { this.name = name; this.inbox = []; this.waiters = []; }
  connect(code) { return new Promise((res, rej) => { this.ws = new WebSocket(`${WS}/v1/rooms/${code}/ws`);
    this.ws.onopen = () => res(this); this.ws.onerror = e => rej(new Error('ws ' + (e.message ?? '')));
    this.ws.onmessage = ev => { const m = JSON.parse(ev.data); if (m.t === 'sp') { this.send({ t: 'spr', s: m.s }); return; } if (m.t === 'welcome') this.id = m.id; this.inbox.push(m); this.waiters = this.waiters.filter(w => !w(m)); }; }); }
  send(o) { try { this.ws.send(JSON.stringify(o)); } catch {} }
  wait(pred, ms, label) { const hit = this.inbox.find(pred); if (hit) { this.inbox.splice(this.inbox.indexOf(hit), 1); return Promise.resolve(hit); }
    return new Promise((res, rej) => { const timer = setTimeout(() => rej(new Error(`${this.name}: timeout ${label}`)), ms);
      this.waiters.push(m => { if (!pred(m)) return false; clearTimeout(timer); this.inbox.splice(this.inbox.indexOf(m), 1); res(m); return true; }); }); }
  close() { try { this.ws.close(); } catch {} }
}
const roomRun = async n => {
  const track = TRACKS[n % 5], host = 'room' + n + 'h' + randomBytes(3).toString('hex');
  const made = await post('/v1/rooms', { tracks: [track], minStake: '0', currency: 'SOL', device: host });
  if (made.status !== 200) return { n, ok: false, why: 'create ' + made.status + ' ' + made.text.slice(0, 80) };
  const cs = []; try {
    for (let k = 0; k < 4; k++) { const c = await new Client(`r${n}.${k}`).connect(made.body.code); c.send({ t: 'hello', device: k ? `room${n}g${k}${randomBytes(3).toString('hex')}` : host, character: k }); await c.wait(m => m.t === 'welcome', 8000, 'welcome'); cs.push(c); }
    cs[0].send({ t: 'start' });
    const loads = await Promise.all(cs.map(c => c.wait(m => m.t === 'load', 15000, 'load')));
    cs.forEach(c => c.send({ t: 'loaded' }));
    const cd = await Promise.all(cs.map(c => c.wait(m => m.t === 'countdown', 15000, 'countdown')));
    await Promise.all(cs.map((c, k) => driveRace(o => c.send(o), cd[k].startAt, drive(loads[k].track, 40 + k * 2))));
    const res = await Promise.all(cs.map(c => c.wait(m => m.t === 'results', 30000, 'results')));
    const dnfs = res[0].results.order.filter(o => o.dnf).length;
    return { n, ok: dnfs === 0, why: dnfs ? `${dnfs} honest DNF` : '' };
  } catch (e) { return { n, ok: false, why: String(e.message ?? e) }; } finally { cs.forEach(c => c.close()); }
};
{
  const t0 = performance.now();
  const rooms = await Promise.all([...Array(25)].map((_, n) => roomRun(n)));
  const bad = rooms.filter(r => !r.ok);
  check(`25 rooms × 4 racers (100 sockets) all race to results with no honest DNF (${((performance.now() - t0) / 1000).toFixed(0)} s)`, bad.length === 0, bad.slice(0, 6).map(r => `#${r.n}: ${r.why}`).join(' | '));
}

// ================= 2b. the 150 races finish, honest, forged and doubled =================
console.log('\n== 2b. 150 finishes at once');
{
  const drives = starts.map(x => drive(x.track, 40));
  const wait = Math.max(...drives.map(d => d.finishT)) * 1000 + 2500 - (Date.now() - startedAt);
  if (wait > 0) { console.log(`   waiting ${(wait / 1000).toFixed(0)} s of real race time`); await sleep(wait); }
  const signedIn = await Promise.all(Array.from({ length: 10 }, signIn));
  const body = (x, d, extra = {}) => ({ raceToken: x.r.body.raceToken, totalMs: d.ms, lapMs: d.soloLaps, checkpointMs: d.cps, path: d.path(), position: 1, ...extra });
  const out = await Promise.all(starts.map(async (x, i) => {
    const d = drives[i];
    if (i % 15 === 1) { const fast = drive(x.track, 90); return { kind: 'forged', r: await post('/v1/race/finish', body(x, fast)) }; }
    if (i % 15 === 2) { const both = await Promise.all([post('/v1/race/finish', body(x, d)), post('/v1/race/finish', body(x, d))]); return { kind: 'double', both }; }
    if (i % 15 === 3) return { kind: 'badtoken', r: await post('/v1/race/finish', { ...body(x, d), raceToken: x.r.body.raceToken.slice(0, -4) + 'AAAA' }) };
    if (i < 10) return { kind: 'signed', r: await post('/v1/race/finish', body(x, d), signedIn[i].token), track: x.track };
    return { kind: 'honest', r: await post('/v1/race/finish', body(x, d)) };
  }));
  const honest = out.filter(o => o.kind === 'honest' || o.kind === 'signed');
  check(`${honest.length} honest finishes at once are all accepted`, honest.every(o => o.r.status === 200), honest.filter(o => o.r.status !== 200).slice(0, 5).map(o => o.r.status + ' ' + o.r.text.slice(0, 100)).join(' | '));
  const forged = out.filter(o => o.kind === 'forged');
  check(`${forged.length} too-fast finishes are all refused`, forged.every(o => o.r.status === 422), forged.map(o => o.r.status).join(','));
  const doubled = out.filter(o => o.kind === 'double');
  check(`${doubled.length} races finished twice at the same instant: one accepted, one refused, never a 5xx`,
    doubled.every(o => o.both.filter(r => r.status === 200).length === 1 && o.both.every(r => r.status < 500)), doubled.map(o => o.both.map(r => r.status).join('/')).join(' '));
  const badtok = out.filter(o => o.kind === 'badtoken');
  check(`${badtok.length} finishes with a tampered race token are refused`, badtok.every(o => o.r.status === 401), badtok.map(o => o.r.status).join(','));
  const board = await get('/v1/leaderboard?track=beach');
  check('signed-in finishes reach the leaderboard', board.status === 200 && board.body.entries.length >= 1, board.text.slice(0, 200));

  // the Weekly's board: fastest first, your row with its rank and gap, nothing from another circuit or an unclaimed guest
  const fin = (r, d, tok) => post('/v1/race/finish', { raceToken: r.body.raceToken, totalMs: d.ms, lapMs: d.soloLaps, checkpointMs: d.cps, path: d.path(), position: 1 }, tok);
  const wf = await Promise.all(weeklyRuns.map(x => fin(x.r, drive(wTrack, x.v), x.w.token)));
  const xf = await fin(offTrack.r, drive(other, 40), offTrack.w.token);
  const gd = drive(wTrack, 50), gf = await fin(guestRun, gd);
  check('12 Weekly runs, the wrong-circuit one and a guest run are all accepted as races', wf.every(r => r.status === 200) && xf.status === 200 && gf.status === 200, wf.map(r => r.status).join(',') + ' ' + xf.status + ' ' + gf.status);
  const slowest = weeklyRacers[0], fastest = weeklyRacers[11];
  const wb = (await get('/v1/weekly?wallet=' + slowest.address)).body;
  const order = wb.entries.map(e => e.wallet);
  check(`the Weekly board lists the 12, fastest first (${wb.entries.length} entries)`, wb.entries.length === 12 && order[0] === fastest.address && order[11] === slowest.address
        && wb.entries.every((e, i) => i === 0 || e.bestMs >= wb.entries[i - 1].bestMs), JSON.stringify(wb.entries.map(e => e.bestMs)));
  check('your row: rank 12 of 12, with the gap to the one above', wb.me?.rank === 12 && wb.me.entrants === 12 && wb.me.gapMs > 0, JSON.stringify(wb.me));
  check('no pot set: no prize promised to anyone', wb.pot === null && wb.entries.every(e => e.prize === null));
  const g = await signIn();
  const claimed = await post('/v1/race/claim', { raceToken: guestRun.body.raceToken }, g.token);
  const wb2 = (await get('/v1/weekly?wallet=' + g.address)).body;
  check("a guest's Weekly run joins the board once claimed by a wallet, at its time", claimed.status === 200 && wb2.entries.length === 13 && wb2.me?.rank === 1, JSON.stringify(wb2.me));
}

console.log(`\n${failures ? failures + ' FAILURE(S)' : 'ALL PASS'}`);
if (process.argv.includes('--keep')) console.log(wlog.split('\n').filter(l => /error|Error|exception/.test(l)).slice(-30).join('\n'));
stop();
process.exit(failures ? 1 : 0);
