using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// A circuit's shortcut (TrackShortcut.cs builds it): a dirt track across the inside of its tightest bend, with its
    /// own waypoints. It pays about a second on a clean line, the intro's drone flies it (PodiumShow) so the player knows
    /// it is there, and the AI only takes it once the field is good (Beach Buggy's AI takes its shortcuts from 500 HP):
    /// from the Tour's third cup up, and then only its bolder drivers.
    public sealed class ShortcutPath : MonoBehaviour
    {
        public BasicWaypoint from, to;          // the circuit's waypoints either side of it, in the way the lap runs
        public BasicWaypoint[] path;            // its own, from the `from` end to the `to` end; the last leads to `to`

        public static ShortcutPath Instance { get; private set; }
        /// The field's pace from which the AI cuts the corner: the Tour's third cup (RaceMode.Difficulty 1.02) and up.
        public const float FieldPace = 1.02f;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// How hard the turn into it is, the way the lap runs now: the road's heading at `from` against the cut's.
        public float TurnIn
        {
            get
            {
                if (from == null || path.Length < 2) return 180f;
                Vector3 road = from.transform.forward, cut = path[1].transform.position - path[0].transform.position;
                road.y = 0f; cut.y = 0f;
                return Vector3.Angle(road, cut);
            }
        }
        /// The AI cuts it only where it can turn in at speed: driven the other way round, a cut can open as a hairpin off
        /// the road (Pine Ridge's, reversed, is a 90 degree flick at 40 m/s, and the field spun off it). A player may still.
        public const float MaxTurnIn = 50f;

        public static bool Takes(float aggression) => Instance != null && RaceMode.Difficulty >= FieldPace && aggression > 0.5f && Instance.TurnIn <= MaxTurnIn;
        public bool Holds(BasicWaypoint w) => w != null && System.Array.IndexOf(path, w) >= 0;

        /// Turned round with the circuit (TrackDirection): it is entered from the other end.
        public void Reverse()
        {
            (from, to) = (to, from);
            System.Array.Reverse(path);
            for (int k = 0; k < path.Length; k++)
            {
                path[k].nextPoint = k + 1 < path.Length ? path[k + 1] : to;
                path[k].transform.rotation = Quaternion.LookRotation(-path[k].transform.forward, Vector3.up);
            }
        }
    }
}
