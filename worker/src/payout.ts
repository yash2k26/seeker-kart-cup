// The treasury from the Worker's side: where shop payments go, and checking that one arrived. Paid rooms do not pass
// through here any more: their stakes sit in the escrow program (escrow.ts), which pays winners itself.
import { Keypair, PublicKey } from '@solana/web3.js';
import { getAssociatedTokenAddressSync } from '@solana/spl-token';
import type { Env } from './index';
import { bs58decode, isSignature } from './base58';
import { fetchTx, instructions, signedBy, claimSignature, usedByOther } from './chain';
import { type Currency } from './catalog';

export function treasury(env: Env): Keypair {
  const s = (env.TREASURY_SECRET ?? '').trim();
  if (!s) throw new Error('TREASURY_SECRET unset');
  const bytes = s.startsWith('[') ? Uint8Array.from(JSON.parse(s)) : bs58decode(s);
  return Keypair.fromSecretKey(bytes);
}

/** Where money goes in: TREASURY_OWNER, else the payout key's own address; its SKR account, else the associated one;
 *  its ORE associated account. One helper, so the address the client is told to pay is the address the payment is
 *  checked against. */
export function treasuryAddrs(env: Env): { owner: string; ata: string; oreAta: string } {
  let owner = (env.TREASURY_OWNER ?? '').trim();
  try { if (!owner && env.TREASURY_SECRET) owner = treasury(env).publicKey.toBase58(); } catch { /* malformed secret */ }
  let ata = (env.TREASURY_ATA ?? '').trim();
  try { if (!ata && owner) ata = getAssociatedTokenAddressSync(new PublicKey(env.SKR_MINT), new PublicKey(owner), true).toBase58(); } catch { /* bad owner */ }
  let oreAta = '';
  try { if (owner && env.ORE_MINT) oreAta = getAssociatedTokenAddressSync(new PublicKey(env.ORE_MINT), new PublicKey(owner), true).toBase58(); } catch { /* bad mint */ }
  return { owner, ata, oreAta };
}

/** pending: not on-chain yet (null from the RPC), may still land. rpcError: the RPC failed, which says nothing. */
export type EntryCheck = { ok: true; slot: number } | { ok: false; pending: boolean; rpcError?: boolean; why: string };

/** Did `wallet` pay `amount` of `currency` into the treasury with memo `memo`? Replay-proof via tx_log (`kind` labels the row).
 *  The transaction is checked BEFORE the signature is claimed: claiming first let anyone who saw a player's payment
 *  on-chain claim it under their own wallet, fail, and leave it marked used, so the real payer could never redeem it. */
export async function verifyPayment(env: Env, signature: string, wallet: string, memo: string, amount: bigint, currency: Currency, kind: string): Promise<EntryCheck> {
  if (env.MOCK_CHAIN === '1') {
    if (!signature.startsWith('mock')) return { ok: false, pending: false, why: 'mock reject' };
    return (await claimSignature(env, signature, wallet, kind)) ? { ok: true, slot: 0 } : { ok: false, pending: false, why: 'signature already used' };
  }
  if (!isSignature(signature)) return { ok: false, pending: false, why: 'not a transaction signature' };
  if (await usedByOther(env, signature, wallet, kind)) return { ok: false, pending: false, why: 'signature already used' };
  const { owner, ata, oreAta } = treasuryAddrs(env);
  if (!owner) return { ok: false, pending: false, why: 'payments are not set up' };
  let tx: any;
  try { tx = await fetchTx(env, signature); } catch (e: any) { return { ok: false, pending: true, rpcError: true, why: String(e?.message ?? e) }; }
  if (!tx) return { ok: false, pending: true, why: 'not landed yet' };
  const ixs = instructions(tx);
  const memoOk = ixs.find((i: any) => i.program === 'spl-memo')?.parsed === memo;
  let paid = false;
  if (currency === 'SOL') {
    const x = ixs.find((i: any) => i.program === 'system' && i.parsed?.type === 'transfer'
      && i.parsed.info?.destination === owner && i.parsed.info?.source === wallet);
    paid = !!x && BigInt(x.parsed.info.lamports) >= amount;
  } else {
    const mint = currency === 'ORE' ? env.ORE_MINT : env.SKR_MINT, dest = currency === 'ORE' ? oreAta : ata;
    if (!mint || !dest) return { ok: false, pending: false, why: `${currency} payments are not set up` };
    // From the payer's own associated account: it is where prizes and refunds go back to, and the payout never pays
    // rent to create one.
    const own = getAssociatedTokenAddressSync(new PublicKey(mint), new PublicKey(wallet), true).toBase58();
    const x = ixs.find((i: any) => i.program === 'spl-token' && i.parsed?.type === 'transferChecked'
      && i.parsed.info?.mint === mint && i.parsed.info?.destination === dest && i.parsed.info?.authority === wallet && i.parsed.info?.source === own);
    paid = !!x && BigInt(x.parsed.info.tokenAmount.amount) >= amount;
  }
  const ok = !tx.meta?.err && signedBy(tx, wallet) && memoOk && paid;
  if (!ok) return { ok: false, pending: false, why: 'payment did not match the entry' };   // not claimed: it may be someone else's
  if (!(await claimSignature(env, signature, wallet, kind))) return { ok: false, pending: false, why: 'signature already used' };
  await env.DB.prepare("UPDATE tx_log SET status = 'confirmed', slot = ?2 WHERE signature = ?1").bind(signature, tx.slot).run();
  return { ok: true, slot: tx.slot };
}
