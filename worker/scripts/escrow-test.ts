// The Worker's side of the escrow: fair shares against the program's own vectors, the "if you win" preview, and the
// instruction encoding against the IDL. (The program itself is tested in escrow/: cargo test.)
//   npm run test:escrow
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { PublicKey } from '@solana/web3.js';
import { split, ifWin, SplitError } from '../src/fairshares';
import { joinInstruction, instruction, roomAddress, configAddress, tokenAccount } from '../src/escrow';
import idl from '../src/kartel_escrow.json';

let failures = 0;
const check = (what: string, ok: boolean, extra = '') => { if (!ok) { failures++; console.log(`FAIL  ${what}  ${extra}`); } };

// 1. fair shares: the same vectors the Rust tests read
// (npm runs scripts from worker/; the bundle itself lives in node_modules/.cache)
const vectors = JSON.parse(readFileSync(resolve(process.cwd(), '../escrow/vectors/fair-shares.json'), 'utf8'));
for (const v of vectors) {
  const stakes = v.stakes.map(BigInt);
  try {
    const got = split(stakes, v.rank, v.cutBps);
    if (v.error) { check(v.name, false, `expected ${v.error}, got a split`); continue; }
    check(`${v.name}: payouts`, got.payouts.join() === v.payouts.join(), `${got.payouts} vs ${v.payouts}`);
    check(`${v.name}: house`, got.house === BigInt(v.house), `${got.house} vs ${v.house}`);
  } catch (e) {
    check(v.name, e instanceof SplitError && e.kind === v.error, String(e));
  }
}
console.log(`fair shares: ${vectors.length} vectors`);

// 2. the winner's take does not depend on the order behind it
let x = 0x9e3779b9;
const rnd = (n: number) => { x ^= x << 13; x ^= x >>> 17; x ^= x << 5; return (x >>> 0) % n; };
for (let t = 0; t < 3000; t++) {
  const n = 2 + rnd(5);
  const stakes = Array.from({ length: n }, () => BigInt([10_000_000, 50_000_000, 100_000_000, 150_000_000, 500_000_000][rnd(5)]));
  const seat = rnd(n), cut = rnd(1501);
  const rank = stakes.map((_, i) => i);
  for (let i = n - 1; i > 0; i--) { const j = rnd(i + 1); [rank[i], rank[j]] = [rank[j], rank[i]]; }
  const winnerFirst = rank.map(r => (r === 0 ? rank[seat] : r === rank[seat] ? 0 : r));   // swap places so `seat` wins
  check('ifWin is order-independent', split(stakes, winnerFirst, cut).payouts[seat] === ifWin(stakes, seat, cut), `${stakes} seat ${seat}`);
}
console.log('ifWin: 3000 random rooms');

// 3. a join, byte for byte: discriminator + u64 LE stake, accounts in the IDL's order, "None" as the program id
const program = new PublicKey('KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv');
const player = new PublicKey('9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin');
const join = joinInstruction(program, 'K7P2Q', 1, player, null, 150_000_000n);
const disc = (idl as any).instructions.find((i: any) => i.name === 'join').discriminator;
check('join data', Buffer.from(join.data).equals(Buffer.from([...disc, 0x80, 0xd1, 0xf0, 0x08, 0, 0, 0, 0])), Buffer.from(join.data).toString('hex'));
const room = roomAddress(program, 'K7P2Q', 1);
check('join accounts (SOL)', join.keys.map(k => `${k.pubkey.toBase58().slice(0, 4)}${k.isSigner ? 's' : ''}${k.isWritable ? 'w' : ''}`).join(' ')
  === [`${player.toBase58().slice(0, 4)}sw`, configAddress(program).toBase58().slice(0, 4), `${room.toBase58().slice(0, 4)}w`, 'Kart', 'Kart', 'Kart', 'Kart', '1111'].join(' '),
  join.keys.map(k => k.pubkey.toBase58()).join(' '));
const mint = new PublicKey('SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3');
const joinSkr = joinInstruction(program, 'K7P2Q', 1, player, mint, 5_000_000n);
check('join accounts (SKR)', joinSkr.keys[3].pubkey.equals(mint) && joinSkr.keys[4].pubkey.equals(tokenAccount(room, mint)) && joinSkr.keys[4].isWritable
  && joinSkr.keys[5].pubkey.equals(tokenAccount(player, mint)) && joinSkr.keys[5].isWritable);
check('room address uses the round', !roomAddress(program, 'K7P2Q', 2).equals(room));
let threw = false;
try { instruction(program, 'join', { player }, { stake: 1n }); } catch { threw = true; }
check('a missing required account is refused', threw);

if (failures) { console.log(`${failures} FAILURE(S)`); process.exit(1); }
console.log('escrow: ALL PASS');
