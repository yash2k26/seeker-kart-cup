// Solana reads: RPC proxy, SGT check, .skr resolution, tx verification. Addresses from docs.solanamobile.com (re-verified 13 Sep 2026).
import type { Env } from './index';
import { json, bad, now } from './index';
import { priceOf, grantsOf, type Currency } from './catalog';
import { rates } from './prices';
import { verifyPayment } from './payout';
import { isSignature } from './auth';

const TOKEN_2022 = 'TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb';
const SGT_MINT_AUTHORITY = 'GT2zuHVaZQYZSyQMgJPLzvkmyztfyXg2NJunqFp4p3A4';
const SGT_GROUP = 'GT22s89nU4iWFkNXj1Bw6uYhJJWDRPpShHt4Bk8f99Te';

const RPC_ALLOW = new Set([
  'getLatestBlockhash', 'sendTransaction', 'getSignatureStatuses', 'getBalance',
  'getTokenAccountsByOwner', 'getAccountInfo', 'getMinimumBalanceForRentExemption', 'getTransaction',
]);

export async function rpc(env: Env, method: string, params: unknown[]) {
  const r = await fetch(env.HELIUS_URL, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }),
  });
  const j = await r.json<any>();
  if (j.error) throw new Error(`${method}: ${j.error.message}`);
  return j.result;
}

// The APK never holds an RPC key: it talks to /rpc, which forwards an allow-list of methods, a few at a time.
const RPC_MAX_CALLS = 8, RPC_MAX_BYTES = 64 * 1024;
export async function rpcProxy(req: Request, env: Env) {
  const text = await req.text();
  if (text.length > RPC_MAX_BYTES) return bad('request too large', 413);
  let body: any = null; try { body = JSON.parse(text); } catch { /* bad json */ }
  const calls = Array.isArray(body) ? body : [body];
  if (calls.length > RPC_MAX_CALLS) return bad('too many calls in one request', 413);
  if (!calls.every(c => c && RPC_ALLOW.has(c.method))) return bad('method not allowed', 403);
  const r = await fetch(env.HELIUS_URL, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
  return new Response(r.body, { status: r.status, headers: { 'content-type': 'application/json' } });
}

/** Returns the SGT mint address the wallet holds, or null. Two plain RPC calls, no packages. */
export async function checkWalletForSGT(env: Env, wallet: string): Promise<string | null> {
  const accs = await rpc(env, 'getTokenAccountsByOwner', [wallet, { programId: TOKEN_2022 }, { encoding: 'jsonParsed' }]);
  const mints: string[] = (accs?.value ?? [])
    .map((a: any) => a.account.data.parsed.info)
    .filter((i: any) => i?.mint && i.tokenAmount?.amount !== '0')
    .map((i: any) => i.mint);
  if (!mints.length) return null;
  const infos = await rpc(env, 'getMultipleAccounts', [mints.slice(0, 100), { encoding: 'jsonParsed' }]);
  for (let i = 0; i < mints.length; i++) {
    const info = infos?.value?.[i]?.data?.parsed?.info;
    if (!info || info.mintAuthority !== SGT_MINT_AUTHORITY) continue;
    const ext = info.extensions ?? [];
    const mp = ext.find((e: any) => e.extension === 'metadataPointer');
    const gm = ext.find((e: any) => e.extension === 'tokenGroupMember');
    if (mp?.state?.metadataAddress === SGT_GROUP && gm?.state?.group === SGT_GROUP) return mints[i];
  }
  return null;
}

/** .skr reverse lookup via AllDomains' parser (the package Solana Mobile's own sample uses). Cached in D1 by the caller. */
export async function resolveSkr(env: Env, wallet: string): Promise<string | null> {
  try {
    const [{ Connection, PublicKey }, { TldParser }] = await Promise.all([import('@solana/web3.js'), import('@onsol/tldparser')]);
    const parser = new TldParser(new Connection(env.HELIUS_URL));
    const pk = new PublicKey(wallet);
    try {
      const main: any = await parser.getMainDomain(pk);
      if (main?.domain && main.tld === '.skr') return `${main.domain}.skr`;
    } catch { /* no main domain */ }
    const domains: any[] = await parser.getParsedAllUserDomainsFromTld(pk, 'skr');
    return domains?.length ? `${domains[0].domain}.skr` : null;
  } catch (e) { console.warn('skr lookup failed', e); return null; }
}

export async function refreshProfile(env: Env, wallet: string) {
  const [sgt, skr] = await Promise.all([checkWalletForSGT(env, wallet).catch(() => null), resolveSkr(env, wallet)]);
  // SGT mint is UNIQUE: a transferred token cannot claim twice from another wallet.
  const taken = sgt ? await env.DB.prepare('SELECT wallet FROM players WHERE sgt_mint = ?1 AND wallet != ?2').bind(sgt, wallet).first() : null;
  const isSeeker = !!sgt && !taken;
  await env.DB.batch([
    env.DB.prepare('UPDATE players SET skr_name = ?2, skr_checked = ?3, is_seeker = ?4, sgt_mint = ?5 WHERE wallet = ?1')
      .bind(wallet, skr, now(), isSeeker ? 1 : 0, isSeeker ? sgt : null),
    ...(isSeeker ? [
      env.DB.prepare("INSERT OR IGNORE INTO unlocks (wallet, sku, source, created_at) VALUES (?1, 'kart.seeker_edition', 'sgt', ?2)").bind(wallet, now()),
      env.DB.prepare("INSERT OR IGNORE INTO unlocks (wallet, sku, source, created_at) VALUES (?1, 'badge.seeker', 'sgt', ?2)").bind(wallet, now()),
    ] : []),
  ]);
}

export async function getProfile(env: Env, wallet: string) {
  const p = await env.DB.prepare('SELECT wallet, skr_name, is_seeker FROM players WHERE wallet = ?1').bind(wallet).first<any>();
  const unlocks = await env.DB.prepare('SELECT sku FROM unlocks WHERE wallet = ?1').bind(wallet).all<{ sku: string }>();
  const skus = unlocks.results.map(u => u.sku);
  return { wallet, skrName: p?.skr_name ?? null, isSeeker: !!p?.is_seeker || skus.includes('badge.seeker'), unlocks: skus };   // badge via SGT or demo code
}

// ---- landed-transaction verification (replay-proof via tx_log PRIMARY KEY) ----
// Claim a signature only AFTER the transaction checked out for this wallet: a claim made first could be made by
// anyone who read the signature on-chain, and it would lock the rightful payer out.
/** Claim a payment signature for `wallet` and `kind`. Claiming it again for the same wallet and kind is the same claim
 *  (a retry after a failure part-way must not find its own claim and call the payment used); what a claim buys is
 *  single-use elsewhere (room_entries.signature UNIQUE, unlocks and memo rows keyed so a repeat changes nothing). */
export async function claimSignature(env: Env, signature: string, wallet: string, kind: string) {
  const r = await env.DB.prepare("INSERT OR IGNORE INTO tx_log (signature, wallet, kind, status, created_at) VALUES (?1, ?2, ?3, 'pending', ?4)")
    .bind(signature, wallet, kind, now()).run();
  if (r.meta.changes > 0) return true;
  const row = await env.DB.prepare('SELECT wallet, kind FROM tx_log WHERE signature = ?1').bind(signature).first<any>();
  return !!row && row.wallet === wallet && row.kind === kind;
}
/** Claimed by another wallet or for another purpose: answered from D1 before any RPC is spent on it. */
export async function usedByOther(env: Env, signature: string, wallet: string, kind: string) {
  const row = await env.DB.prepare('SELECT wallet, kind FROM tx_log WHERE signature = ?1').bind(signature).first<any>();
  return !!row && (row.wallet !== wallet || row.kind !== kind);
}
export async function fetchTx(env: Env, signature: string) {
  if (env.MOCK_CHAIN === '1') return null;          // the local smoke test: nothing leaves this machine
  return rpc(env, 'getTransaction', [signature, { encoding: 'jsonParsed', commitment: 'confirmed', maxSupportedTransactionVersion: 0 }]);
}
export function instructions(tx: any): any[] {
  return [...(tx.transaction.message.instructions ?? []), ...(tx.meta?.innerInstructions ?? []).flatMap((i: any) => i.instructions)];
}
export const signedBy = (tx: any, wallet: string) => tx.transaction.message.accountKeys.some((k: any) => k.pubkey === wallet && k.signer);

/** POST /v1/tx/confirm { signature, raceId } — memo "kc:pb:<raceId>" signed by the session wallet → results.memo_sig */
export async function txConfirm(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { signature, raceId } = await req.json<any>().catch(() => ({}));
  if (typeof signature !== 'string' || typeof raceId !== 'string' || signature.length > 128 || raceId.length > 64) return bad('signature and raceId required');
  if (!isSignature(signature) && env.MOCK_CHAIN !== '1') return bad('not a transaction signature');
  if (await usedByOther(env, signature, wallet, 'memo_pb')) return bad('already processed', 409);   // before the RPC: a replay costs nothing upstream
  const tx = await fetchTx(env, signature);
  if (!tx) return json({ pending: true }, 202);
  const memo = instructions(tx).find((i: any) => i.program === 'spl-memo')?.parsed;
  const ok = !tx.meta?.err && signedBy(tx, wallet) && memo === `kc:pb:${raceId}`;
  if (!ok) return bad('validation failed');
  if (!(await claimSignature(env, signature, wallet, 'memo_pb'))) return bad('already processed', 409);
  await env.DB.prepare("UPDATE tx_log SET status = 'confirmed', slot = ?2 WHERE signature = ?1").bind(signature, tx.slot).run();
  await env.DB.prepare('UPDATE results SET memo_sig = ?1 WHERE race_id = ?2 AND wallet = ?3').bind(signature, raceId, wallet).run();
  return json({ confirmed: true, slot: tx.slot });
}

/** POST /v1/purchase/verify { signature, sku, orderId, currency? } — a transfer of the catalog price to the treasury
 *  (SOL to the owner, SKR to its ATA) plus memo "kc:buy:<sku>:<orderId>". Prices live in catalog.ts (+ PRICES_JSON). */
export async function purchaseVerify(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { signature, sku, orderId, currency } = await req.json<any>().catch(() => ({}));
  const cur: Currency = currency === 'SOL' ? 'SOL' : 'SKR';
  const price = sku ? priceOf(String(sku), cur, await rates(env), env.PRICES_JSON) : null;
  if (typeof signature !== 'string' || signature.length > 128 || !sku || !orderId || price === null) return bad('bad request');
  // The quote the player saw may be minutes old and the token moved: accept 90% of today's price (research/23 §F.1).
  const floor = (price * 9n) / 10n;
  if (String(orderId).length > 64 || String(sku).length > 64) return bad('bad request');
  if (await usedByOther(env, signature, wallet, 'purchase')) return bad('signature already used');
  const r = await verifyPayment(env, signature, wallet, `kc:buy:${sku}:${orderId}`, floor, cur, 'purchase');
  if (!r.ok) return r.pending ? json({ pending: true }, 202) : bad(r.why);
  const granted = grantsOf(String(sku));
  await env.DB.batch(granted.map(g => env.DB.prepare("INSERT OR IGNORE INTO unlocks (wallet, sku, source, signature, created_at) VALUES (?1, ?2, 'purchase', ?3, ?4)")
    .bind(wallet, g, signature, now())));
  return json({ granted });
}
