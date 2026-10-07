using System.Collections.Generic;
using UnityEngine;

namespace KartCup
{
    /// A sheet of water a kart can reach: the sea round Palm Bay, Pine Ridge's lake. The planes are drawn only (no
    /// collider), so karts used to drive along the bottom; this says where the surface is, for the splash a wheel throws
    /// in the shallows and for the out-of-bounds a kart that goes in deep gets.
    public sealed class WaterBody : MonoBehaviour
    {
        public float radius = float.PositiveInfinity;   // the sea has no edge; a lake is a circle round this transform
        static readonly List<WaterBody> all = new();

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// The water's surface height over `p`, if `p` is over water.
        public static bool SurfaceAt(Vector3 p, out float height)
        {
            foreach (var w in all)
            {
                var c = w.transform.position;
                var d = new Vector2(p.x - c.x, p.z - c.z);
                if (d.sqrMagnitude <= w.radius * w.radius) { height = c.y; return true; }
            }
            height = 0f;
            return false;
        }
    }
}
