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
    /// The three wallet-only beats: Seed Vault sign-in (SIWS), a mainnet Memo on a personal best, an SKR paint job.
    /// Rules from research/14: one MWA op at a time (debounce), prompts only from menus, rent guard before any tx,
    /// mainnet from day one, silent reconnect via cached auth token + our HMAC session.
    public sealed class WalletFlow : MonoBehaviour
    {
        public static WalletFlow Instance { get; private set; }

        [SerializeField] string skrMint = "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3";
        [SerializeField] string treasuryOwner = "";           // TODO: treasury wallet (owner); its SKR ATA is pre-created once
        [SerializeField] long minLamportsForTx = 1_000_000;   // rent floor is ~650k after SIMD-0437; Seed Vault injects ComputeBudget ixs
        [SerializeField] UnityEvent<string> onStatus = new();         // UI text
        [SerializeField] UnityEvent<KartApi.Profile> onLoggedIn = new();

        public KartApi.Profile Profile { get; private set; }
        public bool Busy { get; private set; }
        public bool Connected => Web3.Wallet?.Account != null;

        void Awake() => Instance = this;

        async void Start()
        {
            // Returning player: silent reconnect (no prompt) + our session; no probe with Login() when nothing is cached.
            try
            {
                if (await MwaSession.HasCachedSession() && KartApi.HasSession)
                {
                    await Web3.Instance.LoginWalletAdapter();
                    Profile = await KartApi.Me();
                    Status(Profile.skrName ?? Short(Profile.wallet));   // label otherwise stays "Not connected" after a silent reconnect
                    onLoggedIn?.Invoke(Profile);
                    KartApi.Event("wallet_reconnect");
                }
            }
            catch (Exception e) { Debug.LogWarning("[wallet] silent reconnect skipped: " + e.Message); }
        }

        /// Button: "Save to leaderboard" / "Connect". One prompt: connect + Sign-In-With-Solana.
        public async void Login()
        {
            if (Busy) return; Busy = true;
            try
            {
                Status("Opening wallet…");
                var c = await KartApi.Challenge();
                var (account, siws) = await Web3.Instance.LoginWalletAdapter(new SignInPayload
                {
                    Domain = c.domain, Uri = c.uri, Statement = c.statement, Nonce = c.nonce,
                    IssuedAt = c.issuedAt, ExpirationTime = c.expirationTime, Version = c.version, ChainId = c.chainId,
                });
                if (account == null) { Status("Cancelled"); return; }

                if (siws != null && !string.IsNullOrEmpty(siws.Signature))
                    Profile = await KartApi.VerifySiws(c.nonce, siws.Address, siws.SignedMessage, siws.Signature);
                else
                {
                    // Wallet without native SIWS: SignMessage returns message||signature; the Worker checks the last 64 bytes.
                    var msg = Encoding.UTF8.GetBytes($"{c.domain} wants you to sign in with your Solana account:\n{account.PublicKey}\n\n{c.statement}\n\nNonce: {c.nonce}");
                    var payload = await Web3.Wallet.SignMessage(msg);
                    Profile = await KartApi.VerifySignedMessage(c.nonce, account.PublicKey.Key, payload);
                }
                Status(Profile.skrName ?? Short(Profile.wallet));
                onLoggedIn?.Invoke(Profile);
                KartApi.Event("wallet_login", new { seeker = Profile.isSeeker });
                if (RaceSession.Instance != null) await RaceSession.Instance.ClaimAfterLogin();
            }
            catch (OperationInFlightException) { /* debounced; should not happen */ }
            catch (Exception e) { Debug.LogWarning("[wallet] login failed: " + e); Status("Sign-in failed. Try again."); }
            finally { Busy = false; }
        }

        public async void Logout()
        {
            try { await Web3.Instance.DisconnectWalletAdapter(); } catch { }
            KartApi.SessionToken = ""; Profile = null; Status("Not connected");
        }

        /// Results screen, only when the server said isPersonalBest: one mainnet Memo the player signs.
        public async void MintPersonalBestReceipt()
        {
            var memo = RaceSession.Instance?.PbMemo;
            if (Busy || !Connected || string.IsNullOrEmpty(memo)) return;
            Busy = true;
            try
            {
                if (!await HasFunds()) { Status("Add a little SOL (≥0.001) to record on-chain"); return; }
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

        /// Garage: fixed-price paint job paid in SKR (legacy SPL token, 6 decimals) + memo, verified by the Worker.
        public async void BuyPaint(string sku, ulong priceBaseUnits)
        {
            if (Busy || !Connected) return;
            Busy = true;
            try
            {
                if (!await HasFunds()) { Status("Add a little SOL for the network fee"); return; }
                var me = Web3.Wallet.Account.PublicKey;
                var mint = new PublicKey(skrMint);
                var treasury = new PublicKey(treasuryOwner);
                var src = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(me, mint);
                var dst = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(treasury, mint);
                var orderId = Guid.NewGuid().ToString("N").Substring(0, 12);

                var tx = await BuildTx();
                tx.Add(TokenProgram.TransferChecked(src, dst, priceBaseUnits, 6, me, mint));
                tx.Add(MemoProgram.NewMemoV2($"kc:buy:{sku}:{orderId}", me));
                var sig = await SignAndSend(tx);
                if (sig == null) return;
                Status("Verifying…");
                for (int i = 0; i < 6; i++)
                {
                    try { await KartApi.VerifyPurchase(sig, sku, orderId); break; }
                    catch (KartApi.ApiException ex) when (ex.Status == 202) { await Awaitable.WaitForSecondsAsync(2f); }
                }
                Profile = await KartApi.Me();
                onLoggedIn?.Invoke(Profile);
                Status("Paint unlocked ✓");
                KartApi.Event("paint_bought", new { sku, sig });
            }
            catch (Exception e) { Debug.LogWarning("[wallet] purchase failed: " + e); Status("Purchase failed. Nothing was charged unless the wallet showed success."); }
            finally { Busy = false; }
        }

        // ---- helpers ----
        async Awaitable<bool> HasFunds()
        {
            var bal = await Web3.Rpc.GetBalanceAsync(Web3.Wallet.Account.PublicKey, Commitment.Confirmed);
            return (long)(bal.Result?.Value ?? 0) >= minLamportsForTx;
        }

        async Awaitable<Transaction> BuildTx()
        {
            return new Transaction
            {
                RecentBlockHash = await Web3.Wallet.GetBlockHash(Commitment.Confirmed, useCache: true, maxSeconds: 30),
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

        void Status(string s) { Debug.Log("[wallet] " + s); onStatus?.Invoke(s); }
        static string Short(string w) => string.IsNullOrEmpty(w) || w.Length < 8 ? w : $"{w[..4]}…{w[^4..]}";
    }
}
