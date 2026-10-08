// Operating the escrow program's config on any cluster. Every change is signed by the admin (the upgrade authority).
//   npm run escrow:admin -- show     --url <rpc>
//   npm run escrow:admin -- init     --url <rpc> --admin <keypair.json> --referee <pubkey> --treasury <pubkey> [--max-cut 1500]
//   npm run escrow:admin -- pause    --url <rpc> --admin <keypair.json>     (no new rooms or stakes; money can still leave)
//   npm run escrow:admin -- resume   --url <rpc> --admin <keypair.json>
//   npm run escrow:admin -- referee  --url <rpc> --admin <keypair.json> --to <pubkey>   (rotate the game server's key)
import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { Connection, Keypair, PublicKey, Transaction, sendAndConfirmTransaction } from '@solana/web3.js';
import { configAddress, instruction } from '../src/escrow';

const PROGRAM = new PublicKey('KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv');
const LOADER = new PublicKey('BPFLoaderUpgradeab1e11111111111111111111111');
const [command, ...rest] = process.argv.slice(2);
const flag = (name: string) => { const i = rest.indexOf(`--${name}`); return i >= 0 ? rest[i + 1] : undefined; };
const need = (name: string) => { const v = flag(name); if (!v) { console.error(`--${name} is required`); process.exit(2); } return v; };
const keypair = (path: string) => Keypair.fromSecretKey(Uint8Array.from(JSON.parse(readFileSync(path.replace(/^~/, homedir()), 'utf8'))));

const conn = new Connection(need('url'), 'confirmed');
const config = configAddress(PROGRAM);

async function show() {
  const acc = await conn.getAccountInfo(config);
  if (!acc) { console.log(`no config at ${config.toBase58()}: run init`); return; }
  const d = acc.data.subarray(8);
  const key = (at: number) => new PublicKey(d.subarray(at, at + 32)).toBase58();
  console.log({ config: config.toBase58(), admin: key(0), referee: key(32), treasury: key(64), maxCutBps: d.readUInt16LE(96), paused: d[98] === 1 });
}

async function send(ix: ReturnType<typeof instruction>, admin: Keypair) {
  const sig = await sendAndConfirmTransaction(conn, new Transaction().add(ix), [admin], { commitment: 'confirmed' });
  console.log('sent', sig);
  await show();
}

switch (command) {
  case 'show': await show(); break;
  case 'init': {
    const admin = keypair(need('admin'));
    const programData = PublicKey.findProgramAddressSync([PROGRAM.toBytes()], LOADER)[0];
    await send(instruction(PROGRAM, 'initialize', { admin: admin.publicKey, config, program: PROGRAM, program_data: programData }, {
      referee: new PublicKey(need('referee')), treasury: new PublicKey(need('treasury')), max_cut_bps: Number(flag('max-cut') ?? '1500'),
    }), admin);
    break;
  }
  case 'pause': case 'resume': case 'referee': {
    const admin = keypair(need('admin'));
    const update = { admin: null, referee: command === 'referee' ? new PublicKey(need('to')) : null, treasury: null, max_cut_bps: null,
      paused: command === 'referee' ? null : command === 'pause' };
    await send(instruction(PROGRAM, 'update_config', { admin: admin.publicKey, config }, { update }), admin);
    break;
  }
  default: console.error('commands: show | init | pause | resume | referee'); process.exit(2);
}
