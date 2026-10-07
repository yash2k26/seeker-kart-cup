using System.Linq;
using UnityEditor;
using UnityEngine;

// The driver screen's standing drivers (8 Oct 2026, Beach Buggy's driver select): per driver, their own rig's idle
// and greeting takes as legacy clips, and how far the rig's origin (the hips) sits above the feet in the idle pose,
// so CharacterCarousel can stand them on the disc at the size they have in a seat.
public static partial class MenuSetup
{
    static void StandingDrivers(SerializedObject carousel)
    {
        var list = carousel.FindProperty("driverPrefabs");
        var drivers = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject).ToArray();
        var idle = new Object[drivers.Length]; var wave = new Object[drivers.Length]; var lifts = new float[drivers.Length];
        for (int i = 0; i < drivers.Length; i++)
        {
            if (drivers[i] == null) continue;
            var model = PodiumSetup.ModelOf(AssetDatabase.GetAssetPath(drivers[i]));
            if (model == null) continue;
            var idleClip = PodiumSetup.Clip(model, "idle", $"select_idle_{drivers[i].name}", true);
            idle[i] = idleClip;
            wave[i] = PodiumSetup.Clip(model, "emote-yes", $"select_wave_{drivers[i].name}", false);
            lifts[i] = idleClip != null ? Lift(drivers[i], idleClip) : 0f;
        }
        Fill(carousel.FindProperty("idleClips"), idle);
        Fill(carousel.FindProperty("waveClips"), wave);
        var lp = carousel.FindProperty("standLifts"); lp.arraySize = lifts.Length;
        for (int i = 0; i < lifts.Length; i++) lp.GetArrayElementAtIndex(i).floatValue = lifts[i];
        carousel.FindProperty("standScale").floatValue = SeatScale();
        Debug.Log($"[menu] standing drivers: {drivers.Count(d => d != null)}, seat scale {SeatScale():F2}, lifts {string.Join(" ", lifts.Select(l => l.ToString("F2")))}");
    }

    /// In the idle pose, at scale 1 and the origin: how far below the rig's origin the lowest vertex is. Skinned by hand
    /// (Kenney's rig weights each vertex wholly to one bone, as MenuMeet relies on), so it is exact in edit mode.
    static float Lift(GameObject prefab, AnimationClip pose)
    {
        var d = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        d.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); d.transform.localScale = Vector3.one;
        pose.SampleAnimation(d, 0f);
        var body = d.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.ToLowerInvariant().Contains("body"));
        float lift = 0f;
        if (body != null && body.sharedMesh != null)
        {
            var skin = body.sharedMesh; var weights = skin.boneWeights;
            var toWorld = body.bones.Select((bone, i) => bone.localToWorldMatrix * skin.bindposes[i]).ToArray();
            lift = -skin.vertices.Select((v, i) => toWorld[weights[i].boneIndex0].MultiplyPoint3x4(v).y).Min();
        }
        Object.DestroyImmediate(d);
        return lift;
    }

    /// The driver's world scale in a kart's seat: standing beside the karts, they must not be a different size.
    static float SeatScale()
    {
        var kart = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + $"Karts/Kart_{KartSetup.Karts.First()}.prefab");
        var seat = kart != null ? kart.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "DriverSeat") : null;
        return seat != null ? seat.lossyScale.x : 1f;
    }
}
