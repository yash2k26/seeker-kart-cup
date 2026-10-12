using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Types;
using UnityEditor;
using UnityEngine;

/// The wallet's RPC calls through the Worker's /rpc, as the SDK makes them (12 Oct 2026: a shop payment went to the wallet
/// with no blockhash, so the wallet never showed it). Prints each call's success and raw reply.   tools/unity.sh RpcCheck.Run
public static class RpcCheck
{
    public static async void Run()
    {
        var rpc = ClientFactory.GetClient("https://kartcup.bharadwaj465.workers.dev/rpc");
        const string me = "uP7ZfsuXEcUq7LM9jSPeMDnhbS8Fqq4FHbhwSoVWXhQz";
        var bh = await rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        Debug.Log($"[rpc] getLatestBlockhash ok={bh.WasSuccessful} hash={bh.Result?.Value?.Blockhash} reason={bh.Reason} raw={bh.RawRpcResponse}");
        var bal = await rpc.GetBalanceAsync(me, Commitment.Confirmed);
        Debug.Log($"[rpc] getBalance ok={bal.WasSuccessful} lamports={bal.Result?.Value} reason={bal.Reason}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
