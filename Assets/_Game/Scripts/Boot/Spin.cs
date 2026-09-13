using UnityEngine;

namespace KartCup
{
    /// Gate-scene helper. One MonoBehaviour per file, file named after the class: otherwise Unity embeds an editor-only
    /// MonoScript into the scene and the player dies at load with "level0 is corrupted [Position out of bounds]".
    public sealed class Spin : MonoBehaviour { void Update() => transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World); }
}
