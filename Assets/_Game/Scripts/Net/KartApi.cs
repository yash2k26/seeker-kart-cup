using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace KartCup
{
    /// Thin client for the Cloudflare Worker. Awaitable-based (Unity 6), zero-dependency JSON via Newtonsoft (ships with the Solana SDK).
    public static class KartApi
    {
        public static string BaseUrl = "https://kartcup.bharadwaj465.workers.dev";   // TODO: set from a ScriptableObject/config; must be https
        const string kSession = "kc.session";
        const string kDevice = "kc.device";

        public static string SessionToken
        {
            get => PlayerPrefs.GetString(kSession, "");
            set { PlayerPrefs.SetString(kSession, value ?? ""); PlayerPrefs.Save(); }
        }
        public static bool HasSession => !string.IsNullOrEmpty(SessionToken);
        /// The wallet the session belongs to (the token's body), or null.
        public static string SessionWallet
        {
            get
            {
                try
                {
                    var t = SessionToken; if (string.IsNullOrEmpty(t)) return null;
                    var b = t.Split('.')[0].Replace('-', '+').Replace('_', '/');
                    b = b.PadRight(b.Length + (4 - b.Length % 4) % 4, '=');
                    return (string)Newtonsoft.Json.Linq.JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(b)))["w"];
                }
                catch { return null; }
            }
        }

        /// Stable per-install id so guest races can be claimed after sign-in. Not tied to hardware ids.
        public static string DeviceId
        {
            get
            {
                var id = PlayerPrefs.GetString(kDevice, "");
                if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString(kDevice, id); PlayerPrefs.Save(); }
                return id;
            }
        }

        // ---- DTOs ----
        [Serializable] public class SiwsPayload { public string domain, uri, statement, version, chainId, nonce, issuedAt, expirationTime; }
        [Serializable] public class Profile { public string token, wallet, skrName; public bool isSeeker; public List<string> unlocks = new(); }
        [Serializable] public class RaceStart { public string raceId, raceToken; }
        [Serializable] public class RaceFinish { public bool accepted, isPersonalBest; public string raceId, memo, error; }
        [Serializable] public class Board { public string track; public List<Entry> entries = new(); }
        [Serializable] public class Entry { public int rank; public string wallet, name, memoSig; public bool isSeeker; public int bestMs; }
        [Serializable] public class RoomCreated { public string code, url; }
        [Serializable] public class PublicRoom { public string code, host, fee, currency; public int players, maxPlayers; public List<string> tracks = new(); public long createdAt; }
        [Serializable] public class PublicRooms { public List<PublicRoom> rooms = new(); }
        [Serializable] public class ShopItem { public string sku, kind, name, blurb, item; public int qty; public double usd; public bool skrOnly, onSale = true; public List<string> grants; public Dictionary<string, int> powers; public Dictionary<string, string> price = new(); }
        [Serializable] public class ConfigDto
        {
            public string treasuryOwner, treasuryAta, skrMint;
            public bool paidRooms; public int cutBps, hostShareBps, minPaidPlayers, maxPlayers;
            public Dictionary<string, double> usd = new();             // currency -> USD rate ("source" key is absent here)
            public Dictionary<string, List<string>> fees = new();      // currency -> base-unit tiers
            public Dictionary<string, List<int>> splits = new();       // paid players -> % per place
            public List<ShopItem> catalog = new();
            public ShopItem Item(string sku) => catalog.Find(i => i.sku == sku);
            public bool CanPay => !string.IsNullOrEmpty(treasuryOwner);
        }

        /// The Worker's live config (treasury, fee tiers, prices): fetched once per session, never compiled into the APK.
        public static ConfigDto Cfg { get; private set; }
        public static async Awaitable<ConfigDto> Config()
        {
            if (Cfg != null) return Cfg;
            try { Cfg = await Get<ConfigDto>("/v1/config"); }
            catch (Exception e) { Debug.LogWarning("[api] config: " + e.Message); }
            return Cfg;
        }
        public static Awaitable<RoomCreated> CreateRoom(List<string> tracks, string fee, string currency, bool isPublic)
            => Post<RoomCreated>("/v1/rooms", new { tracks, fee, currency, device = DeviceId, isPublic });
        public static Awaitable<PublicRooms> OpenRooms() => Get<PublicRooms>("/v1/rooms/public");

        public static Awaitable<SiwsPayload> Challenge() => Post<SiwsPayload>("/v1/auth/challenge", new { });

        public static async Awaitable<Profile> VerifySiws(string nonce, string address, string signedMessageB64, string signatureB64)
        {
            var p = await Post<Profile>("/v1/auth/verify", new { nonce, signInResult = new { address, signed_message = signedMessageB64, signature = signatureB64 } });
            if (p != null && !string.IsNullOrEmpty(p.token)) SessionToken = p.token;
            return p;
        }

        /// Fallback: payload = message||signature as returned by SignMessage (the Worker slices the last 64 bytes).
        public static async Awaitable<Profile> VerifySignedMessage(string nonce, string address, byte[] messageAndSig)
        {
            var p = await Post<Profile>("/v1/auth/verify", new { nonce, address, challengeSig = Convert.ToBase64String(messageAndSig) });
            if (p != null && !string.IsNullOrEmpty(p.token)) SessionToken = p.token;
            return p;
        }

        public static Awaitable<RaceStart> StartRace(string trackId) => Post<RaceStart>("/v1/race/start", new { deviceId = DeviceId, trackId });
        /// `path`: the drive, 5 samples a second of [t s, x, y, z]; the Worker's referee must believe it (worker/src/referee.ts).
        public static Awaitable<RaceFinish> FinishRace(string raceToken, int totalMs, List<int> lapMs, List<int> checkpointMs, int position, List<float[]> path)
            => Post<RaceFinish>("/v1/race/finish", new { raceToken, totalMs, lapMs, checkpointMs, position, path });
        public static Awaitable<RaceFinish> ClaimRace(string raceToken) => Post<RaceFinish>("/v1/race/claim", new { raceToken });
        public static Awaitable<Board> Leaderboard(string track) => Get<Board>($"/v1/leaderboard?track={track}");
        public static Awaitable<Profile> Me() => Get<Profile>("/v1/me");
        public static Awaitable<object> DeleteMe() => Send<object>("DELETE", "/v1/me", null);
        public static Awaitable<object> ConfirmTx(string signature, string raceId) => Post<object>("/v1/tx/confirm", new { signature, raceId });
        /// An entry payment for a room (session): the room seats it, or verifies and refunds it. Never dropped.
        public static Awaitable<object> RoomEntry(string code, int round, string signature) => Post<object>($"/v1/rooms/{code}/entry", new { signature, round });
        /// A payment the phone may have made but never got the signature of: the room looks for it on-chain by its memo.
        public static Awaitable<object> RoomEntryScan(string code, int round) => Post<object>($"/v1/rooms/{code}/entry", new { scan = true, round });
        public static Awaitable<object> VerifyPurchase(string signature, string sku, string orderId, string currency) => Post<object>("/v1/purchase/verify", new { signature, sku, orderId, currency });

        public static void Event(string name, object props = null)
        {
            _ = Post<object>("/v1/event", new { deviceId = DeviceId, name, props });   // fire and forget
        }

        // ---- transport ----
        static Awaitable<T> Post<T>(string path, object body) => Send<T>("POST", path, body);
        static Awaitable<T> Get<T>(string path) => Send<T>("GET", path, null);

        static async Awaitable<T> Send<T>(string method, string path, object body)
        {
            using var req = new UnityWebRequest(BaseUrl + path, method);
            if (body != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body)));
                req.SetRequestHeader("content-type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            if (HasSession) req.SetRequestHeader("authorization", "Bearer " + SessionToken);
            req.timeout = 15;
            await req.SendWebRequest();
            var text = req.downloadHandler.text;
            if (req.result != UnityWebRequest.Result.Success && req.responseCode != 202)
            {
                Debug.LogWarning($"[api] {method} {path} -> {req.responseCode} {text}");
                if (req.responseCode == 401 && path != "/v1/auth/verify") SessionToken = "";   // expired session: silent re-login next time
                throw new ApiException(req.responseCode, text);
            }
            return string.IsNullOrEmpty(text) ? default : JsonConvert.DeserializeObject<T>(text);
        }

        public sealed class ApiException : Exception
        {
            public readonly long Status;
            public ApiException(long status, string body) : base($"{status}: {body}") { Status = status; }
        }
    }

    /// Entry payments this phone made that no room has acknowledged yet. The transfer is on-chain before any room hears
    /// of it, so the signature is written down first and delivered over HTTP until a room answers: the room then seats
    /// it, or verifies and refunds it. Nothing depends on the socket being up or the seat still being there.
    /// Each is kept with the wallet that paid (delivered only under that wallet's session). An attempt is written down
    /// before the wallet app even opens: if the game dies before the signature comes back, the room is asked to look
    /// for the transfer on-chain by its memo.
    public static class PendingEntries
    {
        const string Key = "kc.entries";
        const string Scan = "scan:";
        const long ScanForSeconds = 300;          // an attempt is asked about for five minutes, then let go
        [Serializable] public class Entry { public string code, sig, wallet; public int round; public long at; }
        static bool flushing;
        static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        static List<Entry> Load() { try { return JsonConvert.DeserializeObject<List<Entry>>(PlayerPrefs.GetString(Key, "[]")) ?? new List<Entry>(); } catch { return new List<Entry>(); } }
        static void Store(List<Entry> list) { PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(list)); PlayerPrefs.Save(); }
        static void Remove(string sig) { var list = Load(); list.RemoveAll(e => e.sig == sig); Store(list); }
        public static bool Has(string sig) => Load().Exists(e => e.sig == sig);

        public static void Add(string code, int round, string sig, string wallet)
        {
            var list = Load();
            if (!list.Exists(e => e.sig == sig)) list.Add(new Entry { code = code, round = round, sig = sig, wallet = wallet, at = Now });
            Store(list);
        }

        /// Before the wallet app opens: returns the attempt's id, to be replaced by the signature or dropped.
        public static string Attempt(string code, int round, string wallet)
        {
            var id = Scan + Guid.NewGuid().ToString("N");
            Add(code, round, id, wallet);
            return id;
        }

        public static void Forget(string id) { if (!string.IsNullOrEmpty(id)) Remove(id); }

        /// Deliver every undelivered entry. Safe to call often: a room answers a repeat as already known.
        public static async Awaitable Flush()
        {
            if (flushing || !KartApi.HasSession) return;
            flushing = true;
            try
            {
                var me = KartApi.SessionWallet;
                foreach (var e in Load())
                {
                    if (!string.IsNullOrEmpty(e.wallet) && e.wallet != me) continue;   // another wallet's: kept until it signs in
                    bool attempt = e.sig.StartsWith(Scan);
                    try
                    {
                        if (attempt) { await KartApi.RoomEntryScan(e.code, e.round); if (Now - e.at > ScanForSeconds) Remove(e.sig); }
                        else { await KartApi.RoomEntry(e.code, e.round, e.sig); Remove(e.sig); }
                    }
                    // not a payment, taken by someone else, or no such room: there is nothing to deliver it to
                    catch (KartApi.ApiException x) when (x.Status == 400 || x.Status == 404) { Remove(e.sig); }
                    catch (Exception x) { Debug.LogWarning("[entry] will retry: " + x.Message); }
                }
            }
            finally { flushing = false; }
        }

        /// Right after paying: deliver now, and keep trying for about half a minute if the network is being difficult.
        public static async Awaitable Deliver(string sig)
        {
            for (float wait = 2f; wait <= 16f && Has(sig); wait *= 2f)
            {
                await Flush();
                if (!Has(sig)) return;
                await Awaitable.WaitForSecondsAsync(wait);
            }
        }
    }
}
