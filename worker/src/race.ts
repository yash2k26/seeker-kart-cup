// Race sessions, results, leaderboard, profile, events, deletion. Guest mode: a race never needs a wallet.
import type { Env } from './index';
import { json, bad, now } from './index';
import { signHmac, readHmac } from './auth';
import { getProfile } from './chain';

// TODO week 1: measure Level_01 and set these from your own best lap (minLapMs = best × 0.9).
export const TRACKS: Record<string, { laps: number; checkpoints: number; minLapMs: number; minSegmentMs: number }> = {
  beach: { laps: 2, checkpoints: 8, minLapMs: 30_000, minSegmentMs: 2_000 },
};

type RaceToken = { r: string; d: string; t: string; s: number };

/** POST /v1/race/start { deviceId, trackId } → { raceId, raceToken } — no session needed (guest mode). */
export async function raceStart(req: Request, env: Env) {
  const { deviceId, trackId } = await req.json<any>().catch(() => ({}));
  if (!deviceId || !TRACKS[trackId]) return bad('deviceId and known trackId required');
  const raceId = crypto.randomUUID();
  const startedMs = Date.now();
  await env.DB.prepare('INSERT INTO race_sessions (race_id, device_id, track_id, started_at) VALUES (?1, ?2, ?3, ?4)')
    .bind(raceId, deviceId, trackId, Math.floor(startedMs / 1000)).run();
  return json({ raceId, raceToken: await signHmac(env, { r: raceId, d: deviceId, t: trackId, s: startedMs } satisfies RaceToken) });
}

function validate(t: typeof TRACKS[string], b: any, issuedAtMs: number): string | null {
  const wall = Date.now() - issuedAtMs;
  if (!Array.isArray(b.lapMs) || !Array.isArray(b.checkpointMs) || typeof b.totalMs !== 'number') return 'shape';
  if (wall < b.totalMs * 0.9) return 'finished faster than wall clock';
  if (wall > b.totalMs + 15 * 60_000) return 'session too old';
  if (b.lapMs.length !== t.laps) return 'lap count';
  if (b.lapMs.some((l: number) => l < t.minLapMs)) return 'lap under floor';
  if (Math.abs(b.lapMs.reduce((a: number, x: number) => a + x, 0) - b.totalMs) > 1500) return 'laps ≠ total';
  if (b.checkpointMs.length !== t.checkpoints * t.laps) return 'checkpoint count';
  if (b.checkpointMs.some((c: number, i: number, a: number[]) => i && c <= a[i - 1])) return 'checkpoints not monotonic';
  const gaps = b.checkpointMs.map((c: number, i: number, a: number[]) => (i ? c - a[i - 1] : c));
  if (gaps.some((g: number) => g < t.minSegmentMs)) return 'segment under floor';
  return null;
}

/** POST /v1/race/finish { raceToken, totalMs, lapMs[], checkpointMs[], position } → { accepted, isPersonalBest, raceId } */
export async function raceFinish(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  const b = await req.json<any>().catch(() => ({}));
  const tok = await readHmac<RaceToken>(env, b.raceToken);
  if (!tok) return bad('bad race token', 401);
  const t = TRACKS[tok.t];
  const why = validate(t, b, tok.s);
  if (why) return bad(`rejected: ${why}`, 422);

  const session = await env.DB.prepare('SELECT wallet, finished_at FROM race_sessions WHERE race_id = ?1').bind(tok.r).first<any>();
  if (!session) return bad('unknown race', 404);
  if (session.finished_at) return bad('already finished', 409);
  const owner: string | null = session.wallet ?? wallet;
  const bestLap = Math.min(...b.lapMs);

  let isPb = false;
  if (owner) {
    const prev = await env.DB.prepare('SELECT MIN(total_ms) AS best FROM results WHERE wallet = ?1 AND track_id = ?2').bind(owner, tok.t).first<any>();
    isPb = !prev?.best || b.totalMs < prev.best;
  }
  await env.DB.batch([
    env.DB.prepare('UPDATE race_sessions SET finished_at = ?2, wallet = COALESCE(wallet, ?3) WHERE race_id = ?1').bind(tok.r, now(), owner),
    env.DB.prepare('INSERT INTO results (race_id, device_id, wallet, track_id, total_ms, best_lap_ms, laps_json, position, is_pb, created_at) VALUES (?1,?2,?3,?4,?5,?6,?7,?8,?9,?10)')
      .bind(tok.r, tok.d, owner, tok.t, b.totalMs, bestLap, JSON.stringify(b.lapMs), b.position ?? null, isPb ? 1 : 0, now()),
  ]);
  return json({ accepted: true, isPersonalBest: isPb, raceId: tok.r, memo: isPb ? `kc:pb:${tok.r}` : null });
}

/** POST /v1/race/claim { raceToken } (session) — binds a guest result to the wallet after Sign-In. */
export async function raceClaim(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { raceToken } = await req.json<any>().catch(() => ({}));
  const tok = await readHmac<RaceToken>(env, raceToken);
  if (!tok) return bad('bad race token', 401);
  await env.DB.batch([
    env.DB.prepare('UPDATE race_sessions SET wallet = ?2 WHERE race_id = ?1 AND wallet IS NULL').bind(tok.r, wallet),
    env.DB.prepare('UPDATE results SET wallet = ?2 WHERE race_id = ?1 AND wallet IS NULL').bind(tok.r, wallet),
  ]);
  const prev = await env.DB.prepare('SELECT total_ms FROM results WHERE race_id = ?1').bind(tok.r).first<any>();
  const best = await env.DB.prepare('SELECT MIN(total_ms) AS best FROM results WHERE wallet = ?1 AND track_id = ?2 AND race_id != ?3').bind(wallet, tok.t, tok.r).first<any>();
  const isPb = !!prev && (!best?.best || prev.total_ms < best.best);
  if (isPb) await env.DB.prepare('UPDATE results SET is_pb = 1 WHERE race_id = ?1').bind(tok.r).run();
  return json({ claimed: true, isPersonalBest: isPb, memo: isPb ? `kc:pb:${tok.r}` : null });
}

/** GET /v1/leaderboard?track=beach&around=<wallet> */
export async function leaderboard(req: Request, env: Env) {
  const url = new URL(req.url);
  const track = url.searchParams.get('track') ?? 'beach';
  if (!TRACKS[track]) return bad('unknown track');
  const rows = await env.DB.prepare(`
    SELECT r.wallet, COALESCE(p.skr_name, substr(r.wallet,1,4) || '…' || substr(r.wallet,-4)) AS name,
           p.is_seeker AS isSeeker, MIN(r.total_ms) AS bestMs, r.memo_sig AS memoSig
    FROM results r JOIN players p USING (wallet)
    WHERE r.track_id = ?1 AND r.wallet IS NOT NULL
    GROUP BY r.wallet ORDER BY bestMs LIMIT 50`).bind(track).all();
  return json({ track, entries: rows.results.map((e: any, i) => ({ rank: i + 1, ...e, isSeeker: !!e.isSeeker })) },
  );
}

/** GET /v1/me (session) */
export async function me(_req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  return json(await getProfile(env, wallet));
}

/** DELETE /v1/me (session) — Publisher Policy: users must be able to delete their account and data. */
export async function deleteMe(_req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  await env.DB.batch([
    env.DB.prepare('DELETE FROM results WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM race_sessions WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM unlocks WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM tx_log WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM pending_tx WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM events WHERE wallet = ?1').bind(wallet),
    env.DB.prepare('DELETE FROM players WHERE wallet = ?1').bind(wallet),
  ]);
  return json({ deleted: true });
}

/** POST /v1/event { deviceId, name, props } — races, connects, returning devices, last exception. */
export async function event(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  const { deviceId, name, props } = await req.json<any>().catch(() => ({}));
  if (!deviceId || !name || String(name).length > 64) return bad('bad event');
  await env.DB.prepare('INSERT INTO events (device_id, wallet, name, props, created_at) VALUES (?1, ?2, ?3, ?4, ?5)')
    .bind(deviceId, wallet, name, props ? JSON.stringify(props).slice(0, 4000) : null, now()).run();
  return json({ ok: true });
}
