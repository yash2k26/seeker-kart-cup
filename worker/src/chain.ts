// Solana reads: RPC proxy, SGT check, .skr resolution, tx verification. Addresses from docs.solanamobile.com (re-verified 13 Sep 2026).
import type { Env } from './index';
import { json, bad, now } from './index';

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

// The APK never holds an RPC key: it talks to /rpc, which forwards an allow-list of methods.
export async function rpcProxy(req: Request, env: Env) {
  const body = await req.json<any>().catch(() => null);
  const calls = Array.isArray(body) ? body : [body];
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
async function claimSignature(env: Env, signature: string, wallet: string, kind: string) {
  const r = await env.DB.prepare("INSERT OR IGNORE INTO tx_log (signature, wallet, kind, status, created_at) VALUES (?1, ?2, ?3, 'pending', ?4)")
    .bind(signature, wallet, kind, now()).run();
  return r.meta.changes > 0;
}
async function fetchTx(env: Env, signature: string) {
  return rpc(env, 'getTransaction', [signature, { encoding: 'jsonParsed', commitment: 'confirmed', maxSupportedTransactionVersion: 0 }]);
}
function instructions(tx: any): any[] {
  return [...(tx.transaction.message.instructions ?? []), ...(tx.meta?.innerInstructions ?? []).flatMap((i: any) => i.instructions)];
}
const signedBy = (tx: any, wallet: string) => tx.transaction.message.accountKeys.some((k: any) => k.pubkey === wallet && k.signer);

/** POST /v1/tx/confirm { signature, raceId } — memo "kc:pb:<raceId>" signed by the session wallet → results.memo_sig */
export async function txConfirm(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { signature, raceId } = await req.json<any>().catch(() => ({}));
  if (!signature || !raceId) return bad('signature and raceId required');
  if (!(await claimSignature(env, signature, wallet, 'memo_pb'))) return bad('already processed', 409);
  const tx = await fetchTx(env, signature);
  if (!tx) { await env.DB.prepare("DELETE FROM tx_log WHERE signature = ?1").bind(signature).run(); return json({ pending: true }, 202); }
  const memo = instructions(tx).find((i: any) => i.program === 'spl-memo')?.parsed;
  const ok = !tx.meta?.err && signedBy(tx, wallet) && memo === `kc:pb:${raceId}`;
  await env.DB.prepare('UPDATE tx_log SET status = ?2, slot = ?3 WHERE signature = ?1').bind(signature, ok ? 'confirmed' : 'rejected', tx.slot).run();
  if (!ok) return bad('validation failed');
  await env.DB.prepare('UPDATE results SET memo_sig = ?1 WHERE race_id = ?2 AND wallet = ?3').bind(signature, raceId, wallet).run();
  return json({ confirmed: true, slot: tx.slot });
}

/** POST /v1/purchase/verify { signature, sku, orderId } — SKR transferChecked to the treasury ATA + memo "kc:buy:<sku>:<orderId>" */
export async function purchaseVerify(req: Request, env: Env, _ctx: ExecutionContext, wallet: string | null) {
  if (!wallet) return bad('unauthorized', 401);
  const { signature, sku, orderId } = await req.json<any>().catch(() => ({}));
  const prices: Record<string, string> = JSON.parse(env.PRICES_JSON || '{}');
  if (!signature || !sku || !orderId || !prices[sku]) return bad('bad request');
  if (!(await claimSignature(env, signature, wallet, 'purchase'))) return bad('already processed', 409);
  const tx = await fetchTx(env, signature);
  if (!tx) { await env.DB.prepare("DELETE FROM tx_log WHERE signature = ?1").bind(signature).run(); return json({ pending: true }, 202); }
  const ixs = instructions(tx);
  const memo = ixs.find((i: any) => i.program === 'spl-memo')?.parsed;
  const xfer = ixs.find((i: any) => i.program === 'spl-token' && i.parsed?.type === 'transferChecked');
  const amount = BigInt(xfer?.parsed?.info?.tokenAmount?.amount ?? 0);
  const ok = !tx.meta?.err && signedBy(tx, wallet) && memo === `kc:buy:${sku}:${orderId}` &&
    xfer?.parsed?.info?.mint === env.SKR_MINT && xfer?.parsed?.info?.destination === env.TREASURY_ATA && amount >= BigInt(prices[sku]);
  await env.DB.prepare('UPDATE tx_log SET status = ?2, slot = ?3 WHERE signature = ?1').bind(signature, ok ? 'confirmed' : 'rejected', tx.slot).run();
  if (!ok) return bad('validation failed');
  await env.DB.prepare("INSERT OR IGNORE INTO unlocks (wallet, sku, source, signature, created_at) VALUES (?1, ?2, 'purchase', ?3, ?4)")
    .bind(wallet, sku, signature, now()).run();
  return json({ granted: sku });
}
