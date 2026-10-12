// One JSON-RPC call to the Solana node behind HELIUS_URL. On its own, so modules that only read the chain (escrow.ts)
// do not pull in the Worker's routes.
import type { Env } from './index';

// 12 Oct 2026: the node behind HELIUS_URL (a free public one) answered a wallet's getLatestBlockhash with -32005 "rate
// limit exceeded", the shop built a transaction with no blockhash, and the wallet opened with nothing to sign. A free
// node limits the shared addresses Workers call from, so a refusal now moves on to the next node. RPC_FALLBACKS="none"
// turns that off (scripts/stress.mjs runs on its own fake node). The real fix is a keyed RPC in HELIUS_URL.
const FALLBACKS = ['https://api.mainnet-beta.solana.com', 'https://solana-rpc.publicnode.com'];
// api.mainnet-beta answers Cloudflare with 403 "Your IP or provider is blocked": a refusal too, never an answer to pass on
const refused = (status: number, text: string) => status === 429 || status === 403 || status >= 500 || /"code":\s*(-32005|403|429)\b|rate limit|blocked/i.test(text);
const pause = (ms: number) => new Promise(r => setTimeout(r, ms));

/** POSTs a JSON-RPC body (one call or a batch) to the first node that does not refuse it. */
export async function rpcPost(env: Env, body: string): Promise<{ status: number; text: string }> {
  const urls = [env.HELIUS_URL, ...(env.RPC_FALLBACKS === 'none' ? [] : FALLBACKS.filter(u => u !== env.HELIUS_URL))];
  let last = { status: 502, text: '{"jsonrpc":"2.0","error":{"code":-32000,"message":"no Solana node answered"},"id":null}' };
  // the first node three times (its limit is per moment: a short wait usually gets through), the others once
  for (const [i, url] of urls.entries()) for (let attempt = 0; attempt < (i === 0 ? 3 : 1); attempt++) {
    if (attempt > 0) await pause(250 * attempt);
    try {
      const r = await fetch(url, { method: 'POST', headers: { 'content-type': 'application/json' }, body });
      last = { status: r.status, text: await r.text() };
      if (!refused(last.status, last.text)) return last;
      console.warn('rpc: refused by', new URL(url).host, last.status, last.text.slice(0, 120));
    } catch (e: any) { console.warn('rpc: no answer from', new URL(url).host, e?.message ?? e); }
  }
  return last;
}

export async function rpc(env: Env, method: string, params: unknown[]) {
  const r = await rpcPost(env, JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }));
  const j = JSON.parse(r.text);
  if (j.error) throw new Error(`${method}: ${j.error.message}`);
  return j.result;
}

/** Hands a signed transaction to the network. 'rejected': the node simulated it and refused it, so it was never
 *  broadcast and cannot land. A throw says nothing either way (it may have gone out): only txStatus decides that. */
export async function sendRaw(env: Env, raw: Uint8Array): Promise<'sent' | { rejected: string }> {
  const r = await rpcPost(env, JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'sendTransaction', params: [
    btoa(String.fromCharCode(...raw)), { encoding: 'base64', skipPreflight: false, preflightCommitment: 'confirmed', maxRetries: 3 },
  ] }));
  const j = JSON.parse(r.text);
  if (!j.error) return 'sent';
  // -32002: preflight simulation failed; the logs carry the program's error.
  if (j.error.code === -32002) return { rejected: [j.error.message, ...(j.error.data?.logs ?? []).slice(-3)].join(' | ') };
  throw new Error(`sendTransaction: ${j.error.message}`);
}

/** Landed (confirmed), failed on-chain, still possible, or expired: past its blockhash without landing, so another
 *  may be sent in its place. */
export async function txStatus(env: Env, signature: string, lastValidBlockHeight: number): Promise<'confirmed' | 'pending' | 'expired' | 'failed'> {
  const read = async () => {
    const s = (await rpc(env, 'getSignatureStatuses', [[signature], { searchTransactionHistory: true }]))?.value?.[0];
    if (!s) return null;
    if (s.err) return 'failed' as const;
    return s.confirmationStatus === 'confirmed' || s.confirmationStatus === 'finalized' ? 'confirmed' as const : 'pending' as const;
  };
  const first = await read();
  if (first) return first;
  const height = await rpc(env, 'getBlockHeight', [{ commitment: 'confirmed' }]);
  if (height <= lastValidBlockHeight) return 'pending';
  // Past its blockhash and not seen: ask once more now that a node has seen that height (the first answer may have
  // come from a node behind the one that answered the height). Only then is it expired.
  return (await read()) ?? 'expired';
}
