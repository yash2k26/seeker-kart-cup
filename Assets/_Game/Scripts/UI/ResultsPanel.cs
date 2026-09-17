using System.Linq;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// End of race: the finishing order, your time, and where it went. Shown by RaceManager's finish event.
    public sealed class ResultsPanel : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text headline, table, status;
        [SerializeField] GameObject saveButton;

        void Start()
        {
            if (panel != null) panel.SetActive(false);
            var race = RaceManager.Instance;
            if (race != null) race.onFinish.AddListener(OnFinish);
        }

        void OnFinish(KartProgress who)
        {
            if (!who.isPlayer || panel == null) return;
            var race = RaceManager.Instance;
            panel.SetActive(true);

            int place = race.PositionOf(who);
            if (headline != null) headline.text = place == 1 ? "YOU WIN" : RaceHud.Ordinal(place) + " PLACE";

            var order = race.racers.OrderByDescending(r => r.finished ? 1 : 0).ThenBy(r => r.finished ? r.finishTime : float.MaxValue)
                                   .ThenByDescending(r => r.score).ToList();
            if (table != null)
                table.text = string.Join("\n", order.Select((r, i) =>
                    $"{i + 1,2}.  {(r.isPlayer ? "<b>" : "")}{r.racerName,-10}{(r.isPlayer ? "</b>" : "")}  {(r.finished ? RaceHud.Clock(r.finishTime) : "—")}"));

            var best = who.lapTimes.Count > 0 ? who.lapTimes.Min() : 0f;
            if (status != null) status.text = best > 0 ? $"best lap {RaceHud.Clock(best)}" : "";

            Haptics.Play(place == 1 ? Haptics.Kind.Win : Haptics.Kind.Tick);
            AudioDirector.Instance?.Finish(place == 1);
            Submit(who, place);
        }

        async void Submit(KartProgress who, int place)
        {
            var session = RaceSession.Instance;
            if (session == null) return;
            session.OnFinish(place);
            if (saveButton != null) saveButton.SetActive(!KartApi.HasSession);
            await Awaitable.WaitForSecondsAsync(0.1f);
        }
    }
}
