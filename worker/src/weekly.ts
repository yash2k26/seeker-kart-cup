// Kartel Weekly: one featured circuit a week (Monday 00:00 UTC to the next Monday), driven as a time trial (the same
// kart for everyone, no items, nobody else on track: RaceSpawner), ranked by best lap. Free to enter, decided by skill
// (the referee's verified laps), the fixed SKR prizes paid by hand from the treasury after the week closes.
// Everyone is on the board; a prize place goes only to a wallet whose Seeker Genesis Token the Worker verified
// (players.is_seeker, not the demo-code badge; one wallet per SGT is the players table's unique index) and whose best
// run was not driven from India, so a second wallet on the same phone or a laptop bot takes nothing: its place passes
// to the next Seeker down. Prizes are paid only when at least MIN_SEEKERS Seekers drove that week.
import type { Env } from './index';
import { json, now } from './index';

const WEEK = 7 * 86400;
const MONDAY0 = Date.UTC(1970, 0, 5) / 1000;                       // a Monday, 00:00 UTC
const ORDER = ['beach', 'alpine', 'forest', 'speedway', 'volcano']; // Tracks.cs order: Palm Bay, Frost Peak, Pine Ridge, Sunset Speedway, Mount Ember
const FIRST = Math.floor((Date.UTC(2026, 9, 5) / 1000 - MONDAY0) / WEEK);   // the week of 5 Oct 2026 opens on Palm Bay
export const SPLIT = [50, 30, 20];                                   // % of the pot for places 1-3
export const MIN_SEEKERS = 3;

/** The week holding unix time t: its number, bounds and circuit. */
export function weekOf(t: number) {
  const n = Math.floor((t - MONDAY0) / WEEK);
  const start = MONDAY0 + n * WEEK;
  return { n, start, end: start + WEEK, track: ORDER[(((n - FIRST) % ORDER.length) + ORDER.length) % ORDER.length] };
}

/** Whole SKR in the pot (a Worker secret, so it can change without a deploy); null while unset. */
const potOf = (env: Env) => {
  const v = Number((env as Env & { WEEKLY_POT_SKR?: string }).WEEKLY_POT_SKR);
  return Number.isFinite(v) && v > 0 ? Math.floor(v) : null;
};

type Week = ReturnType<typeof weekOf>;
type Row = { rank: number; wallet: string; name: string; isSeeker: boolean; bestMs: number; prizePlace: number | null; prize: number | null };

/** The week's whole board on its circuit (best lap per wallet), the prize places marked. */
async function standings(env: Env, w: Week, pot: number | null): Promise<{ rows: Row[]; seekers: number }> {
  const all = (await env.DB.prepare(`
    SELECT r.wallet, COALESCE(p.skr_name, substr(r.wallet,1,4) || '…' || substr(r.wallet,-4)) AS name,
           p.is_seeker AS sgt, p.sgt_mint AS sgtMint,
           (p.is_seeker OR EXISTS (SELECT 1 FROM unlocks u WHERE u.wallet = r.wallet AND u.sku = 'badge.seeker')) AS isSeeker,
           MIN(r.best_lap_ms) AS bestMs, r.created_at AS setAt, r.country AS country   -- (SQLite: bare columns come from the MIN row)
    FROM results r JOIN players p USING (wallet)
    WHERE r.mode = 'weekly' AND r.track_id = ?1 AND r.wallet IS NOT NULL AND r.created_at >= ?2 AND r.created_at < ?3
    GROUP BY r.wallet ORDER BY bestMs, setAt`).bind(w.track, w.start, w.end).all()).results as any[];
  const eligible = (e: any) => !!(e.sgt && e.sgtMint && e.country !== 'IN');
  const seekers = all.filter(eligible).length;
  let place = 0;
  const rows = all.map((e, i) => {
    let prize: number | null = null, prizePlace: number | null = null;
    if (place < SPLIT.length && eligible(e)) {
      prizePlace = ++place;
      prize = pot == null || seekers < MIN_SEEKERS ? null : Math.floor((pot * SPLIT[place - 1]) / 100);
    }
    return { rank: i + 1, wallet: e.wallet, name: e.name, isSeeker: !!e.isSeeker, bestMs: e.bestMs, prizePlace, prize };
  });
  return { rows, seekers };
}

/** wallet's row and the gap to the row above it (null when it has not driven) */
function mine(rows: Row[], wallet: string | null) {
  const i = wallet ? rows.findIndex((e) => e.wallet === wallet) : -1;
  return i < 0 ? null : { ...rows[i], gapMs: i > 0 ? rows[i].bestMs - rows[i - 1].bestMs : 0, entrants: rows.length };
}

/** GET /v1/weekly[?wallet=] — this week (circuit, bounds, pot, top 50, your row) and last week's prize places and your
 *  row there (the Monday claim). ponytail: last week's amounts use today's pot; keep a pot per week if it changes. */
export async function weekly(req: Request, env: Env) {
  const t = now(), w = weekOf(t), last = weekOf(t - WEEK), pot = potOf(env);
  const wallet = new URL(req.url).searchParams.get('wallet');
  const [cur, prev] = await Promise.all([standings(env, w, pot), standings(env, last, pot)]);
  return json({
    week: w.n, track: w.track, startsAt: w.start, endsAt: w.end, now: t, nextTrack: weekOf(w.end).track,
    pot, currency: 'SKR', split: SPLIT, minSeekers: MIN_SEEKERS, seekers: cur.seekers, entrants: cur.rows.length,
    entries: cur.rows.slice(0, 50), me: mine(cur.rows, wallet),
    last: { week: last.n, track: last.track, seekers: prev.seekers, winners: prev.rows.filter((e) => e.prizePlace != null), me: mine(prev.rows, wallet) },
  });
}
