using System.Collections.Generic;
using System.Linq;
using Solana.Unity.Programs;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using UnityEditor;
using UnityEngine;

/// The wallet's RPC calls through the Worker's /rpc, as the SDK makes them (12 Oct 2026: a shop payment went to the wallet
/// with no blockhash, so the wallet never showed it). Prints each call's success and raw reply.   tools/unity.sh RpcCheck.Run
public static class RpcCheck
{
    public static async void Run()
    {
        var rpc = ClientFactory.GetClient("https://kartcup.bharadwaj465.workers.dev/rpc");
        const string me = "uP7ZfsuXEcUq7LM9jSPeEZi74YJeaGuy3cbodknXhQz";
        var bh = await rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        Debug.Log($"[rpc] getLatestBlockhash ok={bh.WasSuccessful} hash={bh.Result?.Value?.Blockhash} reason={bh.Reason} raw={bh.RawRpcResponse}");
        var bal = await rpc.GetBalanceAsync(me, Commitment.Confirmed);
        Debug.Log($"[rpc] getBalance ok={bal.WasSuccessful} lamports={bal.Result?.Value} reason={bal.Reason}");
        // what a wallet receives: one zeroed signature slot per required signer (Phantom refused a transaction with none)
        var payer = new PublicKey(me);
        var tx = new Transaction { RecentBlockHash = bh.Result?.Value?.Blockhash ?? "11111111111111111111111111111111", FeePayer = payer,
                                   Instructions = new List<TransactionInstruction>(), Signatures = new List<SignaturePubKeyPair>() };
        tx.Add(ComputeBudgetProgram.SetComputeUnitLimit(80_000)); tx.Add(ComputeBudgetProgram.SetComputeUnitPrice(100_000));   // as WalletFlow.BuildTx
        tx.Add(SystemProgram.Transfer(payer, payer, 50_000_000)); tx.Add(MemoProgram.NewMemoV2("kc:buy:kart.van:test", payer));
        var forWallet = (byte[])typeof(Solana.Unity.SDK.SolanaMobileWalletAdapter).GetMethod("ForWallet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { tx });
        var back = Transaction.Deserialize(forWallet);
        bool ok = back.Signatures.Count == 1 && back.Signatures[0].PublicKey.Key == me && back.Signatures[0].Signature.All(b => b == 0);
        Debug.Log($"[rpc] wallet payload: {forWallet.Length} bytes, {back.Signatures.Count} signature slot(s), payer first and zeroed: {ok}");
        var msg = Message.Deserialize(tx.CompileMessage());
        Debug.Log($"[rpc] message: {msg.Header.RequiredSignatures} signer(s), keys {string.Join(", ", msg.AccountKeys.Select(k => k.Key[..6]))}");
        // the exact bytes a wallet gets, simulated on mainnet (no signature check): what Phantom's own check would say
        var direct = ClientFactory.GetClient("https://solana-rpc.publicnode.com");
        var sim = await direct.SimulateTransactionAsync(forWallet, sigVerify: false, commitment: Commitment.Confirmed, replaceRecentBlockhash: true);
        Debug.Log($"[rpc] simulate: ok={sim.WasSuccessful} err={(sim.Result?.Value?.Error == null ? "none" : sim.Result.Value.Error.Type.ToString())} reason={sim.Reason} logs={string.Join(" | ", sim.Result?.Value?.Logs ?? new string[0])}");
        // the in-session blockhash refresh's request, off the main thread as the wallet session runs it
        var off = await System.Threading.Tasks.Task.Run(async () =>
        {
            using var http = new System.Net.Http.HttpClient { Timeout = System.TimeSpan.FromSeconds(6) };
            var reply = await http.PostAsync("https://kartcup.bharadwaj465.workers.dev/rpc", new System.Net.Http.StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"getLatestBlockhash\",\"params\":[{\"commitment\":\"confirmed\"}]}", System.Text.Encoding.UTF8, "application/json"));
            var j = Newtonsoft.Json.Linq.JObject.Parse(await reply.Content.ReadAsStringAsync());
            return $"{(string)j.SelectToken("result.value.blockhash")} @ {(ulong?)j.SelectToken("result.context.slot")} main thread: {UnityEditorInternal.InternalEditorUtility.CurrentThreadIsMainThread()}";
        });
        Debug.Log("[rpc] off-thread blockhash: " + off);
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
