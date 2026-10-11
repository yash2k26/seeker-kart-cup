using System.Collections.Generic;
using UnityEngine;

namespace KartCup
{
    /// A sheet a kart can reach: the sea round Palm Bay, Frost Peak's lake, Pine Ridge's swamp, Mount Ember's lava. The
    /// planes are drawn only (no collider), so karts used to drive along the bottom; this says where the surface is, for
    /// the splash a wheel throws in the shallows and for the out-of-bounds a kart that goes in gets.
    public sealed class WaterBody : MonoBehaviour
    {
        public float radius = float.PositiveInfinity;   // a map's sheet has no edge; a pool is a circle round this transform
        public bool lava;                               // no shallows: a wheel in it is a kart out (KartProgress)
        public string word = "SPLASH!";                 // what the player is told on the way under
        static readonly List<WaterBody> all = new();

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// How far under the surface a kart goes before it is out: water has shallows to splash through, lava none.
        public float Depth => lava ? 0.15f : 0.7f;

        /// The sheet over `p`, if `p` is over one.
        public static WaterBody At(Vector3 p)
        {
            foreach (var w in all)
            {
                var c = w.transform.position;
                var d = new Vector2(p.x - c.x, p.z - c.z);
                if (d.sqrMagnitude <= w.radius * w.radius) return w;
            }
            return null;
        }

        /// The surface height over `p`, if `p` is over one.
        public static bool SurfaceAt(Vector3 p, out float height)
        {
            var w = At(p);
            height = w != null ? w.transform.position.y : 0f;
            return w != null;
        }
    }
}
