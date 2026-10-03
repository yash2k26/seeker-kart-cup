using System;
using System.Collections.Generic;
using System.Text;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Utilities;
using UnityEngine;
using UnityEngine.Events;

namespace KartCup
{
    /// Every wallet beat: Seed Vault sign-in (SIWS), a mainnet Memo on a personal best, and one payment path that
    /// the shop and the tournament entry both use (SOL or SKR to the treasury, plus a memo the Worker verifies).
    /// Rules from research/14: one MWA op at a time, prompts only from menus, rent guard before any tx, mainnet from
    /// day one, silent reconnect via cached auth token + our HMAC session. Treasury addresses come from /v1/config,
    /// never from the APK.
    public sealed class WalletFlow : MonoBehaviour
    {
        public static WalletFlow Instance { get; private set; }
        /// Fires on sign-in, sign-out and after a purchase: the garage and the lobby redraw from it.
        public static event Action<KartApi.Profile> ProfileChanged;

        [SerializeField] long minLamportsForTx = 1_000_000;   // rent floor is ~650k after SIMD-0437; Seed Vault injects ComputeBudget ixs
        [SerializeField] UnityEvent<string> onStatus = new();         // UI text
        [SerializeField] UnityEvent<KartApi.Profile> onLoggedIn = new();

        public KartApi.Profile Profile { get; private set; }
        public bool Busy { get; private set; }
        public bool Connected => Web3.Wallet?.Account != null;
        public bool SignedIn => Connected && KartApi.HasSession;
        public string LastStatus { get; private set; }

        void Awake() => Instance = this;

        async void Start()
        {
            _ = KartApi.Config();   // treasury + prices, warmed early so the first shop tap does not wait on it
            // Returning player: silent reconnect (no prompt) + our session; no probe with Login() when nothing is cached.
            try
            {
                if (await MwaSession.HasCachedSession() && KartApi.HasSession)
                {
                    await Web3.Instance.LoginWalletAdapter();
                    SetProfile(await KartApi.Me());
                    Status(Profile.skrName ?? Short(Profile.wallet));   // label otherwise stays "Not connected" after a silent reconnect
                    KartApi.Event("wallet_reconnect");
                }
            }
            catch (Exception e) { Debug.LogWarning("[wallet] silent reconnect skipped: " + e.Message); }
            _ = SettlePurchases();   // a shop payment the Worker had not granted when the app last closed
        }

        void SetProfile(KartApi.Profile p)
        {
            Profile = p;
            if (p != null) Unlocks.Set(p.unlocks); else Unlocks.Clear();
            onLoggedIn?.Invoke(p);
            ProfileChanged?.Invoke(p);
        }

        /// Button: "Save to leaderboard" / "Sign in". One prompt: connect + Sign-In-With-Solana.
        public void Login() { if (!Busy) _ = LoginAsync(); }

        public async Awaitable<bool> LoginAsync()
        {
            bool outer = Busy; Busy = true;
            try
            {
                Status("Opening wallet…");
                var c = await KartApi.Challenge();
                var (account, siws) = await Web3.Instance.LoginWalletAdapter(new SignInPayload
                {
                    Domain = c.domain, Uri = c.uri, Statement = c.statement, Nonce = c.nonce,
                    IssuedAt = c.issuedAt, ExpirationTime = c.expirationTime, Version = c.version, ChainId = c.chainId,
                });
                if (account == null) { Status("Cancelled"); return false; }

                KartApi.Profile profile;
                if (siws != null && !string.IsNullOrEmpty(siws.Signature))
                    profile = await KartApi.VerifySiws(c.nonce, siws.Address, siws.SignedMessage, siws.Signature);
                else
                {
                    // Wallet without native SIWS: SignMessage returns message||signature; the Worker checks the last 64 bytes.
                    var msg = Encoding.UTF8.GetBytes($"{c.domain} wants you to sign in with your Solana account:\n{account.PublicKey}\n\n{c.statement}\n\nNonce: {c.nonce}");
                    var payload = await Web3.Wallet.SignMessage(msg);
                    profile = await KartApi.VerifySignedMessage(c.nonce, account.PublicKey.Key, payload);
                }
                SetProfile(profile);
                Status(Profile.skrName ?? Short(Profile.wallet));
                _ = SettlePurchases();   // payments this wallet made under a session that has since expired
                KartApi.Event("wallet_login", new { seeker = Profile.isSeeker });
                if (RaceSession.Instance != null) await RaceSession.Instance.ClaimAfterLogin();
                return true;
            }
            catch (OperationInFlightException) { return false; }   // debounced; should not happen
            catch (Exception e) { Debug.LogWarning("[wallet] login failed: " + e); Status("Sign-in failed. Try again."); return false; }
            finally { Busy = outer; }
        }

        public async void Logout()
        {
            try { await Web3.Instance.DisconnectWalletAdapter(); } catch { }
            KartApi.SessionToken = ""; SetProfile(null); Status("Not connected");
        }

        /// Results screen, only when the server said isPersonalBest: one mainnet Memo the player signs.
        public async void MintPersonalBestReceipt()
        {
            var memo = RaceSession.Instance?.PbMemo;
            if (Busy || !Connected || string.IsNullOrEmpty(memo)) return;
            Busy = true;
            try
            {
                if (!await HasSol(0)) { Status("Add a little SOL (≥0.001) to record on-chain"); return; }
                var tx = await BuildTx();
                tx.Add(MemoProgram.NewMemoV2(memo, Web3.Wallet.Account.PublicKey));
                var sig = await SignAndSend(tx);
                if (sig == null) return;
                Status("Recorded on-chain ✓");
                await KartApi.ConfirmTx(sig, RaceSession.Instance.RaceId);
                KartApi.Event("memo_pb", new { sig });
            }
            catch (Exception e) { Debug.LogWarning("[wallet] memo failed: " + e); Status("Could not record. Try again."); }
            finally { Busy = false; }
        }

        // ---- the one payment path ----

        /// A transfer of `amount` base units of SOL or SKR to the treasury with `memo`, signed in the wallet.
        /// Returns the signature, or null when nothing was sent (declined, no funds, offline). Signs the player in first if needed.
        public async Awaitable<string> Pay(string memo, ulong amount, string currency, string what, Action beforeSigning = null)
        {
            if (Busy) return null;
            Busy = true;
            try
            {
                var cfg = await KartApi.Config();
                if (cfg == null || !cfg.CanPay) { Status("Payments are offline right now"); return null; }
                if (!SignedIn && !await LoginAsync()) return null;
                var me = Web3.Wallet.Account.PublicKey;
                var tx = await BuildTx();
                if (currency == "SOL")
                {
                    if (!await HasSol(amount)) { Status("Not enough SOL for this"); return null; }
                    tx.Add(SystemProgram.Transfer(me, new PublicKey(cfg.treasuryOwner), amount));
                }
                else
                {
                    if (!await HasSol(0)) { Status("Add a little SOL for the network fee"); return null; }
                    if (!await HasSkr(cfg.skrMint, amount)) { Status("Not enough SKR for this"); return null; }
                    var mint = new PublicKey(cfg.skrMint);
                    var src = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(me, mint);
                    tx.Add(TokenProgram.TransferChecked(src, new PublicKey(cfg.treasuryAta), amount, 6, me, mint));
                }
                tx.Add(MemoProgram.NewMemoV2(memo, me));
                beforeSigning?.Invoke();                    // the last moment before money may move
                var sig = await SignAndSend(tx);
                if (sig != null) KartApi.Event("pay", new { what, currency, amount, sig });
                return sig;
            }
            catch (Exception e) { Debug.LogWarning("[wallet] payment failed: " + e); Status("Payment failed. Nothing was charged unless the wallet showed success."); return null; }
            finally { Busy = false; }
        }

        /// Shop: pay the catalog price, then let the Worker verify it landed and grant it. The signature is written down
        /// before the first ask (PendingPurchases), so a payment the Worker has not seen yet keeps being asked about: for
        /// a few minutes now, then at every launch and sign-in, until it is granted.
        public async Awaitable<bool> Buy(string sku, string currency)
        {
            var cfg = await KartApi.Config(maxAgeSeconds: 300);   // the Worker accepts 90% of today's SKR price, not the session's
            if (cfg == null) { Status("Couldn't load today's prices. Check your connection."); return false; }
            var item = cfg.Item(sku);
            if (item == null) { Status("Not for sale right now"); return false; }
            if (!item.price.TryGetValue(currency, out var priceStr) || string.IsNullOrEmpty(priceStr)) { Status("This one is sold for SKR only"); return false; }
            var orderId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var sig = await Pay($"kc:buy:{sku}:{orderId}", ulong.Parse(priceStr), currency, sku);
            if (sig == null) return false;
            var paid = new PendingPurchases.Entry { sig = sig, sku = sku, orderId = orderId, currency = currency, wallet = KartApi.SessionWallet };
            PendingPurchases.Add(paid);
            Status("Verifying…");
            var outcome = PendingPurchases.Outcome.Pending;
            for (int i = 0; i < 10 && outcome == PendingPurchases.Outcome.Pending; i++)
            {
                if (i > 0) await Awaitable.WaitForSecondsAsync(2f);
                outcome = await PendingPurchases.Verify(paid);
            }
            if (outcome == PendingPurchases.Outcome.Refused) { Status("That payment did not match the price. Contact support with your wallet receipt."); return false; }
            if (outcome == PendingPurchases.Outcome.Pending)
            {
                Status("Paid. Still confirming: it unlocks by itself once the network confirms.");
                _ = SettlePurchases(rounds: 12);
                return false;
            }
            try { SetProfile(await KartApi.Me()); } catch { }
            Status("Unlocked ✓");
            KartApi.Event("bought", new { sku, currency, sig });
            return true;
        }

        /// Payments the Worker has not granted yet: asked about `rounds` times, 15 s apart, while any are waiting.
        async Awaitable SettlePurchases(int rounds = 1)
        {
            for (int i = 0; i < rounds && PendingPurchases.Any; i++)
            {
                if (i > 0) await Awaitable.WaitForSecondsAsync(15f);
                if (this == null) return;   // the menu was left; the next one asks again
                if (await PendingPurchases.Flush() == 0 || this == null) continue;
                try { SetProfile(await KartApi.Me()); } catch { }
                Status("Unlocked ✓");
            }
        }

        /// Lobby: pay the room's entry fee and hand the signature to the room, which verifies it on-chain. The room is
        /// told first that we are paying (it holds the seat while the wallet app is open), and the signature is written
        /// down before it is sent, so it reaches the room however the socket or the seat fared meanwhile.
        public async Awaitable<bool> PayEntry(RoomClient.RoomState room)
        {
            if (room == null || !room.IsPaid) return false;
            string attempt = null;
            var sig = await Pay($"kc:entry:{room.code}:{room.round}", ulong.Parse(room.fee), room.currency, "entry", () =>
            {
                attempt = PendingEntries.Attempt(room.code, room.round, KartApi.SessionWallet);
                RoomClient.Instance?.Send(new { t = "paying" });
            });
            if (sig == null)
            {
                // nothing was sent (declined, no funds, offline): release the seat hold and forget the attempt
                PendingEntries.Forget(attempt);
                if (attempt != null) RoomClient.Instance?.Send(new { t = "paying", off = true });
                return false;
            }
            PendingEntries.Add(room.code, room.round, sig, KartApi.SessionWallet);
            PendingEntries.Forget(attempt);
            _ = PendingEntries.Deliver(sig);
            Status("Entry sent · verifying on-chain…");
            return true;
        }

        // ---- helpers ----
        async Awaitable<bool> HasSol(ulong extra)
        {
            var bal = await Web3.Rpc.GetBalanceAsync(Web3.Wallet.Account.PublicKey, Commitment.Confirmed);
            return (long)(bal.Result?.Value ?? 0) >= minLamportsForTx + (long)extra;
        }

        async Awaitable<bool> HasSkr(string mint, ulong amount)
        {
            try
            {
                var accs = await Web3.Rpc.GetTokenAccountsByOwnerAsync(Web3.Wallet.Account.PublicKey.Key, mint, null, Commitment.Confirmed);
                ulong total = 0;
                if (accs.Result?.Value != null)
                    foreach (var a in accs.Result.Value) total += a.Account?.Data?.Parsed?.Info?.TokenAmount?.AmountUlong ?? 0;
                return total >= amount;
            }
            catch (Exception e) { Debug.LogWarning("[wallet] skr balance: " + e.Message); return true; }   // let the wallet be the judge
        }

        async Awaitable<Transaction> BuildTx()
        {
            return new Transaction
            {
                // Fresh every time: a blockhash lives ~60–90 s and the wallet may sit open for most of that.
                RecentBlockHash = await Web3.Wallet.GetBlockHash(Commitment.Confirmed, useCache: false),
                FeePayer = Web3.Wallet.Account.PublicKey,
                Instructions = new List<TransactionInstruction>(),
                Signatures = new List<SignaturePubKeyPair>(),
            };
        }

        async Awaitable<string> SignAndSend(Transaction tx)
        {
            Status("Approve in your wallet…");
            var adapter = Web3.Wallet as SolanaWalletAdapter;
            if (adapter != null)
            {
                switch (await adapter.SignAndSendTransactions(new[] { tx }))
                {
                    case SignAndSendTxResult.Success s: return Encoders.Base58.EncodeData(s.Signatures[0]);
                    case SignAndSendTxResult.UserDeclined: Status("Cancelled"); return null;
                    case SignAndSendTxResult.Failed f: Status($"Wallet error {f.Code}"); return null;
                    default: break;   // NotSupported / NotSubmitted → sign + our RPC (Backpack path)
                }
            }
            var signed = await Web3.Wallet.SignTransaction(tx);
            var res = await Web3.Rpc.SendTransactionAsync(Convert.ToBase64String(signed.Serialize()), skipPreflight: false, preFlightCommitment: Commitment.Confirmed);
            if (!res.WasSuccessful) { Status("Send failed: " + res.Reason); return null; }
            return res.Result;
        }

        void Status(string s) { LastStatus = s; Debug.Log("[wallet] " + s); onStatus?.Invoke(s); }
        public static string Short(string w) => string.IsNullOrEmpty(w) || w.Length < 8 ? w : $"{w[..4]}…{w[^4..]}";

        /// "0.05 SOL" / "25 SKR" from base units. Money reads in SOL or SKR everywhere; there is no dollar figure.
        /// ponytail: `withUsd` is ignored and stays only so the callers that still pass it compile.
        public static string Amount(string baseUnits, string currency, bool withUsd = false)
        {
            if (!ulong.TryParse(baseUnits, out var b)) return "";
            double v = currency == "SOL" ? b / 1e9 : b / 1e6;
            return currency == "SOL" ? $"{v:0.###} SOL" : $"{v:0.##} SKR";
        }
    }
}
