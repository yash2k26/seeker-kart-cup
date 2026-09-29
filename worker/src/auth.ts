// Sign-In-With-Solana challenge/verify + stateless HMAC sessions. Zero dependencies: Workers WebCrypto does Ed25519.
import type { Env } from './index';
import { json, bad, now } from './index';
import { refreshProfile, getProfile } from './chain';

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
const b64 = (s: string) => Uint8Array.from(atob(s), c => c.charCodeAt(0));
const enc = new TextEncoder();

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

export async function verifyEd25519(pubkeyB58: string, msg: Uint8Array, sig: Uint8Array): Promise<boolean> {
  try {
    const key = await crypto.subtle.importKey('raw', bs58decode(pubkeyB58), { name: 'Ed25519' }, false, ['verify']);
    return await crypto.subtle.verify('Ed25519', key, sig, msg);
  } catch { return false; }
}

// ---- sessions: HMAC over {w: wallet, e: expiry}; no table ----
const b64u = (b: ArrayBuffer | Uint8Array) =>
  btoa(String.fromCharCode(...new Uint8Array(b as ArrayBuffer))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
const unb64u = (s: string) => Uint8Array.from(atob(s.replace(/-/g, '+').replace(/_/g, '/')), c => c.charCodeAt(0));

async function hmacKey(env: Env, usage: ("sign" | "verify")[]) {
  return crypto.subtle.importKey('raw', enc.encode(env.SESSION_HMAC_KEY), { name: 'HMAC', hash: 'SHA-256' }, false, usage);
}
export async function signHmac(env: Env, payload: object): Promise<string> {
  const body = b64u(enc.encode(JSON.stringify(payload)));
  const sig = await crypto.subtle.sign('HMAC', await hmacKey(env, ['sign']), enc.encode(body));
  return `${body}.${b64u(sig)}`;
}
export async function readHmac<T = any>(env: Env, token: string | null | undefined): Promise<T | null> {
  if (!token || !token.includes('.')) return null;
  try {                                   // a malformed token is no token, never an error
    const [body, sig] = token.split('.');
    const ok = await crypto.subtle.verify('HMAC', await hmacKey(env, ['verify']), unb64u(sig), enc.encode(body));
    if (!ok) return null;
    return JSON.parse(new TextDecoder().decode(unb64u(body))) as T;
  } catch { return null; }
}
export async function mintSession(env: Env, wallet: string, days = 30) {
  return signHmac(env, { w: wallet, e: now() + days * 86400 });
}
export async function readSession(env: Env, header: string | null): Promise<string | null> {
  const token = header?.replace(/^Bearer\s+/i, '') ?? null;
  const p = await readHmac<{ w: string; e: number }>(env, token);
  return p && p.e > now() ? p.w : null;
}

// ---- SIWS ----
export async function challenge(_req: Request, env: Env) {
  const nonce = crypto.randomUUID().replace(/-/g, '');
  const issued = new Date(), exp = new Date(Date.now() + 10 * 60_000);
  const payload = {
    domain: env.APP_DOMAIN,
    uri: `https://${env.APP_DOMAIN}`,
    statement: 'Sign in to Seeker Kart Cup',
    version: '1',
    chainId: 'solana:mainnet',
    nonce,
    issuedAt: issued.toISOString(),
    expirationTime: exp.toISOString(),
  };
  await env.DB.prepare('INSERT INTO siws_nonces (nonce, payload, expires_at) VALUES (?1, ?2, ?3)')
    .bind(nonce, JSON.stringify(payload), Math.floor(exp.getTime() / 1000)).run();
  return json(payload);
}

// Body: { nonce, signInResult: {address, signed_message, signature} }  (native SIWS, Seed Vault)
//    or { nonce, address, challengeSig }  (SignMessage fallback: base64 of message||signature — last 64 bytes are the sig)
export async function verify(req: Request, env: Env, ctx: ExecutionContext) {
  const body = await req.json<any>().catch(() => null);
  if (!body?.nonce) return bad('nonce required');

  const row = await env.DB.prepare('DELETE FROM siws_nonces WHERE nonce = ?1 AND expires_at > ?2 RETURNING payload')
    .bind(body.nonce, now()).first<{ payload: string }>();
  if (!row) return bad('bad nonce', 401);
  const issued = JSON.parse(row.payload);

  let address: string, signedMessage: Uint8Array, signature: Uint8Array;
  if (body.signInResult) {
    address = body.signInResult.address;
    signedMessage = b64(body.signInResult.signed_message);
    signature = b64(body.signInResult.signature);
  } else if (body.challengeSig && body.address) {
    address = body.address;
    const payload = b64(body.challengeSig);
    signedMessage = payload.slice(0, -64);
    signature = payload.slice(-64);
  } else return bad('missing signature');

  if (!(await verifyEd25519(address, signedMessage, signature))) return bad('bad signature', 401);
  const text = new TextDecoder().decode(signedMessage);
  if (!text.includes(issued.domain) || !text.includes(issued.nonce) || !text.includes(address)) return bad('payload mismatch', 401);

  await env.DB.prepare(
    'INSERT INTO players (wallet, created_at, last_seen) VALUES (?1, ?2, ?2) ON CONFLICT(wallet) DO UPDATE SET last_seen = ?2',
  ).bind(address, now()).run();

  ctx.waitUntil(refreshProfile(env, address));
  return json({ token: await mintSession(env, address), ...(await getProfile(env, address)) });
}
