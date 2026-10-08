// A local chain for the escrow smoke test (scripts/smoke-escrow.ts). Keys live in .escrow-local/ (git-ignored).
//   npm run escrow:localnet -- validator   prints the solana-test-validator command (run it in its own terminal)
//   npm run escrow:localnet                 funds the keys, initializes the program's config, makes a stand-in SKR
//                                           mint, and prints the `wrangler dev` flags that point the Worker at it
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { Connection, Keypair, LAMPORTS_PER_SOL, PublicKey, Transaction, sendAndConfirmTransaction } from '@solana/web3.js';
import { createMint, getOrCreateAssociatedTokenAccount } from '@solana/spl-token';
import { instruction, configAddress } from '../src/escrow';
import { bs58encode } from '../src/base58';

const RPC = 'http://127.0.0.1:8899';
const PROGRAM = new PublicKey('KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv');
const LOADER = new PublicKey('BPFLoaderUpgradeab1e11111111111111111111111');
const DIR = resolve(process.cwd(), '.escrow-local');

function key(name: string): Keypair {
  const file = resolve(DIR, `${name}.json`);
  if (existsSync(file)) return Keypair.fromSecretKey(Uint8Array.from(JSON.parse(readFileSync(file, 'utf8'))));
  mkdirSync(DIR, { recursive: true });
  const k = Keypair.generate();
  writeFileSync(file, JSON.stringify(Array.from(k.secretKey)));
  return k;
}

const admin = key('admin'), referee = key('referee'), treasury = key('treasury'), skr = key('skr-mint');

if (process.argv.includes('validator')) {
  console.log(`solana-test-validator --reset --quiet --ledger ${resolve(DIR, 'ledger')} \\
  --upgradeable-program ${PROGRAM.toBase58()} ../escrow/target/deploy/kartel_escrow.so ${resolve(DIR, 'admin.json')}`);
  process.exit(0);
}

const conn = new Connection(RPC, 'confirmed');
for (const [k, sol] of [[admin, 100], [referee, 10], [treasury, 1]] as const) {
  if ((await conn.getBalance(k.publicKey)) < sol * LAMPORTS_PER_SOL / 2) {
    await conn.confirmTransaction(await conn.requestAirdrop(k.publicKey, sol * LAMPORTS_PER_SOL), 'confirmed');
  }
}

const config = configAddress(PROGRAM);
if (!(await conn.getAccountInfo(config))) {
  const programData = PublicKey.findProgramAddressSync([PROGRAM.toBytes()], LOADER)[0];
  const ix = instruction(PROGRAM, 'initialize', { admin: admin.publicKey, config, program: PROGRAM, program_data: programData },
    { referee: referee.publicKey, treasury: treasury.publicKey, max_cut_bps: 1500 });
  await sendAndConfirmTransaction(conn, new Transaction().add(ix), [admin], { commitment: 'confirmed' });
  console.log('config initialized:', config.toBase58());
}

if (!(await conn.getAccountInfo(skr.publicKey))) await createMint(conn, admin, admin.publicKey, null, 6, skr, { commitment: 'confirmed' });
await getOrCreateAssociatedTokenAccount(conn, admin, skr.publicKey, treasury.publicKey, true, 'confirmed');

const vars = [
  `HELIUS_URL:${RPC}`, `ESCROW_PROGRAM:${PROGRAM.toBase58()}`, `REFEREE_SECRET:${bs58encode(referee.secretKey)}`,
  `SKR_MINT:${skr.publicKey.toBase58()}`, `TREASURY_OWNER:${treasury.publicKey.toBase58()}`, 'PAID_ROOMS:1',
].map(v => `--var ${v}`).join(' ');
writeFileSync(resolve(DIR, 'wrangler-vars.txt'), vars + '\n');
console.log(`ready. referee ${referee.publicKey.toBase58()} · treasury ${treasury.publicKey.toBase58()} · SKR stand-in ${skr.publicKey.toBase58()}`);
console.log(`npx wrangler dev --port 8787 ${vars.replace(/REFEREE_SECRET:\S+/, 'REFEREE_SECRET:…')}   (full flags in .escrow-local/wrangler-vars.txt)`);
