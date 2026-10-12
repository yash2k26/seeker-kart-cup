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
    /// Every wallet beat: Seed Vault sign-in (SIWS), a mainnet Memo on a personal best, the shop's payment path (SOL or
    /// SKR to the treasury, plus a memo the Worker verifies) and a paid room's stake (into the room's escrow account).
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

        void Awake()
        {
            Instance = this;
            if (FindAnyObjectByType<Web3>() == null) Debug.LogError("[wallet] no Web3 in this scene: sign-in and payments cannot work");
        }

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
                    await CloudSave.Pull(Profile.wallet);   // a newer save from another phone, or this phone's changes up
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
        /// SIGN IN. A tap never goes unanswered (12 Oct 2026, the user: "i click on sign in, and the wallet isnt opening"):
        /// while an attempt waits on the wallet it says so, and one stuck past the SDK's own 90 s wait gives way to a new one.
        public void Login()
        {
            if (Busy && Time.unscaledTime - loginAt < 100f) { Status(busyNote); return; }
            if (Busy) Busy = false;
            _ = LoginAsync();
        }
        float loginAt = -999f;
        string busyNote = "Waiting for your wallet…";

        /// What a failed sign-in tells the player: what happened and what to do, never only "failed".
        static string Why(Exception e)
        {
            var m = e.Message ?? "";
            if (e.GetType().Name == "MwaNoWalletException") return "No Solana wallet on this phone. Install Phantom or Solflare, then sign in.";
            if (m.Contains("returned to app before") || m.Contains("WS closed unexpectedly")) return "Sign-in cancelled. Tap SIGN IN to try again.";
            if (m.Contains("Timeout or cancelled")) return "Your wallet didn't answer. Open it once, then tap SIGN IN.";
            if (m.Contains("declined") || m.Contains("rejected") || m.Contains("NotAuthorized") || m.Contains("authorization request failed")) return "Sign-in declined in the wallet.";
            if (e is KartApi.ApiException a && a.Status != 0) return "Kartel's server didn't accept the sign-in. Tap SIGN IN to try again.";
            if (e is KartApi.ApiException || m.Contains("Cannot resolve") || m.Contains("Cannot connect") || m.Contains("timed out")) return "Can't reach Kartel's server. Check your connection.";
            return "Sign-in failed. Tap SIGN IN to try again.";
        }

        public async Awaitable<bool> LoginAsync()
        {
            bool outer = Busy; Busy = true; loginAt = Time.unscaledTime;
            try
            {
                Status("Opening wallet…"); busyNote = "Waiting for your wallet…";
                var c = await KartApi.Challenge();
                var (account, siws) = await Web3.Instance.LoginWalletAdapter(new SignInPayload
                {
                    Domain = c.domain, Uri = c.uri, Statement = c.statement, Nonce = c.nonce,
                    IssuedAt = c.issuedAt, ExpirationTime = c.expirationTime, Version = c.version, ChainId = c.chainId,
                });
                if (account == null) { Status("Cancelled"); return false; }
                Status("Signing you in…"); busyNote = "Still signing you in…";   // the wallet is done; the server check can take a moment

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
                Status("Signed in as " + (Profile.skrName ?? Short(Profile.wallet)));
                _ = SettlePurchases();   // payments this wallet made under a session that has since expired
                KartApi.Event("wallet_login", new { seeker = Profile.isSeeker });
                if (RaceSession.Instance != null) await RaceSession.Instance.ClaimAfterLogin();
                await CloudSave.Pull(Profile.wallet);   // the wallet's progress comes back (a reinstall), or this phone's goes up
                return true;
            }
            catch (OperationInFlightException) { Status("Your wallet is still open: finish there, or close it and try again."); return false; }
            catch (Exception e) { Debug.LogWarning("[wallet] login failed: " + e); Status(Why(e)); return false; }
            finally { Busy = outer; }
        }

        public async void Logout()
        {
            try { await Web3.Instance.DisconnectWalletAdapter(); } catch { }
            KartApi.SessionToken = ""; CloudSave.SignedOut(); SetProfile(null); Status("Not connected");
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
            catch (Exception e) { Debug.LogWarning("[wallet] memo failed: " + e); Status(e is NoBlockhashException ? SolanaBusy : "Could not record. Try again."); }
            finally { Busy = false; }
        }

        // ---- the one payment path ----

        /// A transfer of `amount` base units of SOL, SKR or ORE to the treasury with `memo`, signed in the wallet.
        /// Returns the signature, or null when nothing was sent (declined, no funds, offline). Signs the player in first if needed.
        public async Awaitable<string> Pay(string memo, ulong amount, string currency, string what)
        {
            if (Busy) { Status("Your wallet is busy with something else. Try again in a moment."); return null; }
            Busy = true;
            try
            {
                var cfg = await KartApi.Config();
                if (cfg == null || !cfg.CanPay) { Status("Payments are offline right now"); return null; }
                if (!SignedIn && !await LoginAsync()) return null;
                var me = Web3.Wallet.Account.PublicKey;
                Status("Checking your balance…");
                var tx = await BuildTx();
                if (currency == "SOL")
                {
                    if (!await HasSol(amount)) { Status("Not enough SOL for this"); return null; }
                    tx.Add(SystemProgram.Transfer(me, new PublicKey(cfg.treasuryOwner), amount));
                }
                else
                {
                    // SKR and ORE: the same token transfer with a different mint, destination and decimals
                    bool ore = currency == "ORE";
                    string mintStr = ore ? cfg.oreMint : cfg.skrMint, dest = ore ? cfg.treasuryOreAta : cfg.treasuryAta;
                    if (string.IsNullOrEmpty(mintStr) || string.IsNullOrEmpty(dest)) { Status($"{currency} payments are offline right now"); return null; }
                    if (!await HasSol(0)) { Status("Add a little SOL for the network fee"); return null; }
                    if (!await HasToken(mintStr, amount)) { Status($"Not enough {currency} for this"); return null; }
                    var mint = new PublicKey(mintStr);
                    var src = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(me, mint);
                    // The treasury's ORE account may not exist yet: create it if missing (a no-op once it does; the first buyer pays
                    // its rent, ~0.002 SOL, once). SKR's account has existed since the shop opened.
                    if (ore) tx.Add(CreateTokenAccountIdempotent(me, new PublicKey(cfg.treasuryOwner), mint));
                    tx.Add(TokenProgram.TransferChecked(src, new PublicKey(dest), amount, ore ? 11 : 6, me, mint));
                }
                tx.Add(MemoProgram.NewMemoV2(memo, me));
                var sig = await SignAndSend(tx);
                if (sig != null) KartApi.Event("pay", new { what, currency, amount, sig });
                return sig;
            }
            catch (Exception e) { Debug.LogWarning("[wallet] payment failed: " + e); Status(e is NoBlockhashException ? SolanaBusy : "Payment failed. Nothing was charged unless the wallet showed success."); return null; }
            finally { Busy = false; }
        }

        /// Shop: pay the catalog price, then let the Worker verify it landed and grant it. The signature is written down
        /// before the first ask (PendingPurchases), so a payment the Worker has not seen yet keeps being asked about: for
        /// a few minutes now, then at every launch and sign-in, until it is granted.
        /// The last Buy paid but the network had not confirmed it yet: it unlocks by itself (PendingPurchases).
        public bool LastPending { get; private set; }

        public async Awaitable<bool> Buy(string sku, string currency)
        {
            LastPending = false;
            Status("Getting today's price…");
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
            Status("Confirming your payment on Solana…");
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
                LastPending = true;
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

        /// Lobby: stake `amount` (base units) into the room's escrow, the program's `join` signed in the wallet. The
        /// stake goes to the room's own account on the escrow program, never to the treasury; the room reads it from the
        /// chain (the signature it is sent only tells it to look now). The room holds the seat while the wallet is open.
        public async Awaitable<bool> Stake(RoomClient.RoomState room, ulong amount)
        {
            if (Busy || room == null || !room.IsPaid || room.escrow == null) return false;
            Busy = true;
            bool holding = false;
            try
            {
                var cfg = await KartApi.Config();
                if (cfg == null || string.IsNullOrEmpty(cfg.escrowProgram)) { Status("Paid races are offline right now"); return false; }
                if (!SignedIn && !await LoginAsync()) return false;
                var program = new PublicKey(cfg.escrowProgram);
                var roomKey = Escrow.RoomAddress(program, room.code, room.round);
                // Only ever into the account derived here from the room's code and round, whatever an address elsewhere says.
                if (roomKey.Key != room.escrow.address) { Status("This room's prize pool did not check out. Nothing was sent."); return false; }
                var me = Web3.Wallet.Account.PublicKey;
                PublicKey mint = room.currency == "SKR" ? new PublicKey(cfg.skrMint) : null;
                if (mint == null && !await HasSol(amount)) { Status("Not enough SOL for this stake"); return false; }
                if (mint != null && !await HasSol(0)) { Status("Add a little SOL for the network fee"); return false; }
                if (mint != null && !await HasToken(cfg.skrMint, amount)) { Status("Not enough SKR for this stake"); return false; }
                var tx = await BuildTx();
                tx.Add(Escrow.Join(program, roomKey, me, mint, amount));
                RoomClient.Instance?.Send(new { t = "paying" });
                holding = true;
                var sig = await SignAndSend(tx);
                if (sig == null) return false;
                holding = false;
                RoomClient.Instance?.Send(new { t = "pay", signature = sig });
                KartApi.Event("stake", new { room = room.code, round = room.round, amount, currency = room.currency, sig });
                Status("Stake sent · the room is reading it from the chain…");
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[wallet] stake failed: " + e); Status(e is NoBlockhashException ? SolanaBusy : "Stake failed. Nothing was taken unless the wallet showed success."); return false; }
            finally
            {
                if (holding) RoomClient.Instance?.Send(new { t = "paying", off = true });   // declined or failed: let the seat go back to normal
                Busy = false;
            }
        }

        // ---- helpers ----
        /// Enough SOL for `extra` plus the fees? 12 Oct 2026: a balance the node would not give was read as 0, and a wallet
        /// holding 16 SOL was told "Not enough SOL" for 0.05. Asked three times; never read, the wallet judges (it shows the
        /// real balance and refuses an unaffordable send), so a lookup failure is never "not enough".
        async Awaitable<bool> HasSol(ulong extra)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) await Awaitable.WaitForSecondsAsync(0.5f * i);
                try
                {
                    var bal = await Web3.Rpc.GetBalanceAsync(Web3.Wallet.Account.PublicKey, Commitment.Confirmed);
                    if (bal != null && bal.WasSuccessful && bal.Result != null) return (long)bal.Result.Value >= minLamportsForTx + (long)extra;
                    Debug.LogWarning("[wallet] balance: " + bal?.Reason);
                }
                catch (Exception e) { Debug.LogWarning("[wallet] balance: " + e.Message); }
            }
            return true;
        }

        async Awaitable<bool> HasToken(string mint, ulong amount)
        {
            try
            {
                var accs = await Web3.Rpc.GetTokenAccountsByOwnerAsync(Web3.Wallet.Account.PublicKey.Key, mint, null, Commitment.Confirmed);
                if (accs == null || !accs.WasSuccessful) { Debug.LogWarning("[wallet] token balance: " + accs?.Reason); return true; }   // unread is not "not enough"
                ulong total = 0;
                if (accs.Result?.Value != null)
                    foreach (var a in accs.Result.Value) total += a.Account?.Data?.Parsed?.Info?.TokenAmount?.AmountUlong ?? 0;
                return total >= amount;
            }
            catch (Exception e) { Debug.LogWarning("[wallet] token balance: " + e.Message); return true; }   // let the wallet be the judge
        }

        /// Associated Token Account program, CreateIdempotent (instruction 1); the SDK only wraps Create, which fails if it exists.
        static TransactionInstruction CreateTokenAccountIdempotent(PublicKey payer, PublicKey owner, PublicKey mint) => new()
        {
            ProgramId = AssociatedTokenAccountProgram.ProgramIdKey.KeyBytes,
            Keys = new List<AccountMeta>
            {
                AccountMeta.Writable(payer, true),
                AccountMeta.Writable(AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint), false),
                AccountMeta.ReadOnly(owner, false),
                AccountMeta.ReadOnly(mint, false),
                AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                AccountMeta.ReadOnly(TokenProgram.ProgramIdKey, false),
            },
            Data = new byte[] { 1 },
        };

        /// Thrown before the wallet opens when no Solana node gave a blockhash: there is nothing the wallet could sign.
        sealed class NoBlockhashException : Exception { public NoBlockhashException() : base("no recent blockhash") { } }
        const string SolanaBusy = "Solana is busy right now. Nothing was charged: try again in a moment.";

        async Awaitable<Transaction> BuildTx()
        {
            // Fresh every time: a blockhash lives ~60–90 s and the wallet may sit open for most of that. 12 Oct 2026: a
            // rate-limited node answered nothing, the transaction went to the wallet without one, and the wallet opened with
            // nothing to sign. So: ask again, and never open the wallet without one.
            string hash = null;
            for (int i = 0; i < 4 && string.IsNullOrEmpty(hash); i++)
            {
                if (i > 0) await Awaitable.WaitForSecondsAsync(0.6f * i);
                try { hash = await Web3.Wallet.GetBlockHash(Commitment.Confirmed, useCache: false); }
                catch (Exception e) { Debug.LogWarning("[wallet] blockhash: " + e.Message); }
            }
            if (string.IsNullOrEmpty(hash)) throw new NoBlockhashException();
            return new Transaction
            {
                RecentBlockHash = hash,
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
                    case SignAndSendTxResult.Failed f: Debug.LogWarning($"[wallet] wallet failed: {f.Code} {f.Message}"); Status("The wallet couldn't send it. Nothing was charged: try again."); return null;
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
            double v = currency == "SOL" ? b / 1e9 : currency == "ORE" ? b / 1e11 : b / 1e6;
            return currency == "SOL" ? $"{v:0.###} SOL" : currency == "ORE" ? $"{v:0.###} ORE" : $"{v:0.##} SKR";
        }
    }
}
