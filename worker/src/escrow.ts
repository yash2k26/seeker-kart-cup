// The prize escrow program (escrow/programs/kartel_escrow) from the Worker's side: its addresses, a room account as
// the chain holds it, and the instructions the referee sends. Encoding is driven by the program's IDL
// (kartel_escrow.json, copied by scripts/sync-idl.mjs), so account order and argument layout follow the program.
import { ComputeBudgetProgram, Connection, Keypair, PublicKey, Transaction, TransactionInstruction } from '@solana/web3.js';
import { createAssociatedTokenAccountIdempotentInstruction, getAssociatedTokenAddressSync, TOKEN_PROGRAM_ID } from '@solana/spl-token';
import type { Env } from './index';
import { rpc } from './rpc';
import { bs58decode, bs58encode } from './base58';
import idl from './kartel_escrow.json';

// ---------- the IDL, as data ----------
type IdlType = string | { array: [IdlType, number] } | { vec: IdlType } | { option: IdlType } | { defined: { name: string } };
type IdlField = { name: string; type: IdlType };
type IdlTypeDef = { name: string; type: { kind: 'struct'; fields: IdlField[] } | { kind: 'enum'; variants: { name: string }[] } };
type IdlAccountMeta = { name: string; writable?: boolean; signer?: boolean; optional?: boolean; address?: string };
type IdlInstruction = { name: string; discriminator: number[]; accounts: IdlAccountMeta[]; args: IdlField[] };

const IDL = idl as unknown as { instructions: IdlInstruction[]; accounts: { name: string; discriminator: number[] }[]; types: IdlTypeDef[] };
const TYPES = new Map(IDL.types.map(t => [t.name, t]));

class Writer {
  bytes: number[] = [];
  raw(b: ArrayLike<number>) { for (let i = 0; i < b.length; i++) this.bytes.push(b[i]); }
  int(v: number | bigint, size: number) {
    let x = BigInt.asUintN(size * 8, BigInt(v));
    for (let i = 0; i < size; i++) { this.bytes.push(Number(x & 0xffn)); x >>= 8n; }
  }
}

class Reader {
  at = 0;
  constructor(readonly b: Uint8Array) {}
  take(n: number): Uint8Array {
    if (this.at + n > this.b.length) throw new Error('escrow: account data ends early');
    const out = this.b.subarray(this.at, this.at + n);
    this.at += n;
    return out;
  }
  uint(size: number): bigint { const s = this.take(size); let x = 0n; for (let i = size - 1; i >= 0; i--) x = (x << 8n) | BigInt(s[i]); return x; }
}

const INTS: Record<string, [number, boolean]> = { u8: [1, false], u16: [2, false], u32: [4, false], u64: [8, false], i64: [8, true] };

function encode(type: IdlType, value: any, w: Writer): void {
  if (typeof type === 'string') {
    if (type in INTS) return w.int(value, INTS[type][0]);
    if (type === 'bool') return w.int(value ? 1 : 0, 1);
    if (type === 'pubkey') return w.raw(new PublicKey(value).toBytes());
    throw new Error(`escrow: cannot encode ${type}`);
  }
  if ('array' in type) { for (let i = 0; i < type.array[1]; i++) encode(type.array[0], value[i], w); return; }
  if ('vec' in type) { w.int(value.length, 4); for (const v of value) encode(type.vec, v, w); return; }
  if ('option' in type) { if (value == null) w.int(0, 1); else { w.int(1, 1); encode(type.option, value, w); } return; }
  const def = TYPES.get(type.defined.name);
  if (!def) throw new Error(`escrow: no type ${type.defined.name}`);
  if (def.type.kind === 'struct') { for (const f of def.type.fields) encode(f.type, value[f.name], w); return; }
  const i = def.type.variants.findIndex(v => v.name === value);
  if (i < 0) throw new Error(`escrow: ${type.defined.name} has no variant ${value}`);
  w.int(i, 1);
}

function decode(type: IdlType, r: Reader): any {
  if (typeof type === 'string') {
    if (type in INTS) { const [size, signed] = INTS[type]; const x = r.uint(size); return signed ? BigInt.asIntN(size * 8, x) : x; }
    if (type === 'bool') return r.uint(1) === 1n;
    if (type === 'pubkey') return new PublicKey(r.take(32)).toBase58();
    throw new Error(`escrow: cannot decode ${type}`);
  }
  if ('array' in type) return Array.from({ length: type.array[1] }, () => decode(type.array[0], r));
  if ('vec' in type) return Array.from({ length: Number(r.uint(4)) }, () => decode(type.vec, r));
  if ('option' in type) return r.uint(1) === 1n ? decode(type.option, r) : null;
  const def = TYPES.get(type.defined.name);
  if (!def) throw new Error(`escrow: no type ${type.defined.name}`);
  if (def.type.kind === 'struct') return Object.fromEntries(def.type.fields.map(f => [f.name, decode(f.type, r)]));
  const variant = def.type.variants[Number(r.uint(1))];
  if (!variant) throw new Error(`escrow: bad ${type.defined.name} variant`);
  return variant.name;
}

/** One instruction from the IDL: `accounts` by name (null leaves an optional account out, as Anchor's "None"),
 *  `args` by name with the IDL's snake_case field names. */
export function instruction(program: PublicKey, name: string, accounts: Record<string, PublicKey | null>, args: Record<string, unknown> = {}): TransactionInstruction {
  const def = IDL.instructions.find(i => i.name === name);
  if (!def) throw new Error(`escrow: no instruction ${name}`);
  const keys = def.accounts.map(a => {
    const given = accounts[a.name];
    if (given === undefined && a.address) return { pubkey: new PublicKey(a.address), isSigner: false, isWritable: false };
    if (given == null) {
      if (!a.optional) throw new Error(`escrow: ${name} needs ${a.name}`);
      return { pubkey: program, isSigner: false, isWritable: false };
    }
    return { pubkey: given, isSigner: !!a.signer, isWritable: !!a.writable };
  });
  const w = new Writer();
  w.raw(def.discriminator);
  for (const arg of def.args) encode(arg.type, args[arg.name], w);
  return new TransactionInstruction({ programId: program, keys, data: Buffer.from(w.bytes) });
}

// ---------- addresses ----------
export const programId = (env: Env) => new PublicKey(env.ESCROW_PROGRAM!);
export const escrowConfigured = (env: Env) => !!env.ESCROW_PROGRAM && !!env.REFEREE_SECRET;
const u32le = (n: number) => { const b = Buffer.alloc(4); b.writeUInt32LE(n); return b; };
export const configAddress = (program: PublicKey) => PublicKey.findProgramAddressSync([Buffer.from('config')], program)[0];
export const roomAddress = (program: PublicKey, code: string, round: number) =>
  PublicKey.findProgramAddressSync([Buffer.from('room'), Buffer.from(code, 'ascii'), u32le(round)], program)[0];
export const tokenAccount = (owner: PublicKey, mint: PublicKey) => getAssociatedTokenAddressSync(mint, owner, true);

// ---------- a room as the chain holds it ----------
export type EscrowState = 'open' | 'locked' | 'settled' | 'cancelled';
/** `refund`: left out of the race at the lock; its whole stake is owed back, outside the split. */
export type ChainEntry = { player: string; stake: bigint; payout: bigint; paid: boolean; refund: boolean };
export type ChainRoom = {
  code: string; round: number; mint: string | null; minStake: bigint; maxStake: bigint; cutBps: number; maxPlayers: number;
  state: EscrowState; expiresAt: number; payer: string; treasury: string; houseCut: bigint; cutPaid: boolean; entries: ChainEntry[];
};

const ROOM_DISCRIMINATOR = IDL.accounts.find(a => a.name === 'Room')!.discriminator;

export function decodeRoom(data: Uint8Array): ChainRoom {
  if (data.length < 8 || ROOM_DISCRIMINATOR.some((b, i) => data[i] !== b)) throw new Error('escrow: not a room account');
  const r = decode({ defined: { name: 'Room' } }, new Reader(data.subarray(8)));
  return {
    code: String.fromCharCode(...r.code.map(Number)), round: Number(r.round), mint: r.mint,
    minStake: r.min_stake, maxStake: r.max_stake, cutBps: Number(r.cut_bps), maxPlayers: Number(r.max_players),
    state: (r.state as string).toLowerCase() as EscrowState, expiresAt: Number(r.expires_at), payer: r.payer, treasury: r.treasury,
    houseCut: r.house_cut, cutPaid: r.cut_paid,
    entries: r.entries.map((e: any) => ({ player: e.player, stake: e.stake, payout: e.payout, paid: e.paid, refund: e.refund })),
  };
}

/** The room account at `address` (confirmed), or null while there is none. An account the program does not own is
 *  not a room (lamports sent to the address before the room exists). */
export async function readRoom(env: Env, address: string): Promise<ChainRoom | null> {
  const res = await rpc(env, 'getAccountInfo', [address, { encoding: 'base64', commitment: 'confirmed' }]);
  const acc = res?.value;
  if (!acc || acc.owner !== env.ESCROW_PROGRAM || !acc.data?.[0]) return null;
  return decodeRoom(Uint8Array.from(atob(acc.data[0]), c => c.charCodeAt(0)));
}

// ---------- the referee's instructions ----------
/** The token-side accounts of an instruction: the mint, the room's vault, the person's token account and the token
 *  program for a token room; none for SOL. `holderName` is the IDL's name for the person's account. */
function legs(room: PublicKey, holder: PublicKey, mint: PublicKey | null, holderName: string): Record<string, PublicKey | null> {
  if (!mint) return { mint: null, vault: null, [holderName]: null, token_program: null };
  return { mint, vault: tokenAccount(room, mint), [holderName]: tokenAccount(holder, mint), token_program: TOKEN_PROGRAM_ID };
}

export type RoomTerms = { code: string; round: number; mint: string | null; minStake: bigint; maxStake: bigint; stakeUnit: bigint; maxPlayers: number; cutBps: number; ttlSecs: number };

export class Referee {
  readonly program: PublicKey;
  readonly key: Keypair;
  readonly config: PublicKey;

  constructor(readonly env: Env) {
    this.program = programId(env);
    const s = (env.REFEREE_SECRET ?? '').trim();
    if (!s) throw new Error('REFEREE_SECRET unset');
    this.key = Keypair.fromSecretKey(s.startsWith('[') ? Uint8Array.from(JSON.parse(s)) : bs58decode(s));
    this.config = configAddress(this.program);
  }

  room(code: string, round: number) { return roomAddress(this.program, code, round); }

  /** The room account, and for a token room its vault, created together. */
  createRoom(t: RoomTerms): TransactionInstruction[] {
    const room = this.room(t.code, t.round);
    const mint = t.mint ? new PublicKey(t.mint) : null;
    const ixs = [instruction(this.program, 'create_room', { referee: this.key.publicKey, config: this.config, room, mint }, {
      code: Array.from(Buffer.from(t.code, 'ascii')), round: t.round,
      params: { min_stake: t.minStake, max_stake: t.maxStake, stake_unit: t.stakeUnit, max_players: t.maxPlayers, cut_bps: t.cutBps, ttl_secs: t.ttlSecs },
    })];
    if (mint) ixs.push(createAssociatedTokenAccountIdempotentInstruction(this.key.publicKey, tokenAccount(room, mint), room, mint));
    return ixs;
  }

  removeEntry(room: PublicKey, mint: string | null, player: string): TransactionInstruction {
    const p = new PublicKey(player);
    return instruction(this.program, 'remove_entry', {
      referee: this.key.publicKey, config: this.config, room, player: p, ...legs(room, p, mint ? new PublicKey(mint) : null, 'player_token'),
    });
  }

  lock(room: PublicKey, players: string[]): TransactionInstruction {
    return instruction(this.program, 'lock', { referee: this.key.publicKey, config: this.config, room }, { players });
  }

  settle(room: PublicKey, order: string[]): TransactionInstruction {
    return instruction(this.program, 'settle', { referee: this.key.publicKey, config: this.config, room }, { order });
  }

  cancel(room: PublicKey): TransactionInstruction {
    return instruction(this.program, 'cancel', { caller: this.key.publicKey, config: this.config, room });
  }

  claim(room: PublicKey, mint: string | null, player: string): TransactionInstruction {
    const p = new PublicKey(player);
    return instruction(this.program, 'claim', { room, player: p, ...legs(room, p, mint ? new PublicKey(mint) : null, 'player_token') });
  }

  collectCut(room: PublicKey, mint: string | null, treasury: string): TransactionInstruction {
    const t = new PublicKey(treasury);
    return instruction(this.program, 'collect_cut', { room, treasury: t, ...legs(room, t, mint ? new PublicKey(mint) : null, 'treasury_token') });
  }

  closeRoom(room: PublicKey, mint: string | null, payer: string, treasury: string): TransactionInstruction {
    return instruction(this.program, 'close_room', {
      room, payer: new PublicKey(payer), ...legs(room, new PublicKey(treasury), mint ? new PublicKey(mint) : null, 'treasury_token'),
    });
  }

  /** Signed by the referee (who also pays the fee) with its signature known BEFORE it is sent: the caller records the
   *  signature first, so a lost response or a restart looks the transaction up instead of sending another. */
  async sign(ixs: TransactionInstruction[], computeUnits: number): Promise<{ raw: Uint8Array; signature: string; lastValidBlockHeight: number }> {
    const conn = new Connection(this.env.HELIUS_URL, 'confirmed');
    const tx = new Transaction();
    tx.add(ComputeBudgetProgram.setComputeUnitLimit({ units: computeUnits }));
    tx.add(ComputeBudgetProgram.setComputeUnitPrice({ microLamports: Number(this.env.PRIORITY_FEE_MICROLAMPORTS ?? '20000') }));
    tx.add(...ixs);
    const { blockhash, lastValidBlockHeight } = await conn.getLatestBlockhash('confirmed');
    tx.recentBlockhash = blockhash;
    tx.feePayer = this.key.publicKey;
    tx.sign(this.key);
    return { raw: tx.serialize(), signature: bs58encode(tx.signature!), lastValidBlockHeight };
  }
}

/** A join, as the phone builds it (Unity: WalletFlow.JoinRoom); the smoke test's racers send this. */
export function joinInstruction(program: PublicKey, code: string, round: number, player: PublicKey, mint: PublicKey | null, stake: bigint): TransactionInstruction {
  const room = roomAddress(program, code, round);
  return instruction(program, 'join', { player, config: configAddress(program), room, ...legs(room, player, mint, 'player_token') }, { stake });
}
