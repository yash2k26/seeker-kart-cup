// Self-check of the price rails: a SOL price quoted in ORE lands on clean amounts, SKR-only skus hide it, and the
// per-sku override still wins.   npm run test:prices
import { baseUnits, type Rates } from '../src/prices';
import { CATALOG, DECIMALS, formatAmount, quote } from '../src/catalog';

let failures = 0;
const check = (ok: boolean, what: string) => { if (!ok) { failures++; console.log('  FAIL', what); } };
const r: Rates = { SKR: 0.0165, SOL: 115, ORE: 110, at: 0, source: 'env' };

check(DECIMALS.ORE === 11, 'ORE has 11 decimals');
check(baseUnits(0.03, 'ORE', r) === 3_100_000_000n, `0.03 SOL at $115 is 0.031 ORE at $110 (got ${formatAmount(baseUnits(0.03, 'ORE', r), 'ORE')})`);
check(baseUnits(0.0001, 'ORE', r) === 100_000_000n, 'the smallest ORE price is 0.001 ORE, never 0');
check(formatAmount(3_100_000_000n, 'ORE') === '0.031 ORE', `formats as 0.031 ORE (got ${formatAmount(3_100_000_000n, 'ORE')})`);
for (const s of CATALOG) {
  const q = quote(s, r);
  check(s.skrOnly ? q.ORE === '' && q.SOL === '' : BigInt(q.ORE) > 0n, `${s.sku}: ${s.skrOnly ? 'SKR only hides ORE and SOL' : 'quoted in ORE'}`);
}
const pinned = quote(CATALOG[0], r, JSON.stringify({ [CATALOG[0].sku]: { ORE: '123' } }));
check(pinned.ORE === '123', 'PRICES_JSON can pin an ORE price');
console.log(failures ? `prices: ${failures} failing` : 'prices: all checks pass');
process.exit(failures ? 1 : 0);
