using PowerslideKartPhysics;
using UnityEngine;

namespace KartCup
{
    /// After the line (solo): the kart does not stop dead. The race is over, the driving is not: the field's AI takes the
    /// player's kart on at a cruise while the camera swings round to the driver's face (Beach Buggy keeps driving too).
    public static class FinishLap
    {
        public static void Cruise(KartProgress p)
        {
            if (p == null) return;
            var touch = p.GetComponent<KartInputMobile>();
            if (touch != null) Object.DestroyImmediate(touch);              // one KartInput per kart: the AI takes its place now
            if (p.GetComponent<KartAi>() == null)
            {
                var ai = p.gameObject.AddComponent<KartAi>();
                ai.Configure(null, NearestAhead(p.transform), 7);
                ai.skill = 0.84f;
            }
            p.GetComponent<KartHooks>()?.SwingCamera();
        }

        /// The waypoint just up the road from here (the chain is the AI's racing line).
        static BasicWaypoint NearestAhead(Transform t)
        {
            BasicWaypoint best = null; float bestD = float.MaxValue;
            foreach (var w in Object.FindObjectsByType<BasicWaypoint>(FindObjectsSortMode.None))
            {
                var d = w.transform.position - t.position;
                if (Vector3.Dot(d, t.forward) < 3f) continue;
                float m = d.sqrMagnitude;
                if (m < bestD) { bestD = m; best = w; }
            }
            return best;
        }
    }
}
