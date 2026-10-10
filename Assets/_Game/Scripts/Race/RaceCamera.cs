using UnityEngine;

namespace KartCup
{
    /// The race camera's draw distance for trackside clutter: TrackDressing puts the palms, rocks, crates and the beach
    /// town's houses on FarLayer, and the camera stops drawing that layer past FarCull metres (fog hides it before then).
    public static class RaceCamera
    {
        public const int FarLayer = 29;
        public const float FarCull = 170f;

        public static void Apply(Camera cam)
        {
            if (cam == null) return;
            var d = cam.layerCullDistances;
            d[FarLayer] = FarCull;
            cam.layerCullDistances = d;
            cam.layerCullSpherical = true;   // by distance, not by depth: no popping as the camera swings round a corner
        }
    }
}
