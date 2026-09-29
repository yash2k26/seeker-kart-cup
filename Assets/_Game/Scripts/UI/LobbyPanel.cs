using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The room before the race: the code to shout across the table, who is in, who has paid, the pool as it
    /// grows, what each place wins, and one button whose meaning is always the next thing to do.
    public sealed class LobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text codeLabel, infoLabel, poolLabel, prizeLabel, playersLabel, statusLabel, primaryLabel;
        [SerializeField] Button primaryButton;

        enum Mode { None, Pay, Verifying, Closed, Start, Ready }
        Mode mode;
        bool attested;

        void OnEnable()
        {
            RoomClient.RoomChanged += Refresh;
            RoomClient.Error += OnError;
            RoomClient.Closed += OnClosed;
            if (statusLabel != null) statusLabel.text = "";
            if (!Application.isPlaying) { RefreshSample(); return; }   // scene builder / MenuPreview: a believable room, not blank labels
            Refresh(RoomClient.Instance?.Room);
        }

        /// Edit-mode stand-in so the panel can be judged from a screenshot (MenuPreview calls it: OnEnable never runs in edit mode).
        public void RefreshSample()
        {
            if (codeLabel != null) codeLabel.text = "K 7 P 2 Q";
            if (infoLabel != null) infoLabel.text = "GRAND PRIX · 4 RACES   ·   ENTRY 0.05 SOL   ·   PUBLIC";
            if (poolLabel != null) poolLabel.text = "PRIZE POOL 0.15 SOL (≈$18.18)   ·   1st 0.08775 SOL   2nd 0.04725 SOL";
            if (prizeLabel != null) prizeLabel.text = "10% platform fee · the host earns 0.003 SOL of it";
            if (playersLabel != null) playersLabel.text = "1.<pos=8%><b>toly.skr  <size=70%><color=#93AEBC>HOST</color></size></b><pos=62%>PAID<pos=84%>\n2.<pos=8%>mert.skr<pos=62%>PAID<pos=84%>READY\n3.<pos=8%>9xQ4…Tf2<pos=62%>NOT PAID<pos=84%>\n<color=#93AEBC>4.<pos=8%>Invite a friend…</color>";
            if (statusLabel != null) statusLabel.text = "Waiting for 9xQ4…Tf2 to pay in";
            if (primaryLabel != null) primaryLabel.text = "START RACE";
        }

        void OnDisable()
        {
            RoomClient.RoomChanged -= Refresh;
            RoomClient.Error -= OnError;
            RoomClient.Closed -= OnClosed;
        }

        void Refresh(RoomClient.RoomState r)
        {
            var client = RoomClient.Instance;
            if (r == null || client == null) return;
            var me = r.players.Find(p => p.id == client.MyId);
            bool host = client.IsHost;

            if (codeLabel != null) codeLabel.text = string.Join(" ", r.code.ToCharArray());
            var trackNames = r.tracks.Select(t => Tracks.At(Tracks.IndexOf(t)).name);
            if (infoLabel != null) infoLabel.text = (r.tracks.Count > 1 ? $"GRAND PRIX · {r.tracks.Count} RACES" : trackNames.First().ToUpperInvariant())
                                                    + (r.IsPaid ? $"   ·   ENTRY {WalletFlow.Amount(r.fee, r.currency)}" : "   ·   FREE RACE")
                                                    + (r.isPublic ? "   ·   PUBLIC" : "   ·   INVITE ONLY");
            if (poolLabel != null)
                poolLabel.text = !r.IsPaid ? "BRAGGING RIGHTS ONLY" : $"PRIZE POOL {WalletFlow.Amount(r.pool, r.currency, true)}"
                    + (r.prizes.Count == 0 ? "" : "   ·   " + string.Join("   ", r.prizes.Select((p, i) => $"{RaceHud.Ordinal(i + 1)} {WalletFlow.Amount(p, r.currency)}")));
            if (prizeLabel != null)
                prizeLabel.text = !r.IsPaid ? "" : r.players.Count(p => p.paid) < 2 ? "Prizes appear once two racers have paid in" : FeeNote(r);

            if (playersLabel != null)
                playersLabel.text = string.Join("\n", r.players.Select((p, i) =>
                {
                    string who = (p.id == client.MyId ? "<b>" : "") + p.name + (p.host ? "  <size=70%><color=#93AEBC>HOST</color></size>" : "") + (p.id == client.MyId ? "</b>" : "");
                    string paid = !r.IsPaid ? "IN" : p.paid ? "PAID" : p.paying ? "PAYING…" : "NOT PAID";
                    string state = !p.connected ? "AWAY" : p.ready ? "READY" : "";
                    return $"{i + 1}.<pos=8%>{who}<pos=62%>{paid}<pos=84%>{state}";
                })) + (r.players.Count < (KartApi.Cfg?.maxPlayers ?? 6) ? $"\n<color=#93AEBC>{r.players.Count + 1}.<pos=8%>Invite a friend…</color>" : "");

            // the one button
            int here = r.players.Count(p => p.connected);
            var unpaid = r.players.Where(p => p.connected && !p.paid).Select(p => p.name).ToList();
            if (r.IsPaid && me != null && !me.paid) { mode = me.paying ? Mode.Verifying : me.canPay ? Mode.Pay : Mode.Closed; }
            else if (host) mode = Mode.Start;
            else mode = Mode.Ready;

            bool canStart = here >= 2 && (!r.IsPaid || unpaid.Count == 0);
            if (primaryLabel != null)
                primaryLabel.text = mode switch
                {
                    Mode.Pay => $"PAY ENTRY {WalletFlow.Amount(r.fee, r.currency)}",
                    Mode.Verifying => "VERIFYING…",
                    Mode.Closed => "ENTRY CLOSED",
                    Mode.Start => "START RACE",
                    _ => me != null && me.ready ? "READY ✓" : "READY",
                };
            if (primaryButton != null) primaryButton.interactable = mode switch { Mode.Verifying => false, Mode.Closed => false, Mode.Start => canStart, _ => true };

            if (statusLabel != null && (string.IsNullOrEmpty(statusLabel.text) || statusLabel.text.StartsWith("Waiting") || statusLabel.text.StartsWith("Everyone") || statusLabel.text.StartsWith("Invite")))
                statusLabel.text = here < 2 ? "Invite a friend — you need at least 2 racers"
                    : r.IsPaid && unpaid.Count > 0 ? $"Waiting for {string.Join(", ", unpaid)} to pay in"
                    : host ? "Everyone's in. Start when ready." : "Waiting for the host to start";
            if (mode == Mode.Closed && statusLabel != null) statusLabel.text = "You've left this room twice — join another one";
        }

        static string FeeNote(RoomClient.RoomState r)
        {
            // hostReward and feeBps are in the snapshot; RoomState carries them as extra fields only when the Worker sends them
            return r.feeBps > 0 ? $"{r.feeBps / 100}% platform fee · the host earns {WalletFlow.Amount(r.hostReward, r.currency)} of it" : "No platform fee on this room";
        }

        public async void Primary()
        {
            var client = RoomClient.Instance; var r = client?.Room;
            if (r == null) return;
            switch (mode)
            {
                case Mode.Pay:
                    if (!attested && PlayerPrefs.GetInt("kc.attest", 0) == 0)
                    {
                        attested = true;
                        Say("Paid races are for players 18+ where skill contests are legal. Tap PAY again to confirm.");
                        return;
                    }
                    PlayerPrefs.SetInt("kc.attest", 1);
                    if (WalletFlow.Instance == null) return;
                    Say("Opening wallet…");
                    if (!await WalletFlow.Instance.PayEntry(r)) Say(WalletFlow.Instance.LastStatus);
                    else Say("Entry sent · verifying on-chain…");
                    break;
                case Mode.Start:
                    Say("Starting…");
                    client.Send(new { t = "start" });
                    break;
                case Mode.Ready:
                    var me = client.Me;
                    client.Send(new { t = "ready", ready = me == null || !me.ready });
                    break;
            }
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Click();
        }

        public void ShareInvite()
        {
            var r = RoomClient.Instance?.Room;
            if (r == null) return;
            var url = $"{KartApi.BaseUrl}/r/{r.code}";
            var stake = r.IsPaid ? $" · entry {WalletFlow.Amount(r.fee, r.currency)}, winners get paid on-chain" : "";
            Share.Text($"Race me in Seeker Kart Cup — room {r.code}{stake}\n{url}");
            Say("Invite shared · code " + r.code);
            KartApi.Event("invite_share", new { code = r.code });
        }

        public void Leave()
        {
            RoomClient.Instance?.Leave();
            ScreenFlow.Instance?.ShowRooms();
        }

        void OnError(string msg) => Say(msg);
        void OnClosed(string reason)
        {
            Say(reason == "kicked" ? "The host removed you from the room" : reason == "Room expired" ? "The room expired" : "The room closed");
            RoomClient.Instance?.Leave();
            ScreenFlow.Instance?.ShowRooms();
        }

        void Say(string s) { if (statusLabel != null) statusLabel.text = s; }
    }
}
