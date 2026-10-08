using System.IO;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;
using KartCup;
using Newtonsoft.Json.Linq;
using Solana.Unity.Wallet;
using UnityEditor;
using UnityEngine;

// The phone's side of the escrow, without a phone or a chain: the C# fair shares against the vectors the program and
// the Worker are tested with, and the `join` the wallet signs against the program's IDL and against the encoding the
// Worker's smoke test sends to a real local chain (worker/scripts/smoke-escrow.ts: every stake there lands).
// Run: ./tools/unity.sh EscrowCheck.Run
public static class EscrowCheck
{
    static int failures;

    static void Check(string what, bool ok, string extra = "")
    {
        if (!ok) failures++;
        Debug.Log($"[escrowcheck] {(ok ? "ok  " : "FAIL")} {what}{(ok ? "" : "  " + extra)}");
    }

    public static void Run()
    {
        failures = 0;
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // 1. fair shares: the program's own vectors
        var vectors = JArray.Parse(File.ReadAllText(Path.Combine(root, "escrow/vectors/fair-shares.json")));
        foreach (var v in vectors)
        {
            var stakes = v["stakes"].Select(x => BigInteger.Parse((string)x)).ToList();
            var rank = v["rank"].Select(x => (int)x).ToList();
            var got = FairShares.Split(stakes, rank, (int)v["cutBps"]);
            if (v["error"] != null)
            {
                // the C# copy previews only: it declines what the program refuses (an overflowing sum cannot be staked)
                if ((string)v["error"] != "overflow") Check($"{v["name"]}: declined", got == null);
                continue;
            }
            var want = v["payouts"].Select(x => BigInteger.Parse((string)x)).ToList();
            Check($"{v["name"]}", got != null && got.Value.payouts.SequenceEqual(want) && got.Value.house == BigInteger.Parse((string)v["house"]),
                  got == null ? "declined" : string.Join(",", got.Value.payouts));
        }

        // 2. the join: discriminator and accounts as the IDL has them
        var idl = JObject.Parse(File.ReadAllText(Path.Combine(root, "worker/src/kartel_escrow.json")));
        var join = idl["instructions"].First(i => (string)i["name"] == "join");
        var program = new PublicKey("KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv");
        var player = new PublicKey("9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin");
        var mint = new PublicKey("SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3");
        var room = Escrow.RoomAddress(program, "K7P2Q", 3);
        var sol = Escrow.Join(program, room, player, null, 150_000_000);
        var skr = Escrow.Join(program, room, player, mint, 5_000_000);
        Check("join discriminator matches the IDL", sol.Data.Take(8).SequenceEqual(join["discriminator"].Select(x => (byte)(int)x)));
        Check("join has the IDL's accounts", sol.Keys.Count == join["accounts"].Count() && skr.Keys.Count == join["accounts"].Count());
        var flags = join["accounts"].Select(a => ((bool?)a["signer"] ?? false, (bool?)a["writable"] ?? false)).ToList();
        Check("join's signer/writable flags match the IDL (token room)", skr.Keys.Select(k => (k.IsSigner, k.IsWritable)).SequenceEqual(flags));

        // 3. byte for byte what the Worker sends to the local chain (scripts/escrow-test.ts and smoke-escrow.ts)
        string Hex(byte[] b) => string.Concat(b.Select(x => x.ToString("x2")));
        string Keys(Solana.Unity.Rpc.Models.TransactionInstruction ix) => string.Join(" ", ix.Keys.Select(k => $"{k.PublicKey}:{(k.IsSigner ? "s" : "-")}{(k.IsWritable ? "w" : "-")}"));
        Check("room address for K7P2Q round 3", room.Key == "9VZvM4zXdYdKCwfkEtN3zmcavnDrcdNo9P4UucNoyEi8", room.Key);
        Check("config address", Escrow.ConfigAddress(program).Key == "6ehbR6sgAygHxJiND1U2LwFg8SY6U1Zi8Q7dwTzaNQ19");
        Check("SOL join data", Hex(sol.Data) == "ce37026a71dc11a380d1f00800000000", Hex(sol.Data));
        Check("SOL join accounts", Keys(sol) == "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin:sw 6ehbR6sgAygHxJiND1U2LwFg8SY6U1Zi8Q7dwTzaNQ19:-- 9VZvM4zXdYdKCwfkEtN3zmcavnDrcdNo9P4UucNoyEi8:-w "
            + "KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv:-- KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv:-- KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv:-- KartpVhCHvs2xSBe59LrPYUJ8qUu1PT4DegfFa1nkhv:-- 11111111111111111111111111111111:--", Keys(sol));
        Check("SKR join data", Hex(skr.Data) == "ce37026a71dc11a3404b4c0000000000", Hex(skr.Data));
        Check("SKR join accounts", Keys(skr) == "9xQeWvG816bUx9EPjHmaT23yvVM2ZWbrrpZb9PusVFin:sw 6ehbR6sgAygHxJiND1U2LwFg8SY6U1Zi8Q7dwTzaNQ19:-- 9VZvM4zXdYdKCwfkEtN3zmcavnDrcdNo9P4UucNoyEi8:-w "
            + "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3:-- EeyY86tmAzHtGyz1dSq9d6Fy5SMuYmkBK6nuUXXydUTc:-w 9cEcTNZ7J7EwKSo6SfJA72du6RcUzXYLAJ4UtxtUmNxR:-w "
            + "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA:-- 11111111111111111111111111111111:--", Keys(skr));
        Check("each round is its own room", Escrow.RoomAddress(program, "K7P2Q", 4).Key != room.Key);

        Debug.Log(failures == 0 ? "[escrowcheck] PASS — all checks" : $"[escrowcheck] {failures} FAILURE(S)");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
