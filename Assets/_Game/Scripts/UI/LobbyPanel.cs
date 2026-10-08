using System.Collections.Generic;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;   // not the namespace: it has its own Vector2/Vector3
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The room before the race: the code to shout across the table, who is in and what each has staked, the pot as it
    /// grows, what you win if you come first, and one button whose meaning is always the next thing to do. In a paid
    /// room each player picks a stake from the room's minimum up (the chips), and it goes into the room's escrow.
    public sealed class LobbyPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text codeLabel, infoLabel, poolLabel, prizeLabel, playersLabel, statusLabel, primaryLabel;
        [SerializeField] Button primaryButton;
        [SerializeField] Button[] stakeChips = System.Array.Empty<Button>();   // the room's stakes from its minimum up
        // The picked chip is lifted, not gold: the STAKE button is the one accent on the screen.
        [SerializeField] Color chipIdle = new(0.18f, 0.04f, 0.44f), chipPicked = new(0.29f, 0.15f, 0.69f);
        [SerializeField] Color chipText = new(0.71f, 0.65f, 0.87f), chipPickedText = new(0.96f, 0.94f, 0.9f);
        [SerializeField] float chipPitch = 236f;   // centre to centre: the shown chips are centred as a row

        enum Mode { None, Opening, Pay, Verifying, Closed, Start, Ready }
        Mode mode;
        bool attested;
        string pick;                 // the stake this seat will put in (base units)
        string pickFor;              // which room and round `pick` belongs to
        List<string> offer = new();  // the stakes this room takes, smallest first

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
            if (infoLabel != null) infoLabel.text = "GRAND PRIX · 4 RACES   ·   STAKES FROM 0.05 SOL   ·   PUBLIC";
            if (poolLabel != null) poolLabel.text = "POT 0.65 SOL   ·   STAKE 0.1 SOL TO WIN 0.1575 SOL";
            if (prizeLabel != null) prizeLabel.text = "";
            if (playersLabel != null) playersLabel.text = "1.<pos=8%><b>toly.skr  <size=70%><color=#B4A7DE>HOST</color></size></b><pos=50%>0.5 SOL<pos=68%>STAKED<pos=86%>\n2.<pos=8%>mert.skr<pos=50%>0.05 SOL<pos=68%>STAKED<pos=86%>READY\n3.<pos=8%>9xQ4…Tf2<pos=50%><pos=68%>NOT STAKED<pos=86%>\n<color=#B4A7DE>4.<pos=8%>Invite a friend…</color>";
            if (statusLabel != null) statusLabel.text = "Waiting for 9xQ4…Tf2 to stake";
            if (primaryLabel != null) primaryLabel.text = "STAKE 0.1 SOL";
            string[] sample = { "0.05 SOL", "0.1 SOL", "0.15 SOL", "0.5 SOL" };
            for (int i = 0; i < stakeChips.Length; i++) ShowChip(i, i < sample.Length ? sample[i] : null, i == 1, sample.Length);
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
            string Amount(string v) => WalletFlow.Amount(v, r.currency);

            // the stakes on offer, and this seat's pick (the room's minimum until it picks another)
            offer = (KartApi.Cfg?.stakes != null && KartApi.Cfg.stakes.TryGetValue(r.currency, out var tiers) ? tiers : new List<string> { r.minStake })
                .Where(t => BigInteger.Parse(t) >= BigInteger.Parse(r.minStake) && BigInteger.Parse(t) <= BigInteger.Parse(r.maxStake)).ToList();
            if (pickFor != r.code + r.round || !offer.Contains(pick)) { pick = offer.FirstOrDefault() ?? r.minStake; pickFor = r.code + r.round; }

            if (codeLabel != null) codeLabel.text = string.Join(" ", r.code.ToCharArray());
            var trackNames = r.tracks.Select(t => Tracks.At(Tracks.IndexOf(t)).name);
            if (infoLabel != null) infoLabel.text = (r.tracks.Count > 1 ? $"GRAND PRIX · {r.tracks.Count} RACES" : trackNames.First().ToUpperInvariant())
                                                    + (r.IsPaid ? $"   ·   STAKES FROM {Amount(r.minStake)}" : "   ·   FREE RACE")
                                                    + (r.isPublic ? "   ·   PUBLIC" : "   ·   INVITE ONLY");

            // the one button
            int here = r.players.Count(p => p.connected);
            var unpaid = r.players.Where(p => p.connected && !p.paid).Select(p => p.name).ToList();
            bool escrowOpen = r.escrow?.state == "open";
            if (r.IsPaid && me != null && !me.paid) mode = me.paying ? Mode.Verifying : !escrowOpen ? Mode.Opening : me.canPay ? Mode.Pay : Mode.Closed;
            else if (host) mode = Mode.Start;
            else mode = Mode.Ready;

            if (poolLabel != null) poolLabel.text = !r.IsPaid ? "BRAGGING RIGHTS ONLY" : $"POT {Amount(r.pool)}" + WinLine(r, me);
            if (prizeLabel != null)
                prizeLabel.text = !r.IsPaid || mode == Mode.Pay ? ""
                    : r.escrow?.error != null ? "The prize pool can't be reached right now. Stakes already in are safe on-chain."
                    : !escrowOpen && r.state == "lobby" ? "Opening the prize pool on Solana…"
                    : $"Fair shares: nobody wins more from a rival than they staked   ·   {r.cutBps / 100}% house cut on matched stakes";

            if (playersLabel != null)
                playersLabel.text = string.Join("\n", r.players.Select((p, i) =>
                {
                    string who = (p.id == client.MyId ? "<b>" : "") + p.name + (p.host ? "  <size=70%><color=#B4A7DE>HOST</color></size>" : "") + (p.id == client.MyId ? "</b>" : "");
                    string stake = r.IsPaid && p.paid && p.stake != null ? Amount(p.stake) : "";
                    string paid = !r.IsPaid ? "IN" : p.paid ? "STAKED" : p.paying ? "STAKING…" : "NOT STAKED";
                    string state = !p.connected ? "AWAY" : p.ready ? "READY" : "";
                    return $"{i + 1}.<pos=8%>{who}<pos=50%>{stake}<pos=68%>{paid}<pos=86%>{state}";
                })) + (r.players.Count < (KartApi.Cfg?.maxPlayers ?? 6) ? $"\n<color=#B4A7DE>{r.players.Count + 1}.<pos=8%>Invite a friend…</color>" : "");

            int shown = mode == Mode.Pay ? Mathf.Min(offer.Count, stakeChips.Length) : 0;
            for (int i = 0; i < stakeChips.Length; i++) ShowChip(i, i < shown ? Amount(offer[i]) : null, i < offer.Count && offer[i] == pick, shown);

            bool canStart = here >= 2 && (!r.IsPaid || unpaid.Count == 0);
            if (primaryLabel != null)
                primaryLabel.text = mode switch
                {
                    Mode.Opening => "OPENING PRIZE POOL…",
                    Mode.Pay => $"STAKE {Amount(pick)}",
                    Mode.Verifying => "STAKING…",
                    Mode.Closed => "ENTRY CLOSED",
                    Mode.Start => "START RACE",
                    _ => me != null && me.ready ? "READY ✓" : "READY",
                };
            if (primaryButton != null) primaryButton.interactable = mode switch { Mode.Opening => false, Mode.Verifying => false, Mode.Closed => false, Mode.Start => canStart, _ => true };

            if (statusLabel != null && (string.IsNullOrEmpty(statusLabel.text) || statusLabel.text.StartsWith("Waiting") || statusLabel.text.StartsWith("Everyone") || statusLabel.text.StartsWith("Invite")))
                statusLabel.text = here < 2 ? "Invite a friend — you need at least 2 racers"
                    : r.IsPaid && unpaid.Count > 0 ? $"Waiting for {string.Join(", ", unpaid)} to stake"
                    : host ? "Everyone's in. Start when ready." : "Waiting for the host to start";
            if (mode == Mode.Closed && statusLabel != null) statusLabel.text = "You've taken your stake back twice — join another room";
        }

        /// "If you win": the room's own figure once this seat has staked, or a preview for the picked chip before it does.
        string WinLine(RoomClient.RoomState r, RoomClient.PlayerState me)
        {
            if (me == null) return "";
            if (me.paid) return me.ifWin != null ? $"   ·   IF YOU WIN {WalletFlow.Amount(me.ifWin, r.currency)}" : "   ·   WAITING FOR RIVALS";
            if (mode != Mode.Pay) return "";
            var stakes = r.players.Where(p => p.paid && p.stake != null).Select(p => BigInteger.Parse(p.stake)).Append(BigInteger.Parse(pick)).ToList();
            var win = FairShares.IfWin(stakes, stakes.Count - 1, r.cutBps);
            return win > 0 ? $"   ·   STAKE {WalletFlow.Amount(pick, r.currency)} TO WIN {WalletFlow.Amount(win.ToString(), r.currency)}" : "";
        }

        void ShowChip(int i, string label, bool picked, int shown)
        {
            var chip = stakeChips[i];
            if (chip == null) return;
            chip.gameObject.SetActive(label != null);
            if (label == null) return;
            var rt = (RectTransform)chip.transform;
            rt.anchoredPosition = new Vector2((i - (shown - 1) / 2f) * chipPitch, rt.anchoredPosition.y);
            var text = chip.GetComponentInChildren<TMP_Text>();
            if (text != null) { text.text = label; text.color = picked ? chipPickedText : chipText; }
            if (chip.targetGraphic != null) chip.targetGraphic.color = picked ? chipPicked : chipIdle;
        }

        /// A stake chip: this seat's stake, before it pays.
        public void PickStake(int i)
        {
            if (mode != Mode.Pay || i < 0 || i >= offer.Count) return;
            pick = offer[i];
            Haptics.Play(Haptics.Kind.Tick); AudioDirector.Instance?.Click();
            Refresh(RoomClient.Instance?.Room);
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
                        Say("Paid races are for players 18+ where skill contests are legal. Tap STAKE again to confirm.");
                        return;
                    }
                    PlayerPrefs.SetInt("kc.attest", 1);
                    if (WalletFlow.Instance == null) return;
                    Say("Opening wallet…");
                    if (!await WalletFlow.Instance.Stake(r, ulong.Parse(pick))) Say(WalletFlow.Instance.LastStatus);
                    else Say("Stake sent · the room is reading it from the chain…");
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
            var stake = r.IsPaid ? $" · stakes from {WalletFlow.Amount(r.minStake, r.currency)}, paid out on-chain by the escrow" : "";
            Share.Text($"Race me in Kartel — room {r.code}{stake}\n{url}");
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
