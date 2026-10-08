// Copies the escrow program's IDL (escrow/target/idl, written by `anchor build`) into src/kartel_escrow.json without
// its doc comments. `--check` fails if the copy is stale instead of writing it (run by test:escrow).
import { readFileSync, writeFileSync, existsSync } from 'node:fs';

const from = new URL('../../escrow/target/idl/kartel_escrow.json', import.meta.url);
const to = new URL('../src/kartel_escrow.json', import.meta.url);
const strip = v => Array.isArray(v) ? v.map(strip) : v && typeof v === 'object'
  ? Object.fromEntries(Object.entries(v).filter(([k]) => k !== 'docs').map(([k, x]) => [k, strip(x)])) : v;

if (!existsSync(from)) { console.log('sync-idl: no escrow build to compare with (run anchor build --arch v0 in escrow/)'); process.exit(0); }
const fresh = JSON.stringify(strip(JSON.parse(readFileSync(from, 'utf8'))), null, 1) + '\n';
if (process.argv.includes('--check')) {
  const current = existsSync(to) ? readFileSync(to, 'utf8') : '';
  if (current !== fresh) { console.error('sync-idl: src/kartel_escrow.json is stale: run node scripts/sync-idl.mjs'); process.exit(1); }
  console.log('sync-idl: src/kartel_escrow.json matches the program');
} else {
  writeFileSync(to, fresh);
  console.log(`sync-idl: wrote src/kartel_escrow.json (${fresh.length} bytes)`);
}
