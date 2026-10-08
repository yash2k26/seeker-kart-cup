// Base58 as Solana writes addresses and signatures. Dependency-free, so any module (or a test bundle) can use it.
const B58 = '123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz';
export function bs58decode(s: string): Uint8Array {
  const bytes = [0];
  for (const c of s) {
    let carry = B58.indexOf(c);
    if (carry < 0) throw new Error('bad base58');
    for (let j = 0; j < bytes.length; j++) { carry += bytes[j] * 58; bytes[j] = carry & 0xff; carry >>= 8; }
    while (carry) { bytes.push(carry & 0xff); carry >>= 8; }
  }
  for (const c of s) { if (c !== '1') break; bytes.push(0); }
  return new Uint8Array(bytes.reverse());
}
export function bs58encode(bytes: Uint8Array): string {
  let n = 0n; for (const b of bytes) n = n * 256n + BigInt(b);
  let out = ''; while (n > 0n) { out = B58[Number(n % 58n)] + out; n /= 58n; }
  for (const b of bytes) { if (b !== 0) break; out = '1' + out; }
  return out;
}

/** A Solana transaction signature: base58 of 64 bytes. Anything else never reaches the RPC. */
export function isSignature(s: unknown): boolean {
  if (typeof s !== 'string' || s.length < 64 || s.length > 90) return false;
  try { return bs58decode(s).length === 64; } catch { return false; }
}
