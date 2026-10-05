using System.Collections;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartCup
{
    /// Between screens that are scenes: a full-screen card saying where you are going, with a turning spinner and a
    /// tip, so a tap on RACE is answered at once instead of by a frozen frame while the race loads. Built into the
    /// menu and kept across scenes; it lifts once the new scene has drawn its first frames.
    public sealed class LoadingCurtain : MonoBehaviour
    {
        public static LoadingCurtain Instance { get; private set; }

        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text kicker, title, tip;
        [SerializeField] RectTransform spinner;

        static readonly string[] Tips =
        {
            "Hold DRIFT through a corner, then let go: the longer the slide, the bigger the boost.",
            "Every item box deals something different, and never what you just picked up.",
            "Chevrons on the road are turbo pads. Drive straight over them.",
            "In a Grand Prix the grid reverses each race: win now, start from the back next time.",
            "Rockets fly straight. Homing ones find the kart ahead. Bombs drop behind you and wait.",
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; }
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// Loads `scene` under the curtain. False when there is no curtain (a scene started on its own in the editor).
        public static bool Load(string scene, string heading, string sub)
        {
            if (Instance == null || Instance.group == null) return false;
            Instance.StopAllCoroutines();
            Instance.StartCoroutine(Instance.Run(scene, heading, sub));
            return true;
        }

        IEnumerator Run(string scene, string heading, string sub)
        {
            if (title != null) title.text = heading;
            if (kicker != null) kicker.text = sub;
            if (tip != null) tip.text = "TIP   " + Tips[Random.Range(0, Tips.Length)];
            group.blocksRaycasts = true;
            Tween.StopAll(group);
            yield return Tween.Alpha(group, 1f, 0.18f, Ease.OutCubic, useUnscaledTime: true).ToYieldInstruction();
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            for (int i = 0; i < 3; i++) yield return null;      // the new scene's first heavy frames (shaders, the drone's first cut) stay covered
            yield return Tween.Alpha(group, 0f, 0.35f, Ease.OutCubic, useUnscaledTime: true).ToYieldInstruction();
            group.blocksRaycasts = false;
        }

        void Update()
        {
            if (spinner != null && group != null && group.alpha > 0f) spinner.Rotate(0f, 0f, -300f * Time.unscaledDeltaTime);
        }
    }
}
