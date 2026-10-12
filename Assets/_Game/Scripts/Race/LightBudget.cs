using System.Linq;
using UnityEngine;

namespace KartCup
{
    /// The night maps' lamps (Palm Bay 72, Frost Peak 60, Mount Ember 43 spot lights): only the nearest dozen within
    /// 120 m of the camera are switched on (13 Oct 2026, for 60 fps). A lamp lights 34 m round it, so one further off only
    /// lit road deep in the fog; the ones kept are the ones URP would pick for the road in shot anyway, and it no longer
    /// sorts seventy for every piece. Karts' headlights and the podium's lights are left alone.
    public sealed class LightBudget : MonoBehaviour
    {
        const int Keep = 12; const float Reach = 120f;
        Light[] lamps; float[] dist; int[] order; float next;

        void Start()
        {
            lamps = FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l.type != LightType.Directional && l.GetComponentInParent<PowerslideKartPhysics.Kart>() == null && l.GetComponentInParent<PodiumShow>() == null).ToArray();
            dist = new float[lamps.Length]; order = new int[lamps.Length];
            if (lamps.Length <= Keep) enabled = false;
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.25f;
            var cam = Camera.main; if (cam == null) return;
            var p = cam.transform.position;
            for (int i = 0; i < lamps.Length; i++) { order[i] = i; dist[i] = lamps[i] != null ? (lamps[i].transform.position - p).sqrMagnitude : float.MaxValue; }
            System.Array.Sort(dist, order);
            for (int k = 0; k < order.Length; k++)
            {
                var l = lamps[order[k]];
                if (l != null) l.enabled = k < Keep && dist[k] < Reach * Reach;
            }
        }
    }
}
