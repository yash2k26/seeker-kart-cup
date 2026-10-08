// End-to-end check of free rooms, the referee and the leaderboard against a local `wrangler dev` (MOCK_CHAIN=1 in
// .dev.vars). Every racer here DRIVES the real circuit (drive.mjs): the room's referee checks each state, so a finish
// only counts if the drive behind it was possible. Fails loudly on the first wrong number. Paid rooms and their escrow
// are checked on a local chain by smoke-escrow.ts.
//   cd worker && npx wrangler dev --port 8787   (other terminal)    node scripts/smoke-room.mjs
import { drive, race as driveRace } from './drive.mjs';

const BASE = process.env.BASE ?? 'http://127.0.0.1:8787';
const WS = BASE.replace(/^http/, 'ws');

let failures = 0;
const check = (what, ok, extra = '') => { console.log(`${ok ? 'pass' : 'FAIL'}  ${what}${ok ? '' : '  ' + extra}`); if (!ok) failures++; };
const post = async (path, body, token, extra = {}) => { const r = await fetch(BASE + path, { method: 'POST', headers: { 'content-type': 'application/json', ...(token ? { authorization: 'Bearer ' + token } : {}), ...extra }, body: JSON.stringify(body) }); return { status: r.status, body: await r.json().catch(() => ({})) }; };
const sleep = ms => new Promise(r => setTimeout(r, ms));

// ---------- 0. config: live prices, catalog, stake tiers ----------
{
  const cfg = await (await fetch(`${BASE}/v1/config`)).json();
  check('config carries the treasury, the escrow program and the stake tiers', 'treasuryOwner' in cfg && 'escrowProgram' in cfg && cfg.stakes.SOL.length === 5 && cfg.stakes.SKR.length === 4);
  check('catalog is priced in SOL and quoted in both currencies', cfg.catalog.length >= 14 && cfg.catalog.every(i => typeof i.sol === 'number' && !('usd' in i) && /^\d+$/.test(i.price.SKR)));
  const rally = cfg.catalog.find(i => i.sku === 'paint.rally'), hero = cfg.catalog.find(i => i.sku === 'kart.firetruck');
  check('the SKR-only paint has no SOL price', rally && rally.price.SOL === '' && rally.skrOnly === true);
  check('a hero kart is 0.15 SOL and a clean SKR amount (multiple of 5 SKR)', hero?.price?.SOL === '150000000' && BigInt(hero.price.SKR) % 5000000n === 0n && BigInt(hero.price.SKR) > 0n, hero?.price?.SKR);
  check('SOL is quoted at a 20% premium over SKR', hero && Math.abs(Number(hero.price.SOL) / 1e9 * cfg.rates.SOL / (Number(hero.price.SKR) / 1e6 * cfg.rates.SKR) - 1.2) < 0.08, `${hero?.price?.SOL} lamports @ $${cfg.rates.SOL}`);
  check('bundles list what they grant', cfg.catalog.find(i => i.sku === 'bundle.garage')?.grants?.length > 10);
  console.log(`   (SKR $${cfg.rates.SKR} · SOL $${cfg.rates.SOL} · ${cfg.rates.source})`);
}

// ---------- 0a. solo races start now; they are finished at the end, when enough wall time has passed ----------
const soloHonest = await post('/v1/race/start', { deviceId: 'devSOLO01', trackId: 'forest' });
const soloForged = await post('/v1/race/start', { deviceId: 'devSOLO02', trackId: 'forest' });
const soloFaster = await post('/v1/race/start', { deviceId: 'devSOLO03', trackId: 'forest' });
check('solo races get race tokens', [soloHonest, soloForged, soloFaster].every(r => r.status === 200 && r.body.raceToken));

class Client {
  constructor(name) { this.name = name; this.inbox = []; this.waiters = []; }
  connect(code) {
    return new Promise((res, rej) => {
      this.ws = new WebSocket(`${WS}/v1/rooms/${code}/ws`);
      this.ws.onopen = () => res(this);
      this.ws.onerror = e => rej(new Error('ws error ' + (e.message ?? '')));
      this.ws.onmessage = ev => {
        const m = JSON.parse(ev.data);
        if (m.t === 'sp') { this.send({ t: 'spr', s: m.s }); return; }   // the room timing our round trip, as the phone answers it
        if (m.t === 'welcome') this.id = m.id;
        this.inbox.push(m); this.waiters = this.waiters.filter(w => !w(m));
      };
    });
  }
  send(o) { try { this.ws.send(JSON.stringify(o)); } catch { /* closed */ } }
  /** Resolves with the first message (past or future) matching pred. */
  wait(pred, ms = 8000, label = '') {
    const hit = this.inbox.find(pred);
    if (hit) { this.inbox.splice(this.inbox.indexOf(hit), 1); return Promise.resolve(hit); }
    return new Promise((res, rej) => {
      const timer = setTimeout(() => rej(new Error(`${this.name}: timeout waiting for ${label || pred}; last messages: ` +
        this.inbox.slice(-6).map(m => m.t + (m.msg ? ':' + m.msg : '') + (m.room ? ':' + m.room.state + ':' + m.room.players.map(p => (p.paid ? 'P' : '-') + (p.connected ? '' : 'off')).join(',') : '')).join(' | '))), ms);
      this.waiters.push(m => { if (!pred(m)) return false; clearTimeout(timer); this.inbox.splice(this.inbox.indexOf(m), 1); res(m); return true; });
    });
  }
  roomWhere(pred, ms, label) { return this.wait(m => (m.t === 'room' || m.t === 'welcome') && pred(m.room), ms, label).then(m => m.room); }
  drain() { this.inbox = this.inbox.filter(m => m.t !== 'room' && m.t !== 's'); }
  close() { try { this.ws.close(); } catch {} }
}

/** One race: everyone loads, then each client drives at its speed (m/s) unless `opts[i]` makes it cheat. Every
 *  racer that drove honestly must finish: an honest DNF is the referee being wrong, and in a paid room it costs money. */
async function race(clients, speeds, opts = []) {
  const loads = await Promise.all(clients.map(c => c.wait(m => m.t === 'load', 8000, 'load')));
  const track = loads[0].track;
  clients.forEach(c => c.send({ t: 'loaded' }));
  const cd = await Promise.all(clients.map(c => c.wait(m => m.t === 'countdown', 8000, 'countdown')));
  check(`countdown carries a server start time (${track})`, cd.every(m => typeof m.startAt === 'number' && m.startAt > Date.now()));
  const drives = clients.map((c, i) => driveRace(o => c.send(o), cd[i].startAt, drive(track, speeds[i]), opts[i] ?? {}));
  const relayed = await clients[1].wait(m => m.t === 's', 15000, 'relayed state');
  check('state is relayed with the sender id', !!relayed.id && Array.isArray(relayed.p));
  await Promise.all(drives);
  check('nobody gets its own states back', clients.every(c => !c.inbox.some(m => m.t === 's' && m.id === c.id)));
  const results = await Promise.all(clients.map(c => c.wait(m => m.t === 'results', 20000, 'results')));
  const honest = clients.filter((_, i) => !opts[i] || Object.keys(opts[i]).length === 0).map(c => c.id);
  const wrongful = results[0].results.order.filter(o => honest.includes(o.id) && o.dnf);
  check(`every honest racer's finish is accepted (${track})`, wrongful.length === 0, JSON.stringify(wrongful));
  return results;
}

// ---------- 0b. public rooms: listed while open, gone once started ----------
{
  const pub = await post('/v1/rooms', { tracks: ['speedway'], minStake: '0', currency: 'SOL', device: 'devPUB00000', isPublic: true });
  const priv = await post('/v1/rooms', { tracks: ['beach'], minStake: '0', currency: 'SOL', device: 'devPRV00000' });
  check('public and private rooms are created', pub.status === 200 && priv.status === 200);
  const before = await (await fetch(`${BASE}/v1/rooms/public`)).json();
  check('an empty public room is not listed yet', !before.rooms.some(r => r.code === pub.body.code));
  const h = await new Client('pubhost').connect(pub.body.code), q = await new Client('privhost').connect(priv.body.code);
  h.send({ t: 'hello', device: 'devPUB00000', character: 2, kart: 'oobi' }); q.send({ t: 'hello', device: 'devPRV00000' });
  await Promise.all([h.wait(m => m.t === 'welcome', 5000, 'pub welcome'), q.wait(m => m.t === 'welcome', 5000, 'priv welcome')]);
  await sleep(400);
  const listed = await (await fetch(`${BASE}/v1/rooms/public`)).json();
  const mine = listed.rooms.find(r => r.code === pub.body.code);
  check('the public room is listed with its host and head count', mine && mine.players === 1 && mine.host.startsWith('Guest') && mine.tracks[0] === 'speedway' && mine.maxPlayers === 6, JSON.stringify(listed));
  check('the private room is not listed', !listed.rooms.some(r => r.code === priv.body.code));
  const g2 = await new Client('joiner').connect(pub.body.code);
  g2.send({ t: 'hello', device: 'devJOIN0000' });
  await g2.wait(m => m.t === 'welcome', 5000, 'joiner welcome');
  await sleep(400);
  const two = await (await fetch(`${BASE}/v1/rooms/public`)).json();
  check('head count follows joins', two.rooms.find(r => r.code === pub.body.code)?.players === 2);
  h.send({ t: 'start' });
  await Promise.all([h, g2].map(c => c.wait(m => m.t === 'load', 8000, 'load')));
  await sleep(400);
  const after = await (await fetch(`${BASE}/v1/rooms/public`)).json();
  check('a started room leaves the list', !after.rooms.some(r => r.code === pub.body.code));
  h.close(); q.close(); g2.close();
}

// ---------- 1. free room, three guests: one drives, one teleports once, one forges its finish ----------
{
  const created = await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devA000000' });
  check('create a free room as a guest', created.status === 200 && /^[A-Z2-9]{5}$/.test(created.body.code ?? ''), JSON.stringify(created));
  const code = created.body.code;
  const info = await (await fetch(`${BASE}/v1/rooms/${code}`)).json();
  check('room info is readable before anyone joins', info.state === 'lobby' && info.players.length === 0);
  const page = await fetch(`${BASE}/r/${code.toLowerCase()}`);
  check('invite page renders (case-insensitive code)', page.status === 200 && (await page.text()).includes(code));

  const a = await new Client('A').connect(code), b = await new Client('B').connect(code), c = await new Client('C').connect(code);
  a.send({ t: 'hello', device: 'devA000000', character: 2, kart: 'firetruck' });   // a shop kart a guest does not own
  const wa = await a.wait(m => m.t === 'welcome', 5000, 'welcome A');
  check('the creating device is the host', wa.room.players[0].host && wa.room.hostId === wa.id);
  check("a guest's room id is not its device id (nobody can take its seat from a snapshot)", wa.id.startsWith('g:') && !wa.id.includes('devA000000'));
  check('a guest cannot race a shop kart it does not own', wa.room.players[0].kart === 'oodi', wa.room.players[0].kart);
  b.send({ t: 'hello', device: 'devB000000', character: 5, kart: 'oozi' });
  c.send({ t: 'hello', device: 'devC000000', character: 3, kart: 'oopi' });
  await Promise.all([b.wait(m => m.t === 'welcome', 5000, 'welcome B'), c.wait(m => m.t === 'welcome', 5000, 'welcome C')]);
  const three = await a.roomWhere(r => r.players.length === 3, 5000, 'three players');
  check('guests in a free room count as paid', three.players.every(p => p.paid));
  check('guest names are derived from the device', three.players[1].name.startsWith('Guest '));
  check('a free kart is kept', three.players.find(p => p.id === b.id)?.kart === 'oozi');

  b.send({ t: 'start' });
  await sleep(300);
  check('only the host can start', !b.inbox.some(m => m.t === 'load') && !a.inbox.some(m => m.t === 'load'));
  a.send({ t: 'start' });
  const [ra] = await race([a, b, c], [55, 54, 50], [{}, { teleportAt: 6 }, { forgeAt: 3 }]);
  const byId = Object.fromEntries(ra.results.order.map(o => [o.id, o]));
  const idB = b.id;
  check('single race is final', ra.results.final === true && ra.results.race === 1 && ra.results.races === 1);
  check('the honest driver wins', ra.results.order[0].id === wa.id && !ra.results.order[0].dnf && ra.results.order[0].place === 1, JSON.stringify(ra.results.order));
  check('a finish with no drive behind it is a DNF', ra.results.order.filter(o => o.dnf).length === 2, JSON.stringify(ra.results.order));
  check('a racer who teleported is a DNF even after driving on to the line', idB && byId[idB]?.dnf === true);
  check('DNFs are ordered by the gates the referee saw: the teleporter ahead of the forger', idB && byId[idB]?.place === 2);
  check('points 10 / 8 / 6', ra.results.order.map(o => o.points).join(',') === '10,8,6');
  check('free room pays nothing', ra.payouts.length === 0 && ra.pool === '0');
  const st = await (await fetch(`${BASE}/v1/rooms/${code}`)).json();
  check('room is finished', st.state === 'finished');
  a.drain();
  a.send({ t: 'again' });
  const again = await a.roomWhere(r => r.state === 'lobby' && r.round === 2, 5000, 'rematch lobby');
  check('rematch: same code, round 2, everyone still in', again.players.length === 3 && again.code === code);
  a.close(); b.close(); c.close();
}

// ---------- 1b. a phone whose race clock runs 5 s late: an honest drive, every time it reports 5 s short ----------
{
  const created = await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devL000000' });
  const code = created.body.code;
  const a = await new Client('honest').connect(code), l = await new Client('late clock').connect(code);
  a.send({ t: 'hello', device: 'devL000000' }); l.send({ t: 'hello', device: 'devM000000' });
  await Promise.all([a, l].map(x => x.wait(m => m.t === 'welcome', 5000, 'welcome')));
  a.send({ t: 'start' });
  const [rl] = await race([a, l], [52, 52], [{}, { clockLag: 5 }]);
  const late = rl.results.order.find(o => o.id === l.id);
  check('a race clock running 5 s behind the room is a DNF, not a 5 s head start', late?.dnf === true && rl.results.order[0].id === a.id, JSON.stringify(rl.results.order));
  a.close(); l.close();
}

// ---------- 2. the host drops out of a cup: the room hands the cup to someone still there ----------
{
  const created = await post('/v1/rooms', { tracks: ['forest', 'forest'], minStake: '0', currency: 'SOL', device: 'devH000000' });
  const code = created.body.code;
  const h = await new Client('host').connect(code), x = await new Client('X').connect(code), y = await new Client('Y').connect(code);
  h.send({ t: 'hello', device: 'devH000000' }); x.send({ t: 'hello', device: 'devX000000' }); y.send({ t: 'hello', device: 'devY000000' });
  const wh = await h.wait(m => m.t === 'welcome', 5000, 'welcome host');
  await Promise.all([x, y].map(c => c.wait(m => m.t === 'welcome', 5000, 'welcome')));
  await h.roomWhere(r => r.players.length === 3, 5000, 'three');
  h.send({ t: 'start' });
  const [r1] = await race([h, x, y], [55, 53, 51]);
  check('race 1 of the cup is not final', r1.results.final === false);
  x.drain(); y.drain();
  h.close();                                                      // the host's phone dies
  const moved = await x.roomWhere(r => r.hostId !== wh.id, 5000, 'host moved');
  check('the host moves to a racer still in the room', moved.hostId && moved.hostId !== wh.id);
  [x, y].find(c => c.id === moved.hostId).send({ t: 'next' });
  const [r2] = await race([x, y], [55, 53]);
  check('the cup is finished by the racers left', r2.results.final === true && r2.results.race === 2);
  check('the dropped host keeps its race-1 points in the standings', r2.standings.some(s => s.id === wh.id && s.points === 10));
  x.close(); y.close();
}

// ---------- 6. the leaderboard: a result needs the drive behind it ----------
{
  const d = drive('forest', 50);
  const honest = await post('/v1/race/finish', { raceToken: soloHonest.body.raceToken, totalMs: d.ms, lapMs: d.soloLaps, checkpointMs: d.cps, position: 1, path: d.path() });
  check('an honest drive is accepted', honest.status === 200 && honest.body.accepted === true, JSON.stringify(honest));
  const again = await post('/v1/race/finish', { raceToken: soloHonest.body.raceToken, totalMs: d.ms, lapMs: d.soloLaps, checkpointMs: d.cps, position: 1, path: d.path() });
  check('a race can be finished once', again.status === 409);
  const straight = Array.from({ length: 60 }, (_, i) => [i * 0.5, i * 20, 0, 0]);
  const forged = await post('/v1/race/finish', { raceToken: soloForged.body.raceToken, totalMs: 30000, lapMs: [15000, 15000], checkpointMs: Array.from({ length: 22 }, (_, i) => 1000 + i * 1300), position: 1, path: straight });
  check('typed-in times with a made-up path are refused', forged.status === 422, JSON.stringify(forged));
  const faster = await post('/v1/race/finish', { raceToken: soloFaster.body.raceToken, totalMs: d.ms - 4000, lapMs: d.soloLaps.map(l => l - 2000), checkpointMs: d.cps, position: 1, path: d.path() });
  check('a real drive claimed 4 s faster is stored at the time the referee measured, not the claim', faster.status === 200 && Math.abs(faster.body.totalMs - d.ms) < 300, JSON.stringify(faster.body));
  const noPath = await post('/v1/race/finish', { raceToken: (await post('/v1/race/start', { deviceId: 'devSOLO04', trackId: 'forest' })).body.raceToken, totalMs: d.ms, lapMs: d.soloLaps, checkpointMs: d.cps, position: 1 });
  check('a result with no path is refused', noPath.status === 422);
}


// ---------- 10. lobby: one player per socket, the organiser keeps the room, names are plain text ----------
{
  const code = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devORG00000' })).body.code;
  const o = await new Client('organiser').connect(code), j = await new Client('joiner').connect(code);
  o.send({ t: 'hello', device: 'devORG00000' });
  await o.wait(m => m.t === 'welcome', 5000, 'welcome');
  j.send({ t: 'hello', device: '<B>x-joiner' });
  const wj = await j.wait(m => m.t === 'welcome', 5000, 'welcome joiner');
  check('a guest name carries no markup', !wj.room.players.some(p => /[<>]/.test(p.name)), JSON.stringify(wj.room.players.map(p => p.name)));
  j.send({ t: 'hello', device: 'devSECOND00' });
  const twice = await j.wait(m => m.t === 'error', 5000, 'second identity refused');
  check('one socket cannot seat a second player', /already joined/.test(twice.msg));
  o.close();
  const moved = await j.roomWhere(r => r.hostId === wj.id, 5000, 'host to joiner');
  check('the host passes on while the organiser is away', moved.hostId === wj.id);
  const o2 = await new Client('organiser back').connect(code);
  o2.send({ t: 'hello', device: 'devORG00000' });
  const wo = await o2.wait(m => m.t === 'welcome', 5000, 'organiser back');
  check('the organiser takes the room back on return', wo.room.hostId === wo.id, JSON.stringify({ host: wo.room.hostId, me: wo.id }));
  const silent = await new Client('silent').connect(code);
  j.send({ t: 'ready', ready: true });
  await sleep(600);
  check('a socket that never said hello hears nothing', silent.inbox.length === 0, JSON.stringify(silent.inbox.map(m => m.t)));
  silent.close(); o2.close(); j.close();
}

// ---------- 11. a bogus finish, a DNF's states and padded frames: none of them reach the other racers ----------
{
  const code = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devSLOW0000' })).body.code;
  const slow = await new Client('slow').connect(code), griefer = await new Client('griefer').connect(code), fast = await new Client('fast').connect(code);
  slow.send({ t: 'hello', device: 'devSLOW0000' }); griefer.send({ t: 'hello', device: 'devGRIEF000' }); fast.send({ t: 'hello', device: 'devFAST0000' });
  await Promise.all([slow, griefer, fast].map(x => x.wait(m => m.t === 'welcome', 5000, 'welcome')));
  let biggest = 0, fromDnf = 0;
  // states stamped after the griefer's bogus finish (at race time 1 s): it is a DNF from then on
  slow.ws.addEventListener('message', ev => { biggest = Math.max(biggest, ev.data.length); const m = JSON.parse(ev.data); if (m.t === 's' && m.id === griefer.id && m.tm > 1.1) fromDnf++; });
  slow.send({ t: 'start' });
  // slow needs ~66 s at 32 m/s; fast finishes first, so the last minute starts at fast's finish, not at the griefer's bogus one
  const loads = await Promise.all([slow, griefer, fast].map(x => x.wait(m => m.t === 'load', 8000, 'load')));
  [slow, griefer, fast].forEach(x => x.send({ t: 'loaded' }));
  const cd = await Promise.all([slow, griefer, fast].map(x => x.wait(m => m.t === 'countdown', 8000, 'countdown')));
  const track = loads[0].track;
  const drives = [
    driveRace(o => slow.send(o), cd[0].startAt, drive(track, 32)),
    driveRace(o => griefer.send(o), cd[1].startAt, drive(track, 45), { forgeAt: 1, keepDriving: true, padAt: 0.5 }),
    driveRace(o => fast.send(o), cd[2].startAt, drive(track, 50)),
  ];
  await Promise.all(drives);
  const [res] = await Promise.all([slow, griefer, fast].map(x => x.wait(m => m.t === 'results', 20000, 'results')));
  const bySlow = res.results.order.find(o => o.id === slow.id);
  check('a bogus finish does not cut the race short for an honest slow racer', bySlow && !bySlow.dnf && bySlow.ms > 60000, JSON.stringify(res.results.order));
  check('a DNF racer\'s states are not relayed', fromDnf === 0, `${fromDnf} relayed`);
  check('a padded state is never relayed', biggest < 8000, `${biggest} bytes`);
  slow.close(); griefer.close(); fast.close();
}

// ---------- 12. the fin lost with a socket at the line: the room followed the drive over it, so that is the finish ----------
{
  const code = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devLOST000' })).body.code;
  const lost = await new Client('lost fin').connect(code), other = await new Client('other').connect(code);
  lost.send({ t: 'hello', device: 'devLOST000' }); other.send({ t: 'hello', device: 'devOTHER00' });
  await Promise.all([lost, other].map(x => x.wait(m => m.t === 'welcome', 5000, 'welcome')));
  lost.send({ t: 'start' });
  const loads = await Promise.all([lost, other].map(x => x.wait(m => m.t === 'load', 8000, 'load')));
  [lost, other].forEach(x => x.send({ t: 'loaded' }));
  const cd = await Promise.all([lost, other].map(x => x.wait(m => m.t === 'countdown', 8000, 'countdown')));
  const dl = drive(loads[0].track, 54);
  await Promise.all([
    driveRace(o => lost.send(o), cd[0].startAt, dl, { noFin: true }).then(() => lost.close()),
    driveRace(o => other.send(o), cd[1].startAt, drive(loads[0].track, 48)),
  ]);
  const res = await other.wait(m => m.t === 'results', 20000, 'results');
  const lr = res.results.order.find(o => o.id === lost.id);
  check('a racer whose fin was lost at the line still wins', lr && !lr.dnf && lr.place === 1 && Math.abs(lr.ms - dl.ms) < 800, JSON.stringify(res.results.order));
  other.close();
}

// ---------- 12b. a network stall across the line: the states and the fin arrive in one burst, and the win stands ----------
{
  const code = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devSTALL00' })).body.code;
  const st = await new Client('stalled').connect(code), other = await new Client('other').connect(code);
  st.send({ t: 'hello', device: 'devSTALL00' }); other.send({ t: 'hello', device: 'devOTHER01' });
  await Promise.all([st, other].map(x => x.wait(m => m.t === 'welcome', 5000, 'welcome')));
  st.send({ t: 'start' });
  const loads = await Promise.all([st, other].map(x => x.wait(m => m.t === 'load', 8000, 'load')));
  [st, other].forEach(x => x.send({ t: 'loaded' }));
  const cd = await Promise.all([st, other].map(x => x.wait(m => m.t === 'countdown', 8000, 'countdown')));
  const ds = drive(loads[0].track, 54);
  await Promise.all([
    driveRace(o => st.send(o), cd[0].startAt, ds, { stall: [ds.finishT - 0.3, 1.5] }),
    driveRace(o => other.send(o), cd[1].startAt, drive(loads[0].track, 48)),
  ]);
  const res = await other.wait(m => m.t === 'results', 20000, 'results');
  const sr = res.results.order.find(o => o.id === st.id);
  check('a racer whose link stalls across the line still wins', sr && !sr.dnf && sr.place === 1, JSON.stringify(res.results.order));
  st.close(); other.close();
}

// ---------- 12c. a slow driver whose race clock runs slow, with a forged round trip to widen the allowance: a DNF ----------
{
  const code = (await post('/v1/rooms', { tracks: ['forest'], minStake: '0', currency: 'SOL', device: 'devSTRETCH' })).body.code;
  const cheat = await new Client('stretch').connect(code), honest = await new Client('honest').connect(code);
  cheat.send({ t: 'hello', device: 'devSTRETCH' }); honest.send({ t: 'hello', device: 'devHONEST2' });
  await Promise.all([cheat, honest].map(x => x.wait(m => m.t === 'welcome', 5000, 'welcome')));
  cheat.send({ t: 'start' });
  const loads = await Promise.all([cheat, honest].map(x => x.wait(m => m.t === 'load', 8000, 'load')));
  [cheat, honest].forEach(x => x.send({ t: 'loaded' }));
  const cd = await Promise.all([cheat, honest].map(x => x.wait(m => m.t === 'countdown', 8000, 'countdown')));
  cheat.send({ t: 'spr', s: Date.now() - 29_000 });                // a round trip the room never timed
  await Promise.all([
    driveRace(o => cheat.send(o), cd[0].startAt, drive(loads[0].track, 48), { stretch: 0.85 }),
    driveRace(o => honest.send(o), cd[1].startAt, drive(loads[0].track, 50)),
  ]);
  const res = await honest.wait(m => m.t === 'results', 20000, 'results');
  const c = res.results.order.find(o => o.id === cheat.id), h = res.results.order.find(o => o.id === honest.id);
  check('a stretched race clock with a forged round trip is a DNF; the faster honest racer wins', c?.dnf === true && h?.place === 1 && !h.dnf, JSON.stringify(res.results.order));
  cheat.close(); honest.close();
}

// ---------- 13. input hygiene on the open routes ----------
{
  const proto = await post('/v1/rooms', { tracks: ['constructor'], minStake: '0', currency: 'SOL', device: 'devPROTO00' });
  check("'constructor' is not a track", proto.status === 400);
  const protoRace = await post('/v1/race/start', { deviceId: 'devPROTO00', trackId: '__proto__' });
  check("'__proto__' is not a track to race", protoRace.status === 400);
  const huge = await post('/v1/race/start', { deviceId: 'x'.repeat(5000), trackId: 'forest' });
  check('a device id is short text, not a payload', huge.status === 400);
}

// ---------- 7. rate limits on the routes anyone can call ----------
{
  const codes = [];
  for (let i = 0; i < 70; i++) codes.push((await post('/v1/event', { deviceId: 'devSPAM', name: 'spam' })).status);
  check('an event flood is throttled per IP (429)', codes.includes(429), `statuses: ${[...new Set(codes)].join(',')}`);
}

console.log(failures ? `\n${failures} FAILURE(S)` : '\nALL PASS');
process.exit(failures ? 1 : 0);
