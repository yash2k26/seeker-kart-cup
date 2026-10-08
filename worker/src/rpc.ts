// One JSON-RPC call to the Solana node behind HELIUS_URL. On its own, so modules that only read the chain (escrow.ts)
// do not pull in the Worker's routes.
import type { Env } from './index';

export async function rpc(env: Env, method: string, params: unknown[]) {
  const r = await fetch(env.HELIUS_URL, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }),
  });
  const j = await r.json<any>();
  if (j.error) throw new Error(`${method}: ${j.error.message}`);
  return j.result;
}

/** Hands a signed transaction to the network. 'rejected': the node simulated it and refused it, so it was never
 *  broadcast and cannot land. A throw says nothing either way (it may have gone out): only txStatus decides that. */
export async function sendRaw(env: Env, raw: Uint8Array): Promise<'sent' | { rejected: string }> {
  const r = await fetch(env.HELIUS_URL, {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ jsonrpc: '2.0', id: 1, method: 'sendTransaction', params: [
      btoa(String.fromCharCode(...raw)), { encoding: 'base64', skipPreflight: false, preflightCommitment: 'confirmed', maxRetries: 3 },
    ] }),
  });
  const j = await r.json<any>();
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
