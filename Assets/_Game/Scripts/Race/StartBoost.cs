using PowerslideKartPhysics;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// A start boost (Beach Buggy's jump start): tap DRIFT in the last moment before GO and the kart leaves the line on a
    /// burst; too early gets nothing. A line under the countdown says so. Some of the AI field gets one too, so the
    /// start is a scramble rather than a procession.
    public sealed class StartBoost : MonoBehaviour
    {
        const float Window = 0.4f;                              // seconds before GO a press counts
        [SerializeField] TMP_Text hint;
        RaceManager race;
        float pressedAt = -1f;                                   // seconds left on the countdown when DRIFT went down
        bool wasDown, fired;

        void Start()
        {
            race = RaceManager.Instance;
            if (race != null) race.onGo.AddListener(Go);
            if (hint != null) hint.gameObject.SetActive(false);
        }

        void Update()
        {
            bool counting = race != null && race.state == RaceManager.State.Countdown && race.countdown > 0f;
            if (hint != null && hint.gameObject.activeSelf != counting) hint.gameObject.SetActive(counting);
            if (!counting) { wasDown = false; return; }
            bool down = InputManager.MobileInput.driftButton;
            if (down && !wasDown) pressedAt = race.countdown;     // the last press is the one that counts
            wasDown = down;
        }

        void Go()
        {
            if (fired || race == null) return;
            fired = true;
            var player = race.Player != null ? race.Player.GetComponent<Kart>() : null;
            if (player != null && pressedAt >= 0f && pressedAt <= Window)
            {
                player.AddBoost(2.5f, 6f);
                RaceHud.Instance?.Callout("<color=#FFB126>START BOOST!</color>", 1.1f);
                CameraFeel.Instance?.Boost(2f);
                Haptics.Play(Haptics.Kind.Boost);
                AudioDirector.Instance?.TierUp(3);
                KartApi.Event("start_boost", new { left = pressedAt });
            }
            if (RoomClient.InRaceRoom) return;                    // online there is no AI field
            foreach (var r in race.racers)
                if (r != null && !r.isPlayer && Random.value < 0.35f) r.GetComponent<Kart>()?.AddBoost(2.5f, 6f);
        }
    }
}
