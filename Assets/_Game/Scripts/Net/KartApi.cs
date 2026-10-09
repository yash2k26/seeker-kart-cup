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
        [Serializable] public class PublicRoom { public string code, host, minStake = "0", currency; public int players, maxPlayers; public List<string> tracks = new(); public long createdAt; }
        [Serializable] public class PublicRooms { public List<PublicRoom> rooms = new(); }
        [Serializable] public class ShopItem { public string sku, kind, name, blurb, item; public int qty; public double sol; public bool skrOnly, onSale = true; public List<string> grants; public Dictionary<string, int> powers; public Dictionary<string, string> price = new(); }
        [Serializable] public class ConfigDto
        {
            public string treasuryOwner, treasuryAta, skrMint, oreMint, treasuryOreAta;
            /// Paid rooms' stakes go to this program's room accounts, never to the treasury.
            public string escrowProgram;
            public bool paidRooms; public int cutBps, minPaidPlayers, maxPlayers;
            public Dictionary<string, List<string>> stakes = new();    // currency -> the base-unit stakes a room offers
            public List<ShopItem> catalog = new();
            public ShopItem Item(string sku) => catalog.Find(i => i.sku == sku);
            public bool CanPay => !string.IsNullOrEmpty(treasuryOwner);
            /// ORE (Regolith Labs; the CLOCK IN side prize): a third rail on the same payment path, only while the Worker names the mint.
            public bool TakesOre => CanPay && !string.IsNullOrEmpty(oreMint) && !string.IsNullOrEmpty(treasuryOreAta);
        }

        /// The Worker's live config (treasury, fee tiers, prices), never compiled into the APK. Fetched once per session;
        /// a payment asks for one younger than `maxAgeSeconds` (SKR prices follow the market) and gets null when the
        /// Worker cannot be reached for it.
        public static ConfigDto Cfg { get; private set; }
        static DateTime cfgAt;
        public static async Awaitable<ConfigDto> Config(double maxAgeSeconds = double.PositiveInfinity)
        {
            if (Cfg == null || (DateTime.UtcNow - cfgAt).TotalSeconds >= maxAgeSeconds)
            {
                try { Cfg = await Get<ConfigDto>("/v1/config"); cfgAt = DateTime.UtcNow; }
                catch (Exception e) { Debug.LogWarning("[api] config: " + e.Message); }
            }
            return (DateTime.UtcNow - cfgAt).TotalSeconds < maxAgeSeconds ? Cfg : null;
        }
        /// minStake "0" is a free room; otherwise each player stakes from it up, into the room's escrow.
        public static Awaitable<RoomCreated> CreateRoom(List<string> tracks, string minStake, string currency, bool isPublic)
            => Post<RoomCreated>("/v1/rooms", new { tracks, minStake, currency, device = DeviceId, isPublic });
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

        /// mode "weekly": a Kartel Weekly time trial (the Worker files it under this week's board).
        public static Awaitable<RaceStart> StartRace(string trackId, string mode = null) => Post<RaceStart>("/v1/race/start", new { deviceId = DeviceId, trackId, mode });
        /// `path`: the drive, 5 samples a second of [t s, x, y, z]; the Worker's referee must believe it (worker/src/referee.ts).
        public static Awaitable<RaceFinish> FinishRace(string raceToken, int totalMs, List<int> lapMs, List<int> checkpointMs, int position, List<float[]> path)
            => Post<RaceFinish>("/v1/race/finish", new { raceToken, totalMs, lapMs, checkpointMs, position, path });
        public static Awaitable<RaceFinish> ClaimRace(string raceToken) => Post<RaceFinish>("/v1/race/claim", new { raceToken });
        public static Awaitable<Board> Leaderboard(string track) => Get<Board>($"/v1/leaderboard?track={track}");
        public static Awaitable<KartCup.Weekly.Board> WeeklyBoard(string wallet) => Get<KartCup.Weekly.Board>("/v1/weekly" + (string.IsNullOrEmpty(wallet) ? "" : "?wallet=" + wallet));
        public static Awaitable<Profile> Me() => Get<Profile>("/v1/me");
        public static Awaitable<object> DeleteMe() => Send<object>("DELETE", "/v1/me", null);
        public static Awaitable<object> ConfirmTx(string signature, string raceId) => Post<object>("/v1/tx/confirm", new { signature, raceId });
        /// `pending` is the Worker's 202 (not landed at 'confirmed' yet): a 2xx, so it arrives as an answer, not an exception.
        [Serializable] public class Verified { public bool pending; public List<string> granted; }
        public static Awaitable<Verified> VerifyPurchase(string signature, string sku, string orderId, string currency) => Post<Verified>("/v1/purchase/verify", new { signature, sku, orderId, currency });

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

    /// Shop payments the Worker has not granted yet. The wallet answers the moment it has sent the transaction, before
    /// the Worker can read it at 'confirmed', so the signature is written down first and asked about until the Worker
    /// grants or refuses it: a slow network, a closed app or a lost answer never turns a payment into nothing. Each is
    /// kept with the wallet that paid (asked about only under its session). What a payment buys is credited once, by
    /// whichever ask takes it off the list.
    public static class PendingPurchases
    {
        const string Key = "kc.purchases";
        public enum Outcome { Granted, Pending, Refused }
        [Serializable] public class Entry { public string sig, sku, orderId, currency, wallet; }
        static bool flushing;

        static List<Entry> Load() { try { return JsonConvert.DeserializeObject<List<Entry>>(PlayerPrefs.GetString(Key, "[]")) ?? new List<Entry>(); } catch { return new List<Entry>(); } }
        static void Store(List<Entry> list) { PlayerPrefs.SetString(Key, JsonConvert.SerializeObject(list)); PlayerPrefs.Save(); }
        /// True only for the caller that took it off the list.
        static bool Take(string sig) { var list = Load(); bool had = list.RemoveAll(e => e.sig == sig) > 0; Store(list); return had; }
        public static bool Any => Load().Count > 0;

        public static void Add(Entry e)
        {
            var list = Load();
            if (!list.Exists(x => x.sig == e.sig)) list.Add(e);
            Store(list);
        }

        public static async Awaitable<Outcome> Verify(Entry e)
        {
            try
            {
                var r = await KartApi.VerifyPurchase(e.sig, e.sku, e.orderId, e.currency);
                if (r == null || r.pending) return Outcome.Pending;
            }
            catch (KartApi.ApiException x) when (x.Status == 400)
            {
                // Refused for good (did not match the price, not a payment, already used). The transfer is on-chain
                // whatever happened, so leave a trail to settle it by hand.
                Debug.LogWarning($"[purchase] refused {e.sig}: {x.Message}");
                KartApi.Event("purchase_refused", new { e.sig, e.sku, e.currency, why = x.Message });
                Take(e.sig);
                return Outcome.Refused;
            }
            catch (Exception x) { Debug.LogWarning("[purchase] will retry: " + x.Message); return Outcome.Pending; }   // offline, 5xx, expired session
            if (Take(e.sig))   // item packs and gem packs are counted on the phone, once per payment
            {
                if (Inventory.BySku(e.sku) is { } pack)
                {
                    Inventory.Add(pack.id, pack.qty);
                    if (string.IsNullOrEmpty(Inventory.Loadout)) Inventory.Loadout = pack.id;
                }
                else if (e.sku.StartsWith("gems.") && int.TryParse(e.sku.Substring(5), out var gems)) Gems.Earn(gems, "bought");
            }
            return Outcome.Granted;
        }

        /// Ask about every payment of the signed-in wallet; returns how many were granted. Safe to call often.
        public static async Awaitable<int> Flush()
        {
            if (flushing || !KartApi.HasSession) return 0;
            flushing = true;
            try
            {
                int granted = 0;
                var me = KartApi.SessionWallet;
                foreach (var e in Load())
                    if (e.wallet == me && await Verify(e) == Outcome.Granted) granted++;
                return granted;
            }
            finally { flushing = false; }
        }
    }
}
