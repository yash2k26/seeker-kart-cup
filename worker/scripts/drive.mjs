// A test racer that drives the real circuit: positions along the centreline the scenes are built from
// (src/tracks.json, written by TrackSetup.cs), at a steady speed, with the finish and lap times of that drive.
// The room's referee checks every state against the circuit, so the bots in bot.mjs and smoke-room.mjs must drive.
import { readFileSync } from 'node:fs';

export const GEO = JSON.parse(readFileSync(new URL('../src/tracks.json', import.meta.url), 'utf8'));
const LEAD = 3;          // the referee counts a gate 3 m short of its line (referee.ts GATE_LEAD_M)
const START = -15;       // the drive begins this far behind the start line, like a grid slot

/** A steady drive of `laps` laps of `id` at `v` m/s: pos(t) for t seconds after GO, and the times the phone reports. */
export function drive(id, v) {
  const g = GEO[id], c = g.centre, cum = [0];
  for (let i = 1; i <= c.length; i++) cum.push(cum[i - 1] + Math.hypot(c[i % c.length][0] - c[i - 1][0], c[i % c.length][1] - c[i - 1][1]));
  const L = cum[c.length];
  const at = s => {
    s = ((s % L) + L) % L;
    let i = 0; while (cum[i + 1] < s) i++;
    const a = c[i], b = c[(i + 1) % c.length], u = (s - cum[i]) / (cum[i + 1] - cum[i]);
    return [a[0] + (b[0] - a[0]) * u, 0, a[1] + (b[1] - a[1]) * u];
  };
  const cross = k => (k * L - LEAD - START) / v;               // time of the k-th start-line crossing (k = 0: leaving the grid)
  const finishT = cross(g.laps);
  const n = g.gates.length, cps = [];                           // the intermediate gates, as RaceSession reports them
  for (let k = 0; k < g.laps; k++) for (let j = 1; j < n; j++) cps.push(Math.round(((k * L + cum[Math.floor(j * c.length / n)] - LEAD - START) / v) * 1000));
  return {
    id, v, finishT, cps,
    ms: Math.round(finishT * 1000),
    laps: Array.from({ length: g.laps }, (_, k) => Math.round((cross(k + 1) - cross(k)) * 1000)),          // KartProgress: lap 1 from the line
    soloLaps: Array.from({ length: g.laps }, (_, k) => Math.round((cross(k + 1) - (k ? cross(k) : 0)) * 1000)),   // RaceSession: lap 1 from GO
    pos: t => at(START + v * Math.max(0, t)),
    /** The solo upload: [t, x, y, z] every 0.2 s to just past the line. */
    path: () => { const out = []; for (let t = 0; t <= finishT + 0.6; t += 0.2) { const q = at(START + v * t); out.push([+t.toFixed(2), +q[0].toFixed(1), 0, +q[2].toFixed(1)]); } return out; },
  };
}

/** Stream `d` into a room socket as the phone does (15 Hz, race clock from the room's GO), finish half a second
 *  after the line and keep streaming for a second. `send` takes a message object. Resolves when the fin is sent.
 *  Cheats and accidents for the tests: forgeAt (claim the finish at that race time; keepDriving: stream on anyway),
 *  teleportAt (one state 8 s ahead), clockLag (a race clock that reads late), noFin (the socket dies 0.2 s past the
 *  line, before the fin), padAt (one state padded with 100 KB of junk), stall ([from, seconds] of race time: the
 *  network holds everything sent then and delivers it in one burst, as a mobile link does after a retransmit), stretch
 *  (a race clock that runs at this fraction of real time: a slow driver claiming a fast time). */
export function race(sendNow, startAt, d, { forgeAt = null, keepDriving = false, teleportAt = null, clockLag = 0, noFin = false, padAt = null, stall = null, stretch = 1 } = {}) {
  const held = [];
  const send = o => {
    const real = (Date.now() - startAt) / 1000;
    if (stall && real >= stall[0] && real < stall[0] + stall[1]) { held.push(o); return; }
    while (held.length) sendNow(held.shift());
    sendNow(o);
  };
  const flush = () => { while (held.length) sendNow(held.shift()); };
  return new Promise(done => {
    const resolve = v => { if (stall) setTimeout(() => { flush(); done(v); }, Math.max(0, (startAt + (stall[0] + stall[1]) * 1000) - Date.now())); else done(v); };
    let finSent = false, forged = false, teleported = false, padded = false;
    const timer = setInterval(() => {
      const real = (Date.now() - startAt) / 1000;
      const tm = real * stretch - clockLag;                        // a cheat: the kart drives in real time, its clock reads late
      if (forgeAt !== null && tm >= forgeAt && !forged) {        // a cheat: claim the finish without driving it
        forged = true; send({ t: 'fin', ms: d.ms, laps: d.laps });
        if (!keepDriving) { finSent = true; clearInterval(timer); resolve('forged'); return; }
      }
      let p = d.pos(real);
      if (teleportAt !== null && real >= teleportAt && !teleported) { teleported = true; p = d.pos(real + 8); }   // one state 8 s ahead
      const st = { t: 's', p: p.map(x => +x.toFixed(2)), r: [0, 0.7, 0, 0.7], v: [0, 0, d.v], st: 0, lap: 1, cp: 0, sc: 0, tm: +tm.toFixed(3) };
      if (padAt !== null && real >= padAt && !padded) { padded = true; st.junk = 'x'.repeat(100_000); }
      send(st);
      if (noFin && real >= d.finishT + 0.2) { clearInterval(timer); resolve('dropped'); return; }
      if (!finSent && !forged && real >= d.finishT + 0.5) { finSent = true; send({ t: 'fin', ms: d.ms - Math.round(clockLag * 1000), laps: d.laps }); }
      if (real >= d.finishT + 1.0) { clearInterval(timer); resolve('fin'); }
    }, 66);
  });
}
