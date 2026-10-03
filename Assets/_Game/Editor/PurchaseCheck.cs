using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using KartCup;
using UnityEditor;
using UnityEngine;

// The shop's money path against a stand-in Worker on localhost (no chain, no wallet): a payment the Worker has not
// seen land (202) stays waiting and credits nothing, the next ask grants it and credits the pack exactly once, a
// refusal is dropped, another wallet's payment waits for its own session, and a payment never prices from a quote
// older than it asked for. The player's own prefs are put back afterwards.
// Run: ./tools/unity.sh PurchaseCheck.Run   (exits itself)
public static class PurchaseCheck
{
    const string Base = "http://127.0.0.1:8799";
    static readonly string[] Keys = { "kc.session", "kc.purchases", "kc.loadout" };   // strings; the rocket count is the one int
    const string Rockets = "kc.item.rocket";
    static HttpListener listener;
    static int failures, verifyCalls, slowAsks, configCalls;
    static double deadline;
    static Action restore;
    static bool done;

    public static void Run()
    {
        deadline = EditorApplication.timeSinceStartup + 60;
        EditorApplication.update += Watchdog;
        Begin();
    }

    static void Watchdog() { if (EditorApplication.timeSinceStartup > deadline) { Check("finished within 60 s", false); Finish(); } }

    static async void Begin()
    {
        var saved = Array.ConvertAll(Keys, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
        int? savedRockets = PlayerPrefs.HasKey(Rockets) ? PlayerPrefs.GetInt(Rockets) : null;
        var savedBase = KartApi.BaseUrl;
        restore = () =>
        {
            KartApi.BaseUrl = savedBase;
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            if (savedRockets == null) PlayerPrefs.DeleteKey(Rockets); else PlayerPrefs.SetInt(Rockets, savedRockets.Value);
            PlayerPrefs.Save();
        };
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(Base + "/");
            listener.Start();
            new Thread(() => { while (listener.IsListening) { try { Serve(listener.GetContext()); } catch { } } }) { IsBackground = true }.Start();

            KartApi.BaseUrl = Base;
            KartApi.SessionToken = Token("WalletA");
            PlayerPrefs.DeleteKey("kc.purchases"); PlayerPrefs.SetInt("kc.item.rocket", 0); PlayerPrefs.SetString("kc.loadout", "");

            var slow = Entry("sig-slow", "WalletA");
            PendingPurchases.Add(slow);
            Check("a 202 is still pending", await PendingPurchases.Verify(slow) == PendingPurchases.Outcome.Pending);
            Check("nothing credited while pending", Inventory.Count("rocket") == 0);
            Check("the payment stays written down", PendingPurchases.Any);
            Check("the next ask grants it", await PendingPurchases.Verify(slow) == PendingPurchases.Outcome.Granted);
            Check("the pack is credited", Inventory.Count("rocket") == 3 && Inventory.Loadout == "rocket");
            Check("a repeat grant credits nothing more", await PendingPurchases.Verify(slow) == PendingPurchases.Outcome.Granted && Inventory.Count("rocket") == 3);

            var bad = Entry("sig-bad", "WalletA");
            PendingPurchases.Add(bad);
            Check("a refusal is final", await PendingPurchases.Verify(bad) == PendingPurchases.Outcome.Refused && !PendingPurchases.Any);

            PendingPurchases.Add(Entry("sig-other", "WalletB"));
            int asked = verifyCalls;
            Check("another wallet's payment is not asked about", await PendingPurchases.Flush() == 0 && verifyCalls == asked && PendingPurchases.Any);

            await KartApi.Config(); await KartApi.Config(); await KartApi.Config(maxAgeSeconds: 300);
            Check("the quote is fetched once and reused", configCalls == 1);
            await KartApi.Config(maxAgeSeconds: 0);
            Check("a payment's max age refetches it", configCalls == 2);
            listener.Stop();
            Check("no stale quote when the Worker is unreachable", await KartApi.Config(maxAgeSeconds: 0) == null && KartApi.Cfg != null);
        }
        catch (Exception e) { Check("no exception: " + e, false); }
        Finish();
    }

    static void Serve(HttpListenerContext c)
    {
        string path = c.Request.Url.AbsolutePath, body = new StreamReader(c.Request.InputStream).ReadToEnd();
        int status = 200; string json = "{}";
        if (path == "/v1/config") { Interlocked.Increment(ref configCalls); json = "{\"treasuryOwner\":\"T\",\"catalog\":[]}"; }
        if (path == "/v1/purchase/verify")
        {
            Interlocked.Increment(ref verifyCalls);
            if (body.Contains("\"sig-bad\"")) { status = 400; json = "{\"error\":\"payment did not match the entry\"}"; }
            else if (body.Contains("\"sig-slow\"") && Interlocked.Increment(ref slowAsks) == 1) { status = 202; json = "{\"pending\":true}"; }
            else json = "{\"granted\":[\"item.rocket3\"]}";
        }
        var bytes = Encoding.UTF8.GetBytes(json);
        c.Response.StatusCode = status; c.Response.ContentType = "application/json";
        c.Response.OutputStream.Write(bytes, 0, bytes.Length);
        c.Response.Close();
    }

    static PendingPurchases.Entry Entry(string sig, string wallet) => new() { sig = sig, sku = "item.rocket3", orderId = "o-" + sig, currency = "SOL", wallet = wallet };

    /// A session token's body as KartApi.SessionWallet reads it; the stand-in never checks the signature half.
    static string Token(string wallet) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"w\":\"{wallet}\"}}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".x";

    static void Check(string what, bool ok)
    {
        if (!ok) failures++;
        Debug.Log($"[purchasecheck] {(ok ? "ok  " : "FAIL")} {what}");
    }

    static void Finish()
    {
        if (done) return;
        done = true;
        EditorApplication.update -= Watchdog;
        try { restore?.Invoke(); } catch (Exception e) { Debug.LogWarning("[purchasecheck] restore: " + e.Message); }
        try { if (listener?.IsListening == true) listener.Stop(); } catch { }
        Debug.Log(failures == 0 ? "[purchasecheck] PASS — all checks" : $"[purchasecheck] {failures} FAILURE(S)");
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
}
