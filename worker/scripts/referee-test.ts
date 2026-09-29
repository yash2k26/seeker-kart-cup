// Self-check of the referee on every circuit: synthetic drives along the real centreline (tracks.json).
//   npm run test:referee
import { TRACK_GEO, referee, agrees, finishT, lapTimes, checkpointTimes, V_SUSTAINED, LAP_V, type Geo, type Sample } from '../src/referee';

let failures = 0;
const check = (ok: boolean, what: string) => { if (!ok) { failures++; console.log('  FAIL', what); } };

/** Arc-length parametrised centreline: position at s metres from sample 0 (the start line), wrapping. */
function path(g: Geo) {
  const c = g.centre, cum = [0];
  for (let i = 1; i <= c.length; i++) cum.push(cum[i - 1] + Math.hypot(c[i % c.length][0] - c[i - 1][0], c[i % c.length][1] - c[i - 1][1]));
  const L = cum[c.length];
  const at = (s: number, lateral = 0) => {
    s = ((s % L) + L) % L;
    let i = 0; while (cum[i + 1] < s) i++;
    const a = c[i], b = c[(i + 1) % c.length], u = (s - cum[i]) / (cum[i + 1] - cum[i]);
    const dx = b[0] - a[0], dz = b[1] - a[1], n = Math.hypot(dx, dz) || 1;
    return { x: a[0] + dx * u + (dz / n) * lateral, z: a[1] + dz * u - (dx / n) * lateral };
  };
  // TrackSetup puts gate i on centreline sample floor(i * samples / gates), which is not i * L / gates
  const gateArc = (i: number) => cum[Math.floor(i * c.length / g.gates.length)];
  return { L, at, gateArc };
}

/** A drive from 15 m behind the line to just past the finish at `v` m/s, sampled at `hz`. */
function drive(g: Geo, v: number, hz: number, tweak?: (t: number, p: { x: number; z: number }) => { x: number; z: number }): Sample[] {
  const { L, at } = path(g), out: Sample[] = [];
  const s0 = -15, s1 = L * g.laps + 20;
  for (let t = 0; ; t += 1 / hz) {
    const s = s0 + v * t; if (s > s1) break;
    let p = at(s); if (tweak) p = tweak(t, p);
    out.push({ t: +t.toFixed(3), x: p.x, y: 0, z: p.z });
  }
  return out;
}

for (const [id, g] of Object.entries(TRACK_GEO)) {
  const { L } = path(g);
  console.log(`${id}: centreline ${L.toFixed(0)} m, ${g.gates.length} gates, ${g.laps} laps`);

  for (const hz of [5, 15]) {
    const s = referee(g, drive(g, 45, hz));
    const ft = finishT(s, g)!;
    check(s.bad === null && ft !== null, `${id} honest 45 m/s @${hz} Hz: bad=${s.bad} crossings=${s.n}`);
    const expect = (15 + L * g.laps) / 45;
    check(ft !== null && Math.abs(ft - expect) < 0.3, `${id} honest finish ${ft?.toFixed(2)} s vs ${expect.toFixed(2)} s`);
    const laps = lapTimes(s, g).map(x => Math.round(x * 1000)), cps = checkpointTimes(s, g).map(x => Math.round(x * 1000));
    check(agrees(s, g, Math.round(ft * 1000), laps, cps) === null, `${id} honest result agrees`);
    check(agrees(s, g, Math.round(ft * 1000) - 3000, laps, cps) !== null, `${id} a claimed time 3 s faster than the drive is refused`);
  }

  check(referee(g, drive(g, 70, 15)).bad?.startsWith('sustained') === true, `${id} 70 m/s is refused`);
  // the countdown: the grid seen before GO is fine; rolling off it, or crossing the line before GO, is not
  const withGrid = (early: number) => { const d = drive(g, 45, 15); const pre: Sample[] = [];
    for (let t = -3; t < 0; t += 1 / 15) { const q = d[0]; const roll = Math.max(0, t + early) * 45; pre.push({ t: +t.toFixed(3), x: q.x + roll * g.gates[0][3], y: 0, z: q.z + roll * g.gates[0][4] }); }
    return referee(g, [...pre, ...d.map(p => early ? { ...p, t: p.t - early } : p)]); };
  const held = withGrid(0);
  check(held.bad === null && finishT(held, g) !== null, `${id} a kart held on the grid through the countdown is fine (${held.bad})`);
  const jumped = withGrid(1.0);
  check(jumped.bad !== null, `${id} a kart that rolls off the grid a second before GO is refused (${jumped.bad})`);
  check(referee(g, drive(g, 56, 15)).bad === null, `${id} the fastest kart at top speed all the way round passes`);
  check(referee(g, drive(g, LAP_V + 1, 15)).bad !== null, `${id} a lap averaging more than ${LAP_V} m/s is refused`);

  // teleport: 200 m forward at t = 10 s
  const tp = referee(g, drive(g, 45, 15, (t, p) => (t > 10 ? path(g).at(45 * t - 15 + 200) : p)));
  check(tp.bad?.startsWith('moved') === true, `${id} a 200 m teleport is refused as a teleport (${tp.bad})`);

  // respawn: at t = 12 s the kart is put back on the last gate crossed, then drives on from there
  const hz = 15, samples: Sample[] = [];
  const base = drive(g, 45, hz);
  const s12 = referee(g, base.filter(p => p.t <= 12));
  const gi = (s12.n - 1) % g.gates.length, gate = g.gates[gi];
  const gateS = path(g).gateArc(gi) + Math.floor((s12.n - 1) / g.gates.length) * path(g).L;
  for (const p of base) if (p.t <= 12) samples.push(p);
  for (let t = 12 + 1 / hz, k = 0; k < 3000; t += 1 / hz, k++) {
    const s = gateS + 45 * (t - 12 - 1 / hz); if (s > L * g.laps + 20) break;
    const q = k === 0 ? { x: gate[0], z: gate[2] } : path(g).at(s);
    samples.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z });
  }
  const rs = referee(g, samples);
  check(rs.bad === null && finishT(rs, g) !== null, `${id} a respawn to the last gate is accepted (bad=${rs.bad}, crossings=${rs.n})`);

  // Gates count the way the phone's trigger counts them (TrackSetup: gateHalfWidth to either side of the centreline).
  const { gateArc } = path(g), gs = gateArc(5);
  const swing = (lat: number) => (t: number, p: { x: number; z: number }) => { const s = 45 * t - 15, k = (s - gs) / 60;   // out and back over 120 m, lap 1
    return Math.abs(k) < 1 ? path(g).at(s, lat * Math.cos(k * Math.PI / 2) ** 2) : p; };
  const inside = referee(g, drive(g, 45, 15, swing(g.gateHalfWidth - 0.5)));
  check(inside.bad === null && finishT(inside, g) !== null, `${id} a run wide of gate 5 still inside its trigger counts it (${inside.bad}, ${inside.n})`);
  const outside = referee(g, drive(g, 45, 15, swing(g.gateHalfWidth + 4)));
  check(outside.bad === null && finishT(outside, g) === null, `${id} a run past gate 5's trigger does not count it, as the phone does not (${outside.bad}, ${outside.n})`);

  /** A drive planned as legs of lap distance and distance off the line, each at its own speed. */
  const plan = (legs: { to: number; lat?: number; v: number }[], hz = 15, s0 = -15, lat0 = 0) => {
    const out: Sample[] = []; let t = 0, s = s0, lat = lat0;
    for (const leg of legs) {
      const n = Math.max(1, Math.round(Math.abs(leg.to - s) / leg.v * hz)), ds = (leg.to - s) / n, dl = ((leg.lat ?? 0) - lat) / n;
      for (let i = 0; i < n; i++) { s += ds; lat += dl; t += 1 / hz; const q = path(g).at(s, lat); out.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    }
    return out;
  };
  // past gate 5 wide, the phone says "missed a checkpoint": back up and drive through it
  const W = g.gateHalfWidth + 6;
  const back = referee(g, plan([{ to: gs - 60, v: 45 }, { to: gs - 15, lat: W, v: 30 }, { to: gs + 30, lat: W, v: 30 }, { to: gs - 20, lat: 0, v: 12 }, { to: L * g.laps + 20, v: 45 }]));
  const tBack = back.cross[5], tPast = (gs - 60 + 15) / 45 + 90 / 30;
  check(back.bad === null && finishT(back, g) !== null && tBack > tPast,
    `${id} a gate missed wide and driven back through counts on the way back (${back.bad}, ${back.n}, gate 5 at ${tBack?.toFixed(1)} s)`);
  // or not noticed until the next lap: every gate after it waits, as on the phone, and the drive needs a lap more
  const lapLate = referee(g, plan([{ to: gs - 60, v: 45 }, { to: gs - 15, lat: W, v: 30 }, { to: gs + 20, lat: W, v: 30 }, { to: gs + 65, lat: 0, v: 30 }, { to: L * (g.laps + 1) + 20, v: 45 }]));
  check(lapLate.bad === null && finishT(lapLate, g) !== null && finishT(lapLate, g)! > (15 + L * (g.laps + 1)) / 45 - 1,
    `${id} a gate missed on lap 1 and taken on lap 2 finishes a lap later (${lapLate.bad}, ${lapLate.n})`);

  // a respawn one gate further back than the referee's last (the phone missed that one at a trigger's edge) is fine
  {
    const s12 = referee(g, base.filter(p => p.t <= 12)), k = g.gates.length;
    const gi2 = (s12.n - 2) % k, gate2 = g.gates[gi2];
    const gateS2 = gateArc(gi2) + Math.floor((s12.n - 2) / k) * L;
    const pts: Sample[] = base.filter(p => p.t <= 12);
    for (let t = 12 + 1 / 15, j = 0; j < 3000; t += 1 / 15, j++) {
      const s = gateS2 + 45 * (t - 12 - 1 / 15); if (s > L * g.laps + 20) break;
      const q = j === 0 ? { x: gate2[0], z: gate2[2] } : path(g).at(s);
      pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z });
    }
    const r2 = referee(g, pts);
    check(r2.bad === null && finishT(r2, g) !== null, `${id} a respawn to the gate before the last is accepted (bad=${r2.bad}, crossings=${r2.n})`);
  }

  // wedged 45 m out on open ground for the phone's 10 s, then respawned at the last gate: no progress made out there
  {
    const s12 = referee(g, base.filter(p => p.t <= 12)), k = g.gates.length;
    const gl = (s12.n - 1) % k, gate = g.gates[gl], gateS = gateArc(gl) + Math.floor((s12.n - 1) / k) * L;
    const s12pos = 45 * 12 - 15;
    const pts: Sample[] = base.filter(p => p.t <= 12);
    let t = 12;
    for (let j = 1; j <= 20; j++) { t += 1 / 15; const q = path(g).at(s12pos + j, 45 * j / 20); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    for (let j = 0; j < 150; j++) { t += 1 / 15; const q = path(g).at(s12pos + 20, 45); pts.push({ t: +t.toFixed(3), x: q.x + 0.01 * j, y: 0, z: q.z }); }
    t += 1 / 15; pts.push({ t: +t.toFixed(3), x: gate[0], y: 0, z: gate[2] });
    for (let s = gateS + 3; s < L * g.laps + 20; s += 3) { t += 3 / 45; const q = path(g).at(s); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    const w = referee(g, pts);
    check(w.bad === null && finishT(w, g) !== null, `${id} a kart wedged 45 m out for 10 s, then respawned, finishes (${w.bad})`);
  }
  // two ordinary run-offs past the corridor, one a lap, between two gates where the ground out there is open
  {
    const out = g.halfWidth + g.kerb + 25 + 5, { at } = path(g), k = g.gates.length;
    const smooth = (a: number, b: number) => { for (let s = a; s < b; s += 2) if (Math.hypot(at(s + 2, out).x - at(s, out).x, at(s + 2, out).z - at(s, out).z) > 3.2) return false; return true; };
    let spot: number[] | null = null;
    for (let i = 1; i < k - 1 && !spot; i++) { const a = gateArc(i) + 8, b = gateArc(i + 1) - 8; if (b - a > 60 && smooth(a, b)) spot = [(a + b) / 2, (b - a) / 2]; }
    if (!spot) console.log(`  (${id} has no open ground ${out.toFixed(0)} m out between two gates: no run-off to test)`);
    else {
      const [mid, half] = spot, v = 25;
      const off = (t: number, p: { x: number; z: number }) => { const s = v * t - 15;
        for (const c0 of [mid, L + mid]) { const u = (s - c0) / half; if (Math.abs(u) < 1) return at(s, out * Math.cos(u * Math.PI / 2) ** 2); }
        return p; };
      const r = referee(g, drive(g, v, 15, off));
      check(r.bad === null && finishT(r, g) !== null, `${id} two run-offs ${out.toFixed(0)} m out are not leaving the circuit (${r.bad}, ${r.offM.toFixed(0)} m beyond)`);
    }
  }
  // first seen well up the road, with no grid and no countdown: it skipped part of the lap unseen
  const skip = referee(g, drive(g, 45, 15).map(p => ({ ...p, t: p.t + 0.05 })).filter((_, i) => i > 0).map((p, i, a) => i === 0 ? { ...p, ...path(g).at(130) } : p));
  check(skip.bad === 'first seen ahead of the grid', `${id} a first state 130 m past the line is refused (${skip.bad})`);
  // the solo leaderboard: the phone records from GO with the kart in its grid slot, never a flying start
  check(referee(g, drive(g, 45, 5), true).bad === null, `${id} a solo path from the grid is accepted`);
  const flying = drive(g, 59.9, 5).map(p => ({ ...p, ...path(g).at(59.9 * p.t - 3) }));
  check(referee(g, flying, true).bad !== null, `${id} a solo path with a flying start is refused (${referee(g, flying, true).bad})`);

  // round two: honest drives the first fixes still refused
  {
    const withGrid = (v: number) => { const d = drive(g, v, 15), pre: Sample[] = [];
      for (let t = -3; t < 0; t += 1 / 15) pre.push({ t: +t.toFixed(3), x: d[0].x, y: 0, z: d[0].z });
      return [...pre, ...d]; };
    // the socket is down across GO: nothing from -1 s to +2 s. The kart was held until GO, so it did not cross early.
    const gapGo = referee(g, withGrid(40).filter(p => p.t < -1 || p.t > 2));
    check(gapGo.bad === null && finishT(gapGo, g) !== null && gapGo.cross[0] >= 0, `${id} a stream gap across GO is not a false start (${gapGo.bad}, start at ${gapGo.cross[0]?.toFixed(2)})`);
    // first state 3 s after GO from a fast kart: the start line credited then must not make lap 1 look too fast
    const late = referee(g, drive(g, 55, 15).filter(p => p.t >= 3));
    check(late.bad === null && finishT(late, g) !== null, `${id} a fast kart first seen 3 s after GO is fine (${late.bad})`);
    // a 14 s gap mid-race at 45 m/s (more than half a lap): the referee must not unwrap the kart a lap backwards
    const long = referee(g, drive(g, 45, 15).filter(p => p.t < 12 || p.t > 26));
    check(long.bad === null && finishT(long, g) !== null, `${id} a 14 s gap mid-race still finishes (${long.bad}, ${long.n} crossings)`);
    // lost out there: 25 s of circles 8 m past the corridor, then back to the road
    const out = g.halfWidth + g.kerb + 25 + 8, { at } = path(g), s0 = gateArc(3) + 20, pts: Sample[] = [];
    let t = 0;
    for (let sArc = -15; sArc < s0; sArc += 3) { t += 3 / 45; const q = at(sArc); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    for (let j = 1; j <= 15; j++) { t += 1 / 15; const q = at(s0 + j, out * j / 15); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    const c0 = at(s0 + 15, out);
    for (let j = 0; j < 25 * 15; j++) { t += 1 / 15; const ang = j / 15 * 12 / 8; pts.push({ t: +t.toFixed(3), x: c0.x + 8 * Math.cos(ang) - 8, y: 0, z: c0.z + 8 * Math.sin(ang) }); }
    for (let j = 1; j <= 15; j++) { t += 1 / 15; const q = at(s0 + 15 + j, out * (1 - j / 15)); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    for (let sArc = s0 + 30; sArc < L * g.laps + 20; sArc += 3) { t += 3 / 45; const q = at(sArc); pts.push({ t: +t.toFixed(3), x: q.x, y: 0, z: q.z }); }
    const circ = referee(g, pts);
    check(circ.bad === null && finishT(circ, g) !== null, `${id} 25 s of circling past the corridor is not leaving the circuit (${circ.bad}, ${circ.offM.toFixed(0)} m)`);
    // a gate passed 2.5 m past its trigger's edge (a drifting shop kart's corner still touches it): counted
    const edge = referee(g, drive(g, 45, 15, (tt, q) => { const sA = 45 * tt - 15, k2 = (sA - gs) / 60;
      return Math.abs(k2) < 1 ? path(g).at(sA, (g.gateHalfWidth + 2.5) * Math.cos(k2 * Math.PI / 2) ** 2) : q; }));
    check(edge.bad === null && finishT(edge, g) !== null, `${id} a gate passed 2.5 m past its trigger edge counts (${edge.bad}, ${edge.n})`);
    // a respawn three gates back (the phone missed one at an edge and has not been back through it)
    const base15 = drive(g, 45, 15), s20 = referee(g, base15.filter(p => p.t <= 20)), k3 = g.gates.length;
    const gi3 = (s20.n - 3) % k3, gate3 = g.gates[gi3], gateS3 = gateArc(gi3) + Math.floor((s20.n - 3) / k3) * L;
    const rs3: Sample[] = base15.filter(p => p.t <= 20);
    for (let tt = 20 + 1 / 15, j = 0; j < 3000; tt += 1 / 15, j++) {
      const sA = gateS3 + 45 * (tt - 20 - 1 / 15); if (sA > L * g.laps + 20) break;
      const q = j === 0 ? { x: gate3[0], z: gate3[2] } : path(g).at(sA);
      rs3.push({ t: +tt.toFixed(3), x: q.x, y: 0, z: q.z });
    }
    const r3 = referee(g, rs3);
    check(r3.bad === null && finishT(r3, g) !== null, `${id} a respawn three gates back is accepted (${r3.bad}, ${r3.n})`);

    // round three
    // a state claimed long before GO must not lend countdown time to a jump up the road at GO
    const q0 = at(-15), q1 = at(820);
    const warp = referee(g, [{ t: -13.9, x: q0.x, y: 0, z: q0.z }, { t: 0.07, x: q1.x, y: 0, z: q1.z }, ...drive(g, 45, 15).map(p2 => ({ ...p2, t: p2.t + 18.3 })).filter(p2 => p2.t > 0.1)]);
    check(warp.bad !== null && warp.bad.startsWith('moved'), `${id} a jump up the road at GO after an early grid state is a teleport (${warp.bad})`);
    // silent for longer than half a lap, then a state 60 m BEHIND the last: no free lap
    const sLast = 200, gap = (L - 45) / 89 + 1, pre = drive(g, 45, 15).filter(p2 => 45 * p2.t - 15 <= sLast);
    const t1 = pre[pre.length - 1].t + gap, post: Sample[] = [];
    for (let sA = sLast - 60, tt = t1; sA < L * (g.laps - 1) + 20; sA += 3, tt += 3 / 45) { const q = at(sA); post.push({ t: +tt.toFixed(3), x: q.x, y: 0, z: q.z }); }
    const gapCheat = referee(g, [...pre, ...post]);
    check(finishT(gapCheat, g) === null || gapCheat.bad !== null, `${id} a silent gap and a step back earn no lap (${gapCheat.bad}, ${gapCheat.n} crossings)`);
    // a respawn eight gates back (the phone missed a gate at a trigger's edge much earlier in the lap)
    const s28 = referee(g, base15.filter(p2 => p2.t <= 28)), gi8 = (s28.n - 8 + k3 * 4) % k3, gate8 = g.gates[gi8];
    const gateS8 = gateArc(gi8) + Math.floor((s28.n - 8) / k3) * L;
    const rs8: Sample[] = base15.filter(p2 => p2.t <= 28);
    for (let tt = 28 + 1 / 15, j = 0; j < 4000; tt += 1 / 15, j++) {
      const sA = gateS8 + 45 * (tt - 28 - 1 / 15); if (sA > L * g.laps + 20) break;
      const q = j === 0 ? { x: gate8[0], z: gate8[2] } : path(g).at(sA);
      rs8.push({ t: +tt.toFixed(3), x: q.x, y: 0, z: q.z });
    }
    const r8 = referee(g, rs8);
    check(r8.bad === null && finishT(r8, g) !== null, `${id} a respawn eight gates back is accepted (${r8.bad}, ${r8.n})`);
  }

  // network stalls: seconds with no samples, even through corners, must not cost an honest drive a gate
  for (const gap of [3, 8]) {
    const stalled = referee(g, drive(g, 45, 15).filter(p => p.t < 20 || p.t > 20 + gap));
    check(stalled.bad === null && finishT(stalled, g) !== null, `${id} an honest drive with a ${gap} s gap in its samples finishes (${stalled.bad}, ${stalled.n} crossings)`);
  }
  const sparse = referee(g, drive(g, 50, 5).filter((_, i) => i % 2 === 0));
  check(sparse.bad === null && finishT(sparse, g) !== null, `${id} a 2.5 Hz drive finishes (${sparse.bad})`);

  // a shortcut across the infield where the circuit doubles back close to itself: refused, even at a normal speed
  {
    const { at: pos } = path(g), c = g.centre, cum = [0];
    for (let i = 1; i <= c.length; i++) cum.push(cum[i - 1] + Math.hypot(c[i % c.length][0] - c[i - 1][0], c[i % c.length][1] - c[i - 1][1]));
    let pair: number[] | null = null;
    for (let i = 0; i < c.length && !pair; i += 2) for (let j = i + 1; j < c.length; j++) {
      const arc = cum[j] - cum[i], gap = Math.hypot(c[j][0] - c[i][0], c[j][1] - c[i][1]);
      if (arc > 150 && arc < 400 && gap < 45 && cum[i] > 60 && cum[j] < L - 60) { pair = [cum[i], cum[j], gap]; break; }
    }
    if (!pair) console.log(`  (no place where ${id} doubles back within 45 m: no shortcut to test)`);
    else {
      const [sa, sb] = pair, v = 45, samples: Sample[] = [];
      let t = 0;
      for (let s0 = -15; s0 < sa; s0 += v / 15, t += 1 / 15) { const q = pos(s0); samples.push({ t, x: q.x, y: 0, z: q.z }); }
      const A = pos(sa), B = pos(sb), cut = Math.hypot(B.x - A.x, B.z - A.z);
      for (let k = 1; k * v / 15 < cut; k++, t += 1 / 15) { const u = k * v / 15 / cut; samples.push({ t, x: A.x + (B.x - A.x) * u, y: 0, z: A.z + (B.z - A.z) * u }); }
      for (let s0 = sb; s0 < L * g.laps + 20; s0 += v / 15, t += 1 / 15) { const q = pos(s0); samples.push({ t, x: q.x, y: 0, z: q.z }); }
      const r = referee(g, samples);
      check(r.bad !== null, `${id} a ${cut.toFixed(0)} m cut across the infield skipping ${(sb - sa).toFixed(0)} m of circuit is refused (${r.bad})`);
    }
  }

  // across the infield: gate to gate in straight lines. What a perfect bot saves at the speed cap vs the centreline at the kart's top speed
  const G = g.gates, chord: Sample[] = [];
  let t = 0, prev = { x: G[0][0] - 15 * G[0][3], z: G[0][2] - 15 * G[0][4] };
  chord.push({ t, x: prev.x, y: 0, z: prev.z });
  for (let k = 0; k <= G.length * g.laps; k++) {
    const q = G[k % G.length], tgt = { x: q[0] + q[3] * 0.5, z: q[2] + q[4] * 0.5 };
    const d = Math.hypot(tgt.x - prev.x, tgt.z - prev.z), steps = Math.max(1, Math.ceil(d / (V_SUSTAINED * 0.99 / 15)));
    for (let j = 1; j <= steps; j++) { t += d / steps / (V_SUSTAINED * 0.99); chord.push({ t, x: prev.x + (tgt.x - prev.x) * j / steps, y: 0, z: prev.z + (tgt.z - prev.z) * j / steps }); }
    prev = tgt;
  }
  const cs = referee(g, chord), honestBest = (15 + L * g.laps) / 56.2;
  console.log(`  chords at the cap: ${cs.bad ?? `finishes in ${finishT(cs, g)?.toFixed(1)} s`}; the centreline at the kart's top speed takes ${honestBest.toFixed(1)} s`);
  check(cs.bad !== null || finishT(cs, g) === null, `${id} gate-to-gate chords at the speed cap are refused`);
}

console.log(failures ? `\n${failures} failure(s)` : '\nall referee checks passed');
if (failures) process.exit(1);
