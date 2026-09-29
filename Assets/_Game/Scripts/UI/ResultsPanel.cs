using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// End of race, and the hinge of the whole game: it scores the race into the cup, shows where that leaves you,
    /// and offers the one action that continues the story. Before the cup existed this screen was a dead end.
    /// A single chosen track is a one-race cup, so the same screen shows a finishing order with times instead.
    /// Online, the room is the referee: the card waits for its verdict, then shows places, prizes and payouts.
    public sealed class ResultsPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text headline, table, status;
        [SerializeField] TMP_Text primaryLabel, secondaryLabel;
        [SerializeField] CharacterDatabase roster;
        [SerializeField] GameObject saveButton;

        bool scored, online;
        RoomClient.ResultsMsg verdict;
        int ceremonyFor = -1;          // the room race whose podium has played (round * 100 + race)

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            var race = RaceManager.Instance;
            if (race != null) race.onFinish.AddListener(OnFinish);
        }

        void OnDestroy()
        {
            RoomClient.ResultsReceived -= OnlineResults;
            RoomClient.PayoutsChanged -= OnlinePayouts;
            RoomClient.Closed -= OnlineClosed;
        }

        void OnFinish(KartProgress who)
        {
            if (!who.isPlayer || panel == null || scored) return;
            scored = true;
            var race = RaceManager.Instance;
            foreach (var control in FindObjectsByType<HoldButton>(FindObjectsSortMode.None))
                control.gameObject.SetActive(false);

            // Online the card comes up at once ("waiting for the others"), and the podium plays when the room's verdict
            // is in; solo the podium plays first and the card follows it.
            if (RoomClient.Member) { ShowCard(); OnlineFinish(who, race); return; }

            // One ordering decides BOTH the position shown and the points awarded. Deriving them separately let the
            // card say "you finished 6th" while handing out the win's 10 points.
            var order = FinishOrder(race);
            int place = Mathf.Max(1, order.IndexOf(who) + 1);
            Cup.Record(order.Select(r => r.characterIndex).ToList());
            bool newBest = Tracks.RecordBest(Cup.TrackId, who.finishTime);

            bool single = Cup.IsSingleRace, final = Cup.IsFinalRace;
            int cupPlace = Cup.PlayerPlace;

            if (headline != null)
                headline.text = single ? (place == 1 ? "YOU WIN" : "FINISH")
                    : final ? (cupPlace == 1 ? "CUP CHAMPION" : RaceHud.Ordinal(cupPlace).ToUpperInvariant() + " IN THE CUP")
                    : $"RACE {Cup.RaceNumber} OF {Cup.Races}";

            int gained = place >= 1 && place <= Cup.PointsFor.Length ? Cup.PointsFor[place - 1] : 0;
            var best = who.lapTimes.Count > 0 ? who.lapTimes.Min() : 0f;
            if (status != null)
                status.text = (single ? $"You finished {RaceHud.Ordinal(place)}" + (best > 0 ? $" · best lap {RaceHud.Clock(best)}" : "")
                        : final ? "Final standings"
                        : $"You finished {RaceHud.Ordinal(place)}   +{gained} pts" + (best > 0 ? $"   best lap {RaceHud.Clock(best)}" : ""))
                    + (newBest ? "  NEW BEST" : "");

            if (table != null) table.text = single ? TimesText(order) : StandingsText();
            if (final) Cup.MarkFinished();   // so a kill-and-relaunch offers a new cup, not this one's last race again

            if (primaryLabel != null) primaryLabel.text = single ? "RACE AGAIN" : final ? "NEW CUP" : "NEXT RACE";
            if (secondaryLabel != null) secondaryLabel.text = single || final ? "MENU" : "QUIT CUP";

            Haptics.Play(cupPlace == 1 && final ? Haptics.Kind.Win : Haptics.Kind.Tick);
            AudioDirector.Instance?.Finish(place == 1);
            RaceSession.Instance?.OnFinish(place);
            if (saveButton != null) saveButton.SetActive(!KartApi.HasSession);

            // The podium: this race's top three, or after a cup's last race the cup's.
            bool cupPodium = final && !single;
            var top = cupPodium ? Cup.Standings.Take(3).Select(s => s.character).ToList() : order.Take(3).Select(r => r.characterIndex).ToList();
            bool mine = top.Count > 0 && top[0] == Cup.PlayerCharacter;
            string heading = mine ? (cupPodium ? "YOU TAKE THE CUP!" : "YOU WIN!")
                : top.Count > 0 ? $"{Name(top[0]).ToUpperInvariant()} {(cupPodium ? "TAKES THE CUP!" : "WINS!")}" : "";
            StartCoroutine(Ceremony(top.Select(c => Podium(c, Name(c))).ToList(), heading, 1.4f));
        }

        // ---------- the podium, then the card ----------
        PodiumShow.Winner Podium(int character, string name)
        {
            var def = roster != null && character >= 0 ? roster.At(character) : null;
            return new PodiumShow.Winner { driver = def != null ? RaceSpawner.DriverPrefab(def.driverId) : null, name = name };
        }

        /// The finish callout lands, then the top three take the podium (party poppers, trophies, the winner jumping),
        /// and after a few seconds, or a tap, the card comes up over it with the celebration still going behind.
        IEnumerator Ceremony(List<PodiumShow.Winner> top, string heading, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            var show = top.Count > 0 && top.Any(w => w.driver != null) ? PodiumShow.Ensure() : null;
            if (show != null)
            {
                if (panel != null) panel.SetActive(false);
                show.Winners(heading, top);
                float until = Time.unscaledTime + PodiumShow.CeremonySeconds, skippable = Time.unscaledTime + 1f;
                while (Time.unscaledTime < until && !(Time.unscaledTime > skippable && PodiumShow.Tapped())) yield return null;
            }
            ShowCard(podium: show != null);
        }

        void ShowCard(bool podium = false)
        {
            if (panel == null) return;
            // over the podium the dim is lighter, so the confetti and the trophies still show round the card
            var dim = panel.GetComponent<Image>();
            if (dim != null) { var c = dim.color; c.a = podium ? 0.5f : 0.74f; dim.color = c; }
            UiMotion.Show(panel, panel.transform.childCount > 0 ? panel.transform.GetChild(0) : null);
        }

        // ---------- online ----------
        void OnlineFinish(KartProgress who, RaceManager race)
        {
            online = true;
            var client = RoomClient.Instance;
            RoomClient.ResultsReceived += OnlineResults;
            RoomClient.PayoutsChanged += OnlinePayouts;
            RoomClient.Closed += OnlineClosed;

            bool crossed = who.finishTime < float.MaxValue && who.lapTimes.Count > 0;
            int provisional = Mathf.Max(1, FinishOrder(race).IndexOf(who) + 1);
            if (headline != null) headline.text = crossed ? "FINISH" : "RACE OVER";
            if (status != null) status.text = crossed ? $"You crossed the line {RaceHud.Ordinal(provisional)} · waiting for the others…" : "Waiting for the room's verdict…";
            if (table != null) table.text = TimesText(FinishOrder(race));
            if (primaryLabel != null) primaryLabel.transform.parent.gameObject.SetActive(false);
            if (secondaryLabel != null) secondaryLabel.text = "LEAVE";
            if (saveButton != null) saveButton.SetActive(false);
            Haptics.Play(Haptics.Kind.Tick);
            AudioDirector.Instance?.Finish(provisional == 1);
            if (crossed) RaceSession.Instance?.OnFinish(provisional);   // the leaderboard still takes your own time

            // The verdict may already be in (we timed out or dropped and the room decided without us).
            if (client.LastResults != null) OnlineResults(client.LastResults);
        }

        void OnlineResults(RoomClient.ResultsMsg r)
        {
            var client = RoomClient.Instance;
            if (client == null || r?.results == null) return;
            verdict = r;
            var res = r.results;
            bool single = res.races == 1, final = res.final;
            var room = client.Room;
            var mine = res.order.Find(o => o.id == client.MyId);
            int place = mine?.place ?? res.order.Count;
            int cupPlace = r.standings?.Find(s => s.id == client.MyId)?.place ?? 0;

            if (headline != null)
                headline.text = single ? (place == 1 ? "YOU WIN" : "FINISH")
                    : final ? (cupPlace == 1 ? "CUP CHAMPION" : RaceHud.Ordinal(cupPlace).ToUpperInvariant() + " IN THE CUP")
                    : $"RACE {res.race} OF {res.races}";

            if (table != null)
                table.text = single || !final
                    ? string.Join("\n", res.order.Select(o => Row(o.place - 1, o.name, o.dnf || o.ms == null ? "DNF" : RaceHud.Clock(o.ms.Value / 1000f), o.id == client.MyId)))
                    : string.Join("\n", r.standings.Select((s, i) => Row(i, s.name, s.points + " pts", s.id == client.MyId)));

            RefreshStatus();

            var primary = primaryLabel != null ? primaryLabel.transform.parent.gameObject : null;
            if (primary != null)
            {
                primary.SetActive(true);
                var button = primary.GetComponent<Button>();
                if (button != null) button.interactable = client.IsHost;
                primaryLabel.text = client.IsHost ? (final ? "RACE AGAIN" : "NEXT RACE") : "WAITING FOR HOST";
            }
            if (final) Haptics.Play(cupPlace == 1 ? Haptics.Kind.Win : Haptics.Kind.Tick);

            // The podium, once per race: the room's top three (or the cup's, after its last race).
            int key = room.round * 100 + res.race;
            if (key != ceremonyFor)
            {
                ceremonyFor = key;
                bool cupPodium = final && !single;
                var top = cupPodium && r.standings != null
                    ? r.standings.Take(3).Select(s => (s.id, s.name)).ToList()
                    : res.order.Take(3).Select(o => (o.id, o.name)).ToList();
                bool iWon = top.Count > 0 && top[0].id == client.MyId;
                string heading = iWon ? (cupPodium ? "YOU TAKE THE CUP!" : "YOU WIN!")
                    : top.Count > 0 ? $"{top[0].name.ToUpperInvariant()} {(cupPodium ? "TAKES THE CUP!" : "WINS!")}" : "";
                var podium = top.Select(t => Podium(room.players.Find(p => p.id == t.id)?.character ?? -1, t.name)).ToList();
                StopAllCoroutines();
                StartCoroutine(Ceremony(podium, heading, 0.2f));
            }
        }

        void OnlinePayouts(List<RoomClient.Payout> payouts) { if (verdict != null) { verdict.payouts = payouts; RefreshStatus(); } }

        void RefreshStatus()
        {
            var client = RoomClient.Instance;
            if (status == null || client?.Room == null || verdict?.results == null) return;
            var res = verdict.results; var room = client.Room;
            var mine = res.order.Find(o => o.id == client.MyId);
            int place = mine?.place ?? res.order.Count;
            bool single = res.races == 1, final = res.final;

            string line = single ? $"You finished {RaceHud.Ordinal(place)}"
                : final ? "Final standings"
                : $"You finished {RaceHud.Ordinal(place)}   +{mine?.points ?? 0} pts";
            if (!room.IsPaid) { status.text = line; return; }
            if (!final) { status.text = line + $"   ·   pool {WalletFlow.Amount(room.pool, room.currency)}"; return; }

            var wallet = client.Me?.wallet;
            var prize = verdict.payouts?.Find(p => p.kind == "prize" && p.wallet == wallet);
            var host = verdict.payouts?.Find(p => p.kind == "host" && p.wallet == wallet);
            if (prize == null && host == null) { status.text = line + "   ·   no prize this time"; return; }
            var parts = new List<string>();
            if (prize != null) parts.Add($"You won {WalletFlow.Amount(prize.amount, prize.currency, true)}");
            if (host != null) parts.Add($"{(prize != null ? "+ " : "")}{WalletFlow.Amount(host.amount, host.currency)} host reward");
            status.text = string.Join(" ", parts) + "   ·   " + PayoutState(prize ?? host);
        }

        static string PayoutState(RoomClient.Payout p) => p.status switch
        {
            "confirmed" => $"paid ✓ {WalletFlow.Short(p.signature)}",
            "sent" => "sending to your wallet…",
            "failed" => "payout stuck — contact support with this room's code",
            _ => "paying out…",
        };

        void OnlineClosed(string reason) { if (status != null) status.text = reason ?? "Room closed"; }

        /// Racers, first across the line to last. Anyone still driving is ranked by race progress.
        static List<KartProgress> FinishOrder(RaceManager race)
        {
            return race.racers
                .OrderByDescending(r => r.finished && r.finishTime < float.MaxValue ? 1 : 0)
                .ThenBy(r => r.finished ? r.finishTime : float.MaxValue)
                .ThenByDescending(r => r.score)
                .ToList();
        }

        string StandingsText() => string.Join("\n", Cup.Standings.Select((s, i) =>
            Row(i, Name(s.character), s.points + " pts", s.character == Cup.PlayerCharacter)));

        /// Single race: the finishing order with total times. A racer still on track when the player's card came up
        /// has no time yet, and inventing one would be a lie the leaderboard could never match.
        string TimesText(List<KartProgress> order) => string.Join("\n", order.Select((r, i) =>
            Row(i, r.remote || r.isPlayer && online ? r.racerName : Name(r.characterIndex), r.finished && r.finishTime < float.MaxValue ? RaceHud.Clock(r.finishTime) : "—", r.isPlayer)));

        string Name(int character) => roster != null && character >= 0 ? roster.At(character).displayName : "?";

        static string Row(int i, string name, string value, bool me)
        {
            string open = me ? "<b>" : "", close = me ? "</b>" : "";
            return $"{open}{i + 1}.<pos=12%>{name}<pos=72%>{value}{close}";
        }

        // ---- the two buttons, whose meaning depends on where the cup is ----
        public void Primary()
        {
            if (online)
            {
                var client = RoomClient.Instance;
                if (client == null || !client.IsHost || verdict?.results == null) return;
                client.Send(new { t = verdict.results.final ? "again" : "next" });
                return;
            }
            // RACE AGAIN and NEW CUP are the same thing: the track list this cup ran, from the top.
            if (Cup.IsFinalRace) Cup.Begin(Cup.PlayerCharacter, roster != null ? roster.characters.Length : 8, Cup.TrackList);
            else Cup.Advance();
            ScreenFlow.Instance?.LoadCupRace();
        }

        public void Secondary()
        {
            if (!online) Cup.End();
            ScreenFlow.Instance?.BackToMenu();   // online: leaves the room on the way out
        }
    }
}
