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
        [Serializable] public class ShopItem { public string sku, kind, name, blurb; public double usd; public bool skrOnly, onSale = true; public List<string> grants; public Dictionary<string, string> price = new(); }
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
        public static Awaitable<RaceFinish> FinishRace(string raceToken, int totalMs, List<int> lapMs, List<int> checkpointMs, int position)
            => Post<RaceFinish>("/v1/race/finish", new { raceToken, totalMs, lapMs, checkpointMs, position });
        public static Awaitable<RaceFinish> ClaimRace(string raceToken) => Post<RaceFinish>("/v1/race/claim", new { raceToken });
        public static Awaitable<Board> Leaderboard(string track) => Get<Board>($"/v1/leaderboard?track={track}");
        public static Awaitable<Profile> Me() => Get<Profile>("/v1/me");
        public static Awaitable<object> DeleteMe() => Send<object>("DELETE", "/v1/me", null);
        public static Awaitable<object> ConfirmTx(string signature, string raceId) => Post<object>("/v1/tx/confirm", new { signature, raceId });
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
}
