// Treasury: verifies entry payments in, sends prizes and refunds out. Custodial by design for the hackathon —
// every payout is a plain on-chain transfer whose signature the players see on the results screen.
// The key never leaves `wrangler secret put TREASURY_SECRET` (base58 or JSON byte array of the 64-byte secret key).
import { Connection, Keypair, PublicKey, SystemProgram, Transaction, TransactionInstruction, ComputeBudgetProgram } from '@solana/web3.js';
import { createTransferCheckedInstruction, getAssociatedTokenAddressSync } from '@solana/spl-token';
import type { Env } from './index';
import { bs58decode, bs58encode, isSignature } from './auth';
import { rpc, fetchTx, instructions, signedBy, claimSignature, usedByOther } from './chain';
import { DECIMALS, type Currency } from './catalog';

const MEMO = new PublicKey('MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr');

export type PayoutRow = { id: string; wallet: string; amount: string; currency: Currency };

export function treasury(env: Env): Keypair {
  const s = (env.TREASURY_SECRET ?? '').trim();
  if (!s) throw new Error('TREASURY_SECRET unset');
  const bytes = s.startsWith('[') ? Uint8Array.from(JSON.parse(s)) : bs58decode(s);
  return Keypair.fromSecretKey(bytes);
}

/** Where money goes in: TREASURY_OWNER, else the payout key's own address; its SKR account, else the associated one.
 *  One helper, so the address the client is told to pay is the address the payment is checked against. */
export function treasuryAddrs(env: Env): { owner: string; ata: string } {
  let owner = (env.TREASURY_OWNER ?? '').trim();
  try { if (!owner && env.TREASURY_SECRET) owner = treasury(env).publicKey.toBase58(); } catch { /* malformed secret */ }
  let ata = (env.TREASURY_ATA ?? '').trim();
  try { if (!ata && owner) ata = getAssociatedTokenAddressSync(new PublicKey(env.SKR_MINT), new PublicKey(owner), true).toBase58(); } catch { /* bad owner */ }
  return { owner, ata };
}

/** Prizes and refunds need the payout key (or the local mock chain). Without it, nothing may take an entry fee. */
export function canPayOut(env: Env): boolean {
  if (env.MOCK_CHAIN === '1') return true;
  try { treasury(env); return true; } catch { return false; }
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
  const { owner, ata } = treasuryAddrs(env);
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
    // From the payer's own associated account: it is where prizes and refunds go back to, and the payout never pays
    // rent to create one.
    const own = getAssociatedTokenAddressSync(new PublicKey(env.SKR_MINT), new PublicKey(wallet), true).toBase58();
    const x = ixs.find((i: any) => i.program === 'spl-token' && i.parsed?.type === 'transferChecked'
      && i.parsed.info?.mint === env.SKR_MINT && i.parsed.info?.destination === ata && i.parsed.info?.authority === wallet && i.parsed.info?.source === own);
    paid = !!x && BigInt(x.parsed.info.tokenAmount.amount) >= amount;
  }
  const ok = !tx.meta?.err && signedBy(tx, wallet) && memoOk && paid;
  if (!ok) return { ok: false, pending: false, why: 'payment did not match the entry' };   // not claimed: it may be someone else's
  if (!(await claimSignature(env, signature, wallet, kind))) return { ok: false, pending: false, why: 'signature already used' };
  await env.DB.prepare("UPDATE tx_log SET status = 'confirmed', slot = ?2 WHERE signature = ?1").bind(signature, tx.slot).run();
  return { ok: true, slot: tx.slot };
}

/** A signed transaction paying every row (same currency), with its signature, BEFORE anything is sent: the caller
 *  saves the rows as 'sent' under that signature first, so a restart or a lost response looks the transaction up
 *  instead of paying twice (a new send only after the old blockhash has expired unseen).
 *  No recipient's token account is ever created, for prizes or refunds: the treasury would pay its rent, and a ring of
 *  winners closing their accounts could farm that rent out of the payout key. An SKR winner whose account is gone gets
 *  a failing row (settled by hand), and one row per transaction keeps it from holding up anyone else's. */
export async function buildBatch(env: Env, rows: PayoutRow[], memo: string): Promise<{ raw: Uint8Array | null; signature: string; lastValidBlockHeight: number }> {
  if (env.MOCK_CHAIN === '1') return { raw: null, signature: 'mock-' + crypto.randomUUID().slice(0, 12), lastValidBlockHeight: 0 };
  const key = treasury(env);
  const conn = new Connection(env.HELIUS_URL, 'confirmed');
  const tx = new Transaction();
  tx.add(ComputeBudgetProgram.setComputeUnitLimit({ units: 20_000 + rows.length * 40_000 }));
  tx.add(ComputeBudgetProgram.setComputeUnitPrice({ microLamports: Number(env.PRIORITY_FEE_MICROLAMPORTS ?? '50000') }));
  for (const r of rows) {
    const to = new PublicKey(r.wallet);
    if (r.currency === 'SOL') {
      tx.add(SystemProgram.transfer({ fromPubkey: key.publicKey, toPubkey: to, lamports: BigInt(r.amount) }));
    } else {
      const mint = new PublicKey(env.SKR_MINT);
      tx.add(createTransferCheckedInstruction(new PublicKey(treasuryAddrs(env).ata), mint, getAssociatedTokenAddressSync(mint, to, true), key.publicKey, BigInt(r.amount), DECIMALS.SKR));
    }
  }
  tx.add(new TransactionInstruction({ programId: MEMO, keys: [], data: Buffer.from(memo, 'utf8') }));
  const { blockhash, lastValidBlockHeight } = await conn.getLatestBlockhash('confirmed');
  tx.recentBlockhash = blockhash;
  tx.feePayer = key.publicKey;
  tx.sign(key);
  return { raw: tx.serialize(), signature: bs58encode(tx.signature!), lastValidBlockHeight };
}

/** Hand a built transaction to the network. A throw here does not mean it did not land: txStatus decides. */
export async function sendRaw(env: Env, raw: Uint8Array | null): Promise<void> {
  if (!raw) return;
  await new Connection(env.HELIUS_URL, 'confirmed').sendRawTransaction(raw, { skipPreflight: false, maxRetries: 3, preflightCommitment: 'confirmed' });
}

export async function txStatus(env: Env, signature: string, lastValidBlockHeight: number): Promise<'confirmed' | 'pending' | 'expired' | 'failed'> {
  if (env.MOCK_CHAIN === '1') return 'confirmed';
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
  // come from a node behind the one that answered the height). Only then is it expired, and safe to pay again.
  return (await read()) ?? 'expired';
}

/** The first of these signatures that landed (confirmed, without error), or null. Before a row is paid again, every
 *  transaction ever sent for it is asked about, not only the last. */
export async function landedAny(env: Env, sigs: string[]): Promise<string | null> {
  if (env.MOCK_CHAIN === '1' || !sigs.length) return null;
  const st = await rpc(env, 'getSignatureStatuses', [sigs, { searchTransactionHistory: true }]);
  const i = (st?.value ?? []).findIndex((s: any) => s && !s.err && (s.confirmationStatus === 'confirmed' || s.confirmationStatus === 'finalized'));
  return i >= 0 ? sigs[i] : null;
}

/** Signatures of `wallet`'s recent transactions that carry exactly `memo`: an entry the phone paid but never reported
 *  (it was killed while the wallet app was open). */
export async function findEntrySignatures(env: Env, wallet: string, memo: string): Promise<string[]> {
  if (env.MOCK_CHAIN === '1') return [];
  const list: any[] = await rpc(env, 'getSignaturesForAddress', [wallet, { limit: 25, commitment: 'confirmed' }]);
  // the RPC gives memos as "[len] text", several joined by "; "
  const has = (m: unknown) => typeof m === 'string' && m.split('; ').some(x => x.replace(/^\[\d+\]\s*/, '') === memo);
  return (list ?? []).filter(s => !s.err && has(s.memo)).map(s => s.signature);
}
