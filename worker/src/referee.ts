// The referee: follows one kart round a circuit from its positions alone and says whether that drive was possible.
// A client reports its own times, laps and checkpoints, and all of those can be typed in. What it cannot type in
// cheaply is a path: through the twelve gates in order, never faster than the fastest kart, never across the
// infield. Paid rooms feed it the 15 Hz state stream; the solo leaderboard feeds it a 5 Hz path the phone uploads
// with the finish. Plain data in and out, so a Durable Object can store the state between messages.
//
// Geometry comes from the track generator (TrackSetup.cs writes tracks.json with every scene build), so the
// referee and the scene are always the same circuit.
import GEO from './tracks.json';

export type Geo = {
  lengthM: number; laps: number; halfWidth: number; kerb: number; gateHalfWidth: number;
  gates: number[][];      // [x, y, z, forwardX, forwardZ] per gate; gate 0 is the start/finish line
  centre: number[][];     // [x, z] centreline samples, a closed loop
};
export const TRACK_GEO = GEO as unknown as Record<string, Geo>;
/** A track id's geometry, or undefined. Own keys only: 'constructor' or '__proto__' is not a circuit. */
export const geoOf = (id: unknown): Geo | undefined =>
  typeof id === 'string' && Object.prototype.hasOwnProperty.call(TRACK_GEO, id) ? TRACK_GEO[id] : undefined;

// The fastest kart: maxSpeed 40 x 1.10 (driver +2) x 1.05 (kart +2) + 10 boost = 56.2 m/s on the road, which is the
// fastest surface (Default Road speed 1; dirt 0.9, grass 0.7; no boost pads). 62 leaves 10% for slopes and bumps.
export const V_SUSTAINED = 62;       // m/s, averaged over WINDOW_S
export const V_BURST = 95;           // m/s between two samples: a knock or a landing, never a teleport
// A whole lap cannot average more than this along the centreline. The racing line is a few percent shorter than the
// centreline, and nobody holds top speed through the corners; a bot cutting straight from gate to gate at V_SUSTAINED
// would (11% under the centreline at top speed, measured by scripts/referee-test.ts), so it is what stops that.
export const LAP_V = 60;
// Progress along the centreline can outrun the kart's own speed on the inside of a corner, but not by this much: over
// a second, more than this is a cut across the infield, however ordinary the kart's speed between samples looked.
export const V_PROGRESS = 90;
// Across a gap in the samples longer than half a lap, the kart is taken to have gone round only if a quick human
// driver (well under the kart's top speed) could have; a racer who sat out that long will not beat that anyway.
const HUMAN_V = 45;
const WINDOW_S = 1.0;
const RESPAWN_M = 8;                 // a jump that lands this close to the last gate crossed is KartProgress.Respawn
// The phone counts a gate when the kart's collider meets the gate's trigger: a box 2 m deep centred on the line and
// gateHalfWidth to either side (TrackSetup). The collider reaches 1.96 m ahead of the kart's origin and 1.49 m behind,
// so the phone counts it with the origin anywhere from 3 m short of the line to 3 m past it, entering from any side,
// and up to 2.8 m past the box's edge (the corner of the longest shop kart, 4.48 x 2.3 m, drifting sideways). The
// referee counts it the same way, and never narrower than any phone can: a gate only the phone counted would leave
// the referee a lap behind. (Where only the referee counts, a strip at the edge, the room takes the referee's word.)
const GATE_LEAD_M = 3;
const GATE_DEPTH_M = 6;
const GATE_SIDE_M = 2.8;
const START_DRIFT_M = 5;             // a held kart settling on its springs moves centimetres, not metres
const GRID_M = 4.5;                  // every grid slot is further than this behind the line (the front row is 5.6 m back)
const CORRIDOR_M = 25;               // past the kerb a kart may run this far (sand, grass, a wide line)...
// ...and beyond that it may gain this much lap progress in all, before it has cut the circuit. Progress, not time: a
// kart wedged on a rock out there gains nothing and may sit until the phone respawns it (10 s), while a cut across
// the infield gains a lot quickly.
const OFF_CIRCUIT_M = 150;

export type Sample = { t: number; x: number; y: number; z: number };

// Gates count by PROGRESS along the lap, not by a straight line between two samples passing through the gate: a
// network stall leaves seconds between samples, and the chord between them cuts the corner beside the gate, which
// then never counted and left an honest drive a lap short. Each position is projected onto the centreline near where
// the kart was last seen; the distance round the lap (unwrapped, so it keeps growing lap after lap) crosses each gate.
export type RefState = {
  n: number;                 // gates crossed in order; the first start-line crossing makes it 1
  cross: number[];           // race time (s) of each crossing
  last: number[] | null;     // [t, x, z, distance off the centreline] of the previous sample
  hist: number[][];          // [t, path distance, lap distance, beyond the corridor?] over the last WINDOW_S and a bit
  dist: number;              // path length so far (m)
  A: number | null;          // distance round the circuit (m), unwrapped; the start line is 0, the grid negative
  idx: number;               // nearest centreline segment
  offM: number;              // new lap progress made beyond the corridor (m)
  top?: number;              // the furthest A reached: ground covered twice is not new
  unseen?: boolean;          // the start crossing was never seen (the first sample was past it): lap 1 runs from GO
  bad: string | null;        // the first violation, if any
  grid?: number[];           // [x, z] where the kart sat before GO (states stamped with a negative race time)
};

export const newRef = (): RefState => ({ n: 0, cross: [], last: null, hist: [], dist: 0, A: null, idx: 0, offM: 0, bad: null });

/** Crossings needed to finish: the start line once, then every gate of every lap back to the line. */
export const crossingsToFinish = (g: Geo) => 1 + g.gates.length * g.laps;
export const finished = (s: RefState, g: Geo) => s.n >= crossingsToFinish(g);
export const finishT = (s: RefState, g: Geo) => (finished(s, g) ? s.cross[crossingsToFinish(g) - 1] : null);

/** Lap times (s) from the start-line crossings. The phone has two ways of counting the first lap: the race (KartProgress,
 *  sent by an online finish) starts it at the first crossing of the line; RaceSession (the leaderboard) starts it at GO,
 *  so it includes the roll from the grid to the line. `fromStart` picks the second. */
export function lapTimes(s: RefState, g: Geo, fromStart = false): number[] {
  const k = g.gates.length, out: number[] = [];
  for (let lap = 1; lap <= g.laps && lap * k < s.cross.length; lap++) out.push(s.cross[lap * k] - (lap === 1 && fromStart ? 0 : s.cross[(lap - 1) * k]));
  return out;
}

/** Times (s) of the intermediate gates (not the start line), the way RaceSession reports checkpointMs. */
export function checkpointTimes(s: RefState, g: Geo): number[] {
  const k = g.gates.length;
  return s.cross.filter((_, i) => i % k !== 0);
}

// ---- the circuit as distances: cumulative length at each centreline sample, and each gate's place on the lap ----
type Loop = { cum: number[]; L: number; gateArc: number[]; avgSeg: number };
const loops = new WeakMap<Geo, Loop>();
function loop(g: Geo): Loop {
  let l = loops.get(g);
  if (!l) {
    const c = g.centre, m = c.length, cum = [0];
    for (let i = 1; i <= m; i++) cum.push(cum[i - 1] + Math.hypot(c[i % m][0] - c[i - 1][0], c[i % m][1] - c[i - 1][1]));
    // TrackSetup places gate i on centreline sample floor(i * samples / gates)
    const gateArc = g.gates.map((_, i) => cum[Math.floor(i * m / g.gates.length)]);
    l = { cum, L: cum[m], gateArc, avgSeg: cum[m] / m };
    loops.set(g, l);
  }
  return l;
}

/** The lap distance at which crossing number n happens (0: the start line leaving the grid). */
function crossAt(g: Geo, n: number): number {
  const { L, gateArc } = loop(g), k = g.gates.length;
  return Math.floor(n / k) * L + gateArc[n % k] - GATE_LEAD_M;
}

/** Nearest point of the centreline to (x, z), searching `back` segments behind and `fwd` ahead of the last one:
 *  -> distance from the line, and the lap distance of that point (0..L). */
function project(g: Geo, s: RefState, x: number, z: number, back: number, fwd: number): { d: number; a: number } {
  const c = g.centre, m = c.length, { cum } = loop(g);
  let best = Infinity, bi = s.idx, bu = 0;
  for (let k = -back; k <= fwd; k++) {
    const i = ((s.idx + k) % m + m) % m, j = (i + 1) % m;
    const ax = c[i][0], az = c[i][1], vx = c[j][0] - ax, vz = c[j][1] - az, len2 = vx * vx + vz * vz;
    const u = len2 > 0 ? Math.max(0, Math.min(1, ((x - ax) * vx + (z - az) * vz) / len2)) : 0;
    const d = Math.hypot(x - ax - u * vx, z - az - u * vz);
    if (d < best) { best = d; bi = i; bu = u; }
  }
  s.idx = bi;
  return { d: best, a: cum[bi] + (cum[bi + 1] - cum[bi]) * bu };
}

/** Where the segment from (a0, d0) to (a1, d1) first enters a gate's box, as a fraction of it no earlier than fmin, or
 *  null. a is lap distance past the gate's counting edge (the box is 0..GATE_DEPTH_M), d the distance off the line. */
function enter(a0: number, d0: number, a1: number, d1: number, band: number, fmin: number): number | null {
  let lo = fmin, hi = 1;
  const clip = (v0: number, v1: number, min: number, max: number) => {
    const dv = v1 - v0;
    if (Math.abs(dv) < 1e-9) { if (v0 < min || v0 > max) lo = 2; return; }
    let fa = (min - v0) / dv, fb = (max - v0) / dv;
    if (fa > fb) [fa, fb] = [fb, fa];
    lo = Math.max(lo, fa); hi = Math.min(hi, fb);
  };
  clip(a0, a1, 0, GATE_DEPTH_M);
  clip(d0, d1, -Infinity, band);
  return lo <= hi ? lo : null;
}

/** Feed one sample. Returns the violation it caused, or null. Samples after a violation are still followed (the
 *  room keeps relaying progress) but the first reason sticks. */
export function push(s: RefState, g: Geo, p: Sample): string | null {
  if (![p.t, p.x, p.z].every(Number.isFinite)) return fail(s, 'malformed sample');
  const last = s.last;
  if (last && p.t <= last[0]) return null;                       // duplicate or reordered: ignore, it adds nothing
  let violation: string | null = null;
  const { L, avgSeg } = loop(g), m = g.centre.length, k = g.gates.length;
  // Before GO the kart sits on the grid (the room streams the countdown so the referee sees it behind the line; a
  // start crossing it never saw would leave the drive a lap short for good). A kart that rolls off early is cheating.
  if (p.t < 0) {
    if (!s.grid) s.grid = [p.x, p.z];
    else if (Math.hypot(p.x - s.grid[0], p.z - s.grid[1]) > START_DRIFT_M) violation = fail(s, 'moved before GO');
  }

  if (!last || s.A === null) {                                   // the first sample: find it anywhere on the loop
    const { d, a } = project(g, s, p.x, p.z, 0, m - 1);
    // Behind the line is negative: the grid (just short of a full lap round), or before GO anywhere in the back half.
    // After GO a late first sample may be well round the lap already.
    s.A = a > L - 40 || (p.t < 0 && a > L / 2) ? a - L : a;
    // First seen in a grid slot, or (a phone whose stream began late) no further on than the fastest kart could have
    // got from the front row since GO. Anywhere else, it skipped part of the lap unseen.
    if (s.A > V_SUSTAINED * Math.max(0, p.t) - GRID_M) violation = fail(s, 'first seen ahead of the grid');
    // Gates it is already past were passed unseen: timed now, which is late, so never to its advantage.
    while (!finished(s, g) && s.A >= crossAt(g, s.n)) {
      if (p.t < 0) violation = fail(s, 'crossed the line before GO');
      s.cross.push(p.t); s.n++; s.unseen = true;
    }
    s.last = [p.t, p.x, p.z, d]; s.hist = [[p.t, s.dist, s.A]]; s.top = s.A;
    return violation;
  }

  // The phone holds the kart until GO: across a gap that spans GO the drive began at 0, not at the last grid state
  // (else a state claimed long before GO would lend countdown time to a jump up the road).
  if (last[0] < 0 && p.t >= 0) { last[0] = 0; s.hist = [[0, s.dist, s.A, 0]]; }
  const dt = p.t - last[0];
  const step = Math.hypot(p.x - last[1], p.z - last[2]);
  if (step / Math.max(dt, 0.02) > V_BURST) {
    // KartProgress.Respawn: back to the last gate the phone counted (the start line before any). At the very edge of
    // a trigger the phone and the referee can be one gate apart, so the gate before is a respawn too, and un-counts
    // the last one: the kart drives through it again and it is timed again. Landing further back only loses ground.
    // A phone some gates behind the referee (it missed one at a trigger's edge and has not been back) respawns to its
    // own last gate: any gate up to a lap back is a respawn, and un-counts the gates after it. (The next gate ahead,
    // read as the same gate a lap back, un-counts nearly a lap: nothing to gain.)
    const back = Math.max(1, Math.min(s.n, k));
    const cands = s.n === 0 ? [0] : Array.from({ length: back }, (_, i) => ((s.n - 1 - i) % k + k) % k);
    const hit = cands.findIndex(gi => Math.hypot(p.x - g.gates[gi][0], p.z - g.gates[gi][2]) <= RESPAWN_M);
    if (hit < 0) {
      // A teleport: it counts for nothing (no distance, no progress, no gate) and the caller must not show it to
      // anyone. Follow the kart on from where it now claims to be.
      const why = fail(s, `moved ${step.toFixed(0)} m in ${dt.toFixed(2)} s`);
      const { d, a } = project(g, s, p.x, p.z, 0, m - 1);
      s.A = s.A + (((a - (((s.A % L) + L) % L)) % L + 1.5 * L) % L - L / 2);
      s.last = [p.t, p.x, p.z, d]; s.hist = [[p.t, s.dist, s.A]];
      return why;
    }
    const rewind = hit > 0 && !finished(s, g);
    if (rewind) { s.n -= hit; s.cross.length = s.n; }
    // A reset, not a drive: the kart is at that gate (on the lap of the crossing that counted it), and the speed
    // window starts again. Placed from the gate itself: a projection could not reach several gates back.
    const gi = cands[hit], c = Math.max(0, s.n - 1 - (rewind ? 0 : hit));
    const { gateArc } = loop(g);
    s.idx = Math.floor(gi * m / k) % m;
    const { d, a } = project(g, s, p.x, p.z, 3, 3);
    s.A = Math.floor(c / k) * L + gateArc[gi] + (((a - gateArc[gi]) % L + 1.5 * L) % L - L / 2);
    s.last = [p.t, p.x, p.z, d]; s.hist = [[p.t, s.dist, s.A]];
    return violation;
  }

  s.dist += step;
  // Where on the lap: search from the last place, as far ahead as the fastest kart could have got since.
  const fwd = Math.ceil((V_BURST * dt + 40) / avgSeg), back = Math.ceil(40 / avgSeg);
  const { d, a } = project(g, s, p.x, p.z, back, Math.min(fwd, m - back - 1));
  const A0 = s.A, d0 = last[3] ?? 0;
  let delta = ((a - (((A0 % L) + L) % L)) % L + 1.5 * L) % L - L / 2;   // the shortest way round from the last place
  // The search reached only `back` segments behind: a point "found" further behind than that is the forward window
  // wrapping past half a lap after a long gap, not a kart driven half a lap backwards; but only as far on as a quick
  // human could have got in the gap (else a silent gap and a step back would earn a lap).
  if (delta < -(back + 1) * avgSeg && L + delta <= HUMAN_V * dt + 60) delta += L;
  s.A = A0 + delta;

  // Gates, in order, each timed where the line between the two samples (in lap distance and distance off the line)
  // enters its box. One passed wide of its trigger is not counted, by the phone or here, until the kart goes back
  // through it, on this lap or a later one.
  const band = g.gateHalfWidth + GATE_SIDE_M;
  let fmin = 0;
  while (!finished(s, g)) {
    const edge = crossAt(g, s.n);
    const late = Math.max(0, Math.round((Math.min(A0, s.A) - edge - GATE_DEPTH_M / 2) / L)) * L;   // missed on an earlier lap
    const f = enter(A0 - edge - late, d0, s.A - edge - late, d, band, fmin);
    if (f === null) break;
    const when = last[0] + dt * f;
    if (when < 0) violation = fail(s, 'crossed the line before GO');
    s.cross.push(when);
    s.n++; fmin = f;
    if (s.n > 1 && (s.n - 1) % k === 0) {                        // back at the line: a lap completed
      const from = s.n - 1 - k === 0 && s.unseen ? Math.min(0, s.cross[0]) : s.cross[s.n - 1 - k];
      const lap = s.cross[s.n - 1] - from;
      if (lap < g.lengthM / LAP_V) violation = fail(s, `a ${lap.toFixed(1)} s lap is faster than the circuit allows`);
    }
  }

  const fresh = Math.max(0, s.A - Math.max(A0, s.top ?? A0));   // ground never covered before: circling out there is not
  s.top = Math.max(s.top ?? s.A, s.A);
  // Charged when either end of the step is beyond the corridor: a cut that steps back onto the road pays for it too.
  if ((d > g.halfWidth + g.kerb + CORRIDOR_M || d0 > g.halfWidth + g.kerb + CORRIDOR_M) && !finished(s, g) && fresh > 0) {
    s.offM += fresh;
    if (s.offM > OFF_CIRCUIT_M) violation = fail(s, 'left the circuit');
  }

  const beyond = d > g.halfWidth + g.kerb + CORRIDOR_M ? 1 : 0;
  s.hist.push([p.t, s.dist, s.A, beyond]);
  while (s.hist.length > 2 && s.hist[1][0] <= p.t - WINDOW_S) s.hist.shift();   // keep one sample at or before the window start
  const [t0, d0w, a0w] = s.hist[0];
  if (p.t - t0 >= WINDOW_S) {
    if ((s.dist - d0w) / (p.t - t0) > V_SUSTAINED) violation = fail(s, `sustained ${((s.dist - d0w) / (p.t - t0)).toFixed(1)} m/s`);
    // Far off the line its nearest point swings along the lap as the kart moves (round a corner's outside), so the
    // progress rate means nothing there; the off-circuit budget counts that ground instead.
    if (!s.hist.some(h => h[3]) && (s.A - a0w) / (p.t - t0) > V_PROGRESS) violation = fail(s, `cut the course (${((s.A - a0w) / (p.t - t0)).toFixed(0)} m/s round the lap)`);
  }
  s.last = [p.t, p.x, p.z, d];
  return violation;
}

function fail(s: RefState, why: string): string { if (!s.bad) s.bad = why; return why; }

/** A whole uploaded path at once (solo leaderboard). `fromGrid`: the phone records from GO, so the first sample is
 *  the kart still in its grid slot, not a flying start. */
export function referee(g: Geo, path: Sample[], fromGrid = false): RefState {
  const s = newRef();
  path.forEach((p, i) => {
    push(s, g, p);
    if (i === 0 && fromGrid && !(p.t <= 0.5 && s.A !== null && s.A <= -GRID_M)) fail(s, 'did not start from the grid');
  });
  return s;
}

/** Does a reported result agree with what the referee saw? Times in ms, tolerance for sampling and rounding. */
export function agrees(s: RefState, g: Geo, totalMs: number, lapMs: number[], checkpointMs?: number[], tolMs = 500, lapsFromStart = false): string | null {
  if (s.bad) return s.bad;
  const ft = finishT(s, g);
  if (ft === null) return `crossed ${s.n} of ${crossingsToFinish(g)} gates`;
  if (!(Math.abs(ft * 1000 - totalMs) <= tolMs)) return `finish time ${Math.round(totalMs)} ms does not match the drive (${Math.round(ft * 1000)} ms)`;
  const laps = lapTimes(s, g, lapsFromStart);
  if (lapMs.length !== laps.length || lapMs.some((l, i) => !(Math.abs(l - laps[i] * 1000) <= tolMs))) return `lap times ${lapMs.map(Math.round).join('/')} ms do not match the drive (${laps.map(x => Math.round(x * 1000)).join('/')} ms)`;
  if (checkpointMs) {
    const cps = checkpointTimes(s, g);
    if (checkpointMs.length !== cps.length || checkpointMs.some((c, i) => !(Math.abs(c - cps[i] * 1000) <= tolMs))) return 'checkpoint times do not match the drive';
  }
  return null;
}
